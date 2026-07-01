using FacialBiometric.Application.Abstractions.Messaging;

namespace FacialBiometric.Application.Features.Users.Register;

/// <param name="FullName">Nome completo do usuário.</param>
/// <param name="PhotoContent">Bytes da foto enviada (multipart/form-data).</param>
/// <param name="PhotoFileName">Nome original do arquivo (usado para extensão).</param>
public sealed record RegisterUserCommand(
    string FullName,
    byte[] PhotoContent,
    string PhotoFileName) : ICommand<long>;
