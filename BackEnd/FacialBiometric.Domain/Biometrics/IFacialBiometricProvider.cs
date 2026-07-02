using FacialBiometric.Domain.Primitives;

namespace FacialBiometric.Domain.Biometrics;

/// <summary>
/// Contrato para o motor de biometria facial (extração + comparação de rostos).
///
/// Implementação concreta: <c>LocalFacialBiometricProvider</c>
/// (Infrastructure/Biometrics), usando FaceONNX (detecção + landmarks +
/// embedding 512-d via ONNX Runtime, local/on-premise). Ver
/// README.md para detalhes do modelo e do threshold
/// de decisão usado na comparação.
/// </summary>
public interface IFacialBiometricProvider
{
    /// <summary>Extrai o embedding facial de uma foto (usado no cadastro).</summary>
    Task<Result<FaceEmbeddingResult>> ExtractEmbeddingAsync(
        byte[] photoContent,
        CancellationToken cancellationToken = default);

    /// <summary>Compara o embedding cadastrado com uma foto candidata (usado na verificação).</summary>
    Task<Result<FaceMatchResult>> CompareAsync(
        string storedEmbedding,
        byte[] candidatePhotoContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compara dois embeddings já extraídos, sem reprocessar imagem (usado na checagem
    /// de rosto duplicado durante o cadastro, contra vários usuários já cadastrados).
    /// </summary>
    Task<Result<FaceMatchResult>> CompareEmbeddingsAsync(
        string storedEmbedding,
        string candidateEmbedding,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decodifica um embedding serializado (JSON) para o vetor de floats. Retorna null se
    /// o texto estiver corrompido/em formato inesperado. Usado para popular o
    /// <see cref="IFaceEmbeddingIndex"/> uma única vez por usuário, em vez de reparsear o
    /// JSON a cada comparação.
    /// </summary>
    float[]? DecodeEmbedding(string embedding);

    /// <summary>
    /// Compara dois embeddings já decodificados (sem I/O, sem parsing de JSON) — é a
    /// operação "quente" usada em loop pela autenticação 1:N via <see cref="IFaceEmbeddingIndex"/>.
    /// </summary>
    FaceMatchResult CompareDecodedEmbeddings(float[] storedEmbedding, float[] candidateEmbedding);
}
