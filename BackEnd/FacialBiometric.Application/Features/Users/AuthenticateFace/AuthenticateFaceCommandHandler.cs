using FacialBiometric.Application.Abstractions.Messaging;
using FacialBiometric.Domain.Biometrics;
using FacialBiometric.Domain.Primitives;

namespace FacialBiometric.Application.Features.Users.AuthenticateFace;

/// <summary>
/// Autenticação 1:N — recebe só uma foto (sem Id de usuário) e procura, entre todos
/// os usuários cadastrados, qual rosto corresponde. Diferente de VerifyFace (1:1, que
/// já sabe qual usuário comparar), aqui o usuário é desconhecido a priori.
/// </summary>
internal sealed class AuthenticateFaceCommandHandler(
    IFacialBiometricProvider facialBiometricProvider,
    IFaceEmbeddingIndex faceEmbeddingIndex,
    ILivenessDetector livenessDetector)
    : ICommandHandler<AuthenticateFaceCommand, AuthenticateFaceResponse>
{
    private static readonly AuthenticateFaceResponse NotFoundResponse =
        new(false, null, null, null, "Usuário não existe na base de dados.");

    public async Task<Result<AuthenticateFaceResponse>> Handle(
        AuthenticateFaceCommand request, CancellationToken cancellationToken)
    {
        // 1. Checagem de vivacidade (heurística passiva) — aplicada aqui e não no
        //    cadastro, porque este é o "portão de acesso" de verdade. Ver
        //    PassiveLivenessDetector e o README para as limitações dessa heurística.
        var liveness = await livenessDetector.AssessAsync(request.PhotoContent, cancellationToken);
        if (!liveness.IsLikelyLive)
        {
            return Result.Failure<AuthenticateFaceResponse>(new Error(
                "FacialBiometric.LivenessCheckFailed",
                $"A foto não passou na checagem de vivacidade (score {liveness.Score:0.00}). " +
                $"Sinais: {(liveness.Signals.Count == 0 ? "nenhum específico" : string.Join(", ", liveness.Signals))}. " +
                "Tire uma nova foto direto da câmera, com boa iluminação e sem reflexos."));
        }

        // 2. Extrair o embedding da foto candidata.
        var embeddingResult = await facialBiometricProvider.ExtractEmbeddingAsync(
            request.PhotoContent, cancellationToken);

        if (embeddingResult.IsFailure)
        {
            return Result.Failure<AuthenticateFaceResponse>(embeddingResult.Error);
        }

        var candidateEmbedding = facialBiometricProvider.DecodeEmbedding(embeddingResult.Value.Embedding);
        if (candidateEmbedding is null)
        {
            return Result.Failure<AuthenticateFaceResponse>(new Error(
                "FacialBiometric.InvalidStoredEmbedding",
                "Falha ao processar o embedding extraído da foto enviada."));
        }

        // 3. Comparar contra o índice em memória, em paralelo (CPU-bound, sem I/O —
        //    os embeddings já estão decodificados). Ficamos com o de maior confiança.
        var indexedUsers = await faceEmbeddingIndex.GetAllAsync(cancellationToken);

        var lockObj = new object();
        FaceEmbeddingIndexEntry? bestMatch = null;
        var bestConfidence = 0.0;

        Parallel.ForEach(indexedUsers, entry =>
        {
            var match = facialBiometricProvider.CompareDecodedEmbeddings(entry.Embedding, candidateEmbedding);
            if (!match.IsMatch)
            {
                return;
            }

            lock (lockObj)
            {
                if (bestMatch is null || match.Confidence > bestConfidence)
                {
                    bestMatch = entry;
                    bestConfidence = match.Confidence;
                }
            }
        });

        // 4. Nenhum usuário correspondente encontrado.
        if (bestMatch is null)
        {
            return Result.Success(NotFoundResponse);
        }

        // 5. Rosto reconhecido.
        var response = new AuthenticateFaceResponse(
            true, bestMatch.UserId, bestMatch.FullName, bestConfidence, null);

        return Result.Success(response);
    }
}
