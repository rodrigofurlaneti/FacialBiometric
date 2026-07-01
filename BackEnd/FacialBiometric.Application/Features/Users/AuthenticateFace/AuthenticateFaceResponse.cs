namespace FacialBiometric.Application.Features.Users.AuthenticateFace;

/// <param name="IsAuthenticated">Verdadeiro se o rosto enviado corresponde a um usuário cadastrado.</param>
/// <param name="UserId">Id do usuário identificado (null se não autenticado).</param>
/// <param name="FullName">Nome completo do usuário identificado (null se não autenticado).</param>
/// <param name="Confidence">Score de similaridade do melhor match (null se não autenticado).</param>
/// <param name="Message">Mensagem explicando o resultado (preenchida quando não autenticado).</param>
public sealed record AuthenticateFaceResponse(
    bool IsAuthenticated,
    long? UserId,
    string? FullName,
    double? Confidence,
    string? Message);
