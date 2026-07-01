using FacialBiometric.Application.Abstractions.Messaging;
using FacialBiometric.Domain.Biometrics;
using FacialBiometric.Domain.Entities;
using FacialBiometric.Domain.Primitives;
using FacialBiometric.Domain.Repositories;

namespace FacialBiometric.Application.Features.Users.AuthenticateFace;

/// <summary>
/// Autenticação 1:N — recebe só uma foto (sem Id de usuário) e procura, entre todos
/// os usuários cadastrados, qual rosto corresponde. Diferente de VerifyFace (1:1, que
/// já sabe qual usuário comparar), aqui o usuário é desconhecido a priori.
/// </summary>
internal sealed class AuthenticateFaceCommandHandler(
    IUserRepository repository,
    IFacialBiometricProvider facialBiometricProvider)
    : ICommandHandler<AuthenticateFaceCommand, AuthenticateFaceResponse>
{
    private static readonly AuthenticateFaceResponse NotFoundResponse =
        new(false, null, null, null, "Usuário não existe na base de dados.");

    public async Task<Result<AuthenticateFaceResponse>> Handle(
        AuthenticateFaceCommand request, CancellationToken cancellationToken)
    {
        // 1. Extrair o embedding da foto candidata (pode falhar: nenhum rosto detectado
        //    ou imagem inválida — nesses casos retorna erro, não "não autenticado").
        var embeddingResult = await facialBiometricProvider.ExtractEmbeddingAsync(
            request.PhotoContent, cancellationToken);

        if (embeddingResult.IsFailure)
        {
            return Result.Failure<AuthenticateFaceResponse>(embeddingResult.Error);
        }

        var candidateEmbedding = embeddingResult.Value.Embedding;

        // 2. Comparar contra todos os usuários ativos que já têm rosto cadastrado,
        //    ficando com o de maior similaridade.
        var existingUsers = await repository.GetActiveWithFaceEmbeddingAsync(cancellationToken);

        User? bestMatch = null;
        var bestConfidence = 0.0;

        foreach (var existingUser in existingUsers)
        {
            var matchResult = await facialBiometricProvider.CompareEmbeddingsAsync(
                existingUser.FaceEmbedding!, candidateEmbedding, cancellationToken);

            if (matchResult.IsFailure || !matchResult.Value.IsMatch)
            {
                continue;
            }

            if (bestMatch is null || matchResult.Value.Confidence > bestConfidence)
            {
                bestMatch = existingUser;
                bestConfidence = matchResult.Value.Confidence;
            }
        }

        // 3. Nenhum usuário correspondente encontrado.
        if (bestMatch is null)
        {
            return Result.Success(NotFoundResponse);
        }

        // 4. Rosto reconhecido.
        var response = new AuthenticateFaceResponse(
            true, bestMatch.Id, bestMatch.FullName, bestConfidence, null);

        return Result.Success(response);
    }
}
