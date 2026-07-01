using FacialBiometric.Application.Abstractions.Messaging;

namespace FacialBiometric.Application.Features.Users.AuthenticateFace;

/// <param name="PhotoContent">Foto candidata (selfie) a ser comparada contra todos os usuários cadastrados.</param>
public sealed record AuthenticateFaceCommand(byte[] PhotoContent)
    : ICommand<AuthenticateFaceResponse>;
