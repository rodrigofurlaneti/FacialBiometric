using FacialBiometric.Application.Abstractions.Messaging;
using FacialBiometric.Domain.Biometrics;
using FacialBiometric.Domain.Primitives;
using FacialBiometric.Domain.Repositories;

namespace FacialBiometric.Application.Features.Users.VerifyFace;

internal sealed class VerifyUserFaceCommandHandler(
    IUserRepository repository,
    IFacialBiometricProvider facialBiometricProvider)
    : ICommandHandler<VerifyUserFaceCommand, FaceVerificationResponse>
{
    public async Task<Result<FaceVerificationResponse>> Handle(
        VerifyUserFaceCommand request, CancellationToken cancellationToken)
    {
        var user = await repository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<FaceVerificationResponse>(
                new Error("User.NotFound", $"Usuário {request.UserId} não encontrado."));
        }

        if (!user.HasPhoto || string.IsNullOrWhiteSpace(user.FaceEmbedding))
        {
            return Result.Failure<FaceVerificationResponse>(
                new Error("User.PhotoNotRegistered", "Usuário ainda não tem foto/embedding cadastrados."));
        }

        // Compara via FaceONNX (cosine similarity dos embeddings 512-d).
        // Pode falhar com "FacialBiometric.NoFaceDetected" se a foto enviada
        // não tiver um rosto identificável.
        var matchResult = await facialBiometricProvider.CompareAsync(
            user.FaceEmbedding, request.PhotoContent, cancellationToken);

        if (matchResult.IsFailure)
        {
            return Result.Failure<FaceVerificationResponse>(matchResult.Error);
        }

        var response = new FaceVerificationResponse(
            user.Id, matchResult.Value.IsMatch, matchResult.Value.Confidence);

        return Result.Success(response);
    }
}
