using FacialBiometric.Application.Abstractions.Messaging;

namespace FacialBiometric.Application.Features.Users.VerifyFace;

/// <param name="UserId">Usuário cuja foto cadastrada será usada como referência.</param>
/// <param name="PhotoContent">Foto candidata (ex: selfie tirada no momento) para comparação.</param>
public sealed record VerifyUserFaceCommand(long UserId, byte[] PhotoContent)
    : ICommand<FaceVerificationResponse>;
