namespace FacialBiometric.Domain.Biometrics;

/// <summary>
/// Resultado da extração de características faciais de uma foto.
/// </summary>
/// <param name="Embedding">Vetor de características serializado (JSON) — formato depende do provedor.</param>
/// <param name="Confidence">Confiança de que a imagem contém um rosto válido (0.0 a 1.0).</param>
public sealed record FaceEmbeddingResult(string Embedding, double Confidence);
