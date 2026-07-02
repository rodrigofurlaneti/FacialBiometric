using System.Linq;
using System.Text.Json;
using FaceONNX;
using FacialBiometric.Domain.Biometrics;
using FacialBiometric.Domain.Primitives;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FacialBiometric.Infrastructure.Biometrics;

/// <summary>
/// Implementação local (on-premise) de <see cref="IFacialBiometricProvider"/> usando
/// FaceONNX (detecção YOLOv5-face + landmarks 68 pontos + embedding ResNet27, 512-d,
/// tudo via ONNX Runtime, sem chamada externa/cloud).
///
/// Modelos vêm embutidos no pacote NuGet FaceONNX (não precisa baixar nada à parte).
///
/// ⚠️ Threshold de decisão (<see cref="MatchThreshold"/>): comece com 0.62 e recalibre
/// com fotos reais do seu público antes de ir pra produção — ver README_FACIALBIOMETRIC_API.md.
///
/// Registrado como Singleton no DI (ver Infrastructure/DependencyInjection.cs): os modelos
/// ONNX são caros para carregar, então a sessão de inferência é reaproveitada entre
/// requisições. FaceDetector/Face68LandmarksExtractor/FaceEmbedder do FaceONNX são
/// thread-safe para chamadas de Forward (só leitura da sessão do ONNX Runtime).
/// </summary>
internal sealed class LocalFacialBiometricProvider : IFacialBiometricProvider, IDisposable
{
    /// <summary>Similaridade de cosseno mínima (0 a 1) para considerar a mesma pessoa.</summary>
    private const float MatchThreshold = 0.62f;

    private static readonly Error NoFaceDetectedError = new(
        "FacialBiometric.NoFaceDetected",
        "Nenhum rosto foi detectado na foto enviada.");

    private static readonly Error InvalidImageError = new(
        "FacialBiometric.InvalidImage",
        "A foto enviada não pôde ser lida (formato inválido ou arquivo corrompido).");

    private static readonly Error InvalidStoredEmbeddingError = new(
        "FacialBiometric.InvalidStoredEmbedding",
        "O embedding cadastrado para este usuário está corrompido — refaça o cadastro da foto.");

    private readonly FaceDetector _faceDetector = new();
    private readonly Face68LandmarksExtractor _landmarksExtractor = new();
    private readonly FaceEmbedder _faceEmbedder = new();

    public Task<Result<FaceEmbeddingResult>> ExtractEmbeddingAsync(
        byte[] photoContent, CancellationToken cancellationToken = default)
    {
        var extraction = TryExtractEmbedding(photoContent);

        if (extraction.Error is not null)
        {
            return Task.FromResult(Result.Failure<FaceEmbeddingResult>(extraction.Error));
        }

        var json = JsonSerializer.Serialize(extraction.Embedding);
        return Task.FromResult(Result.Success(new FaceEmbeddingResult(json, extraction.Score)));
    }

    public Task<Result<FaceMatchResult>> CompareAsync(
        string storedEmbedding, byte[] candidatePhotoContent, CancellationToken cancellationToken = default)
    {
        if (!TryDeserializeEmbedding(storedEmbedding, out var stored))
        {
            return Task.FromResult(Result.Failure<FaceMatchResult>(InvalidStoredEmbeddingError));
        }

        var extraction = TryExtractEmbedding(candidatePhotoContent);
        if (extraction.Error is not null)
        {
            return Task.FromResult(Result.Failure<FaceMatchResult>(extraction.Error));
        }

        var similarity = CosineSimilarity(stored!, extraction.Embedding!);
        var isMatch = similarity >= MatchThreshold;

        return Task.FromResult(Result.Success(new FaceMatchResult(isMatch, similarity)));
    }

    public Task<Result<FaceMatchResult>> CompareEmbeddingsAsync(
        string storedEmbedding, string candidateEmbedding, CancellationToken cancellationToken = default)
    {
        if (!TryDeserializeEmbedding(storedEmbedding, out var stored))
        {
            return Task.FromResult(Result.Failure<FaceMatchResult>(InvalidStoredEmbeddingError));
        }

        if (!TryDeserializeEmbedding(candidateEmbedding, out var candidate))
        {
            return Task.FromResult(Result.Failure<FaceMatchResult>(InvalidStoredEmbeddingError));
        }

        var similarity = CosineSimilarity(stored!, candidate!);
        var isMatch = similarity >= MatchThreshold;

        return Task.FromResult(Result.Success(new FaceMatchResult(isMatch, similarity)));
    }

    public float[]? DecodeEmbedding(string? embedding)
        => embedding is not null && TryDeserializeEmbedding(embedding, out var value) ? value : null;

    public FaceMatchResult CompareDecodedEmbeddings(float[] storedEmbedding, float[] candidateEmbedding)
    {
        var similarity = CosineSimilarity(storedEmbedding, candidateEmbedding);
        return new FaceMatchResult(similarity >= MatchThreshold, similarity);
    }

    private static bool TryDeserializeEmbedding(string json, out float[]? embedding)
    {
        try
        {
            embedding = JsonSerializer.Deserialize<float[]>(json);
        }
        catch (JsonException)
        {
            embedding = null;
            return false;
        }

        return embedding is not null && embedding.Length == FaceEmbedder.EmbeddingSize;
    }

    /// <summary>Detecta o maior/melhor rosto da foto e retorna o embedding (512-d) alinhado.</summary>
    private (float[]? Embedding, double Score, Error? Error) TryExtractEmbedding(byte[] photoContent)
    {
        float[][,] pixels;
        try
        {
            using var image = Image.Load<Rgb24>(photoContent);
            pixels = ToBgrFloatArray(image);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            return (null, 0, InvalidImageError);
        }

        var faces = _faceDetector.Forward(pixels);
        var best = faces.OrderByDescending(f => f.Score).FirstOrDefault();

        if (best is null || best.Rectangle.IsEmpty)
        {
            return (null, 0, NoFaceDetectedError);
        }

        var landmarks = _landmarksExtractor.Forward(pixels, best.Rectangle);
        var aligned = FaceProcessingExtensions.Align(pixels, best.Rectangle, landmarks.RotationAngle);
        var embedding = _faceEmbedder.Forward(aligned);

        return (embedding, best.Score, null);
    }

    /// <summary>FaceONNX espera a imagem como float[3][,] em ordem BGR, valores normalizados 0..1.</summary>
    private static float[][,] ToBgrFloatArray(Image<Rgb24> image)
    {
        var array = new[]
        {
            new float[image.Height, image.Width], // B
            new float[image.Height, image.Width], // G
            new float[image.Height, image.Width]  // R
        };

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < accessor.Width; x++)
                {
                    array[2][y, x] = row[x].R / 255f;
                    array[1][y, x] = row[x].G / 255f;
                    array[0][y, x] = row[x].B / 255f;
                }
            }
        });

        return array;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || a.Length != b.Length)
        {
            return 0f;
        }

        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
        {
            return 0f;
        }

        return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }

    public void Dispose()
    {
        _faceDetector.Dispose();
        _landmarksExtractor.Dispose();
        _faceEmbedder.Dispose();
    }
}
