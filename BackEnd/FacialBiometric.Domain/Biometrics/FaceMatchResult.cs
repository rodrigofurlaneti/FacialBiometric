namespace FacialBiometric.Domain.Biometrics;

/// <summary>
/// Resultado da comparação entre um embedding cadastrado e uma foto candidata.
/// </summary>
/// <param name="IsMatch">Verdadeiro se as faces foram consideradas a mesma pessoa.</param>
/// <param name="Confidence">Score de similaridade (0.0 a 1.0) usado na decisão.</param>
public sealed record FaceMatchResult(bool IsMatch, double Confidence);
