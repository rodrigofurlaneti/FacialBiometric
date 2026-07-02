namespace FacialBiometric.Application.Features.Calibration.CalibrateThreshold;

/// <param name="Threshold">Similaridade de cosseno candidata a limiar de decisão.</param>
/// <param name="FalseAcceptRate">Fração de pares IMPOSTORES (pessoas diferentes) que ficariam acima
/// desse threshold — ou seja, seriam aceitos como a mesma pessoa por engano.</param>
/// <param name="FalseRejectRate">Fração de pares GENUÍNOS (mesma pessoa) que ficariam abaixo desse
/// threshold — ou seja, seriam rejeitados por engano.</param>
public sealed record ThresholdCandidate(double Threshold, double FalseAcceptRate, double FalseRejectRate);

/// <param name="TotalPhotos">Total de fotos recebidas na chamada.</param>
/// <param name="PhotosWithFaceDetected">Quantas tiveram rosto detectável (as demais foram ignoradas).</param>
/// <param name="GenuinePairs">Pares formados por fotos da MESMA pessoa (mesmo rótulo).</param>
/// <param name="ImpostorPairs">Pares formados por fotos de pessoas DIFERENTES.</param>
/// <param name="SuggestedThreshold">Threshold onde |FAR - FRR| é mínimo nesta amostra (aproximação do Equal Error Rate).</param>
/// <param name="FarAtSuggested">Taxa de falso aceite no threshold sugerido.</param>
/// <param name="FrrAtSuggested">Taxa de falso rejeite no threshold sugerido.</param>
/// <param name="Curve">FAR/FRR pra cada threshold testado (0.30 a 0.95, passo 0.01) — use pra escolher
/// um ponto diferente do sugerido, priorizando FAR baixo (menos falsos aceites) ou FRR baixo
/// (menos falsos rejeites) conforme o risco aceitável do seu caso de uso.</param>
public sealed record CalibrateThresholdResponse(
    int TotalPhotos,
    int PhotosWithFaceDetected,
    int GenuinePairs,
    int ImpostorPairs,
    double SuggestedThreshold,
    double FarAtSuggested,
    double FrrAtSuggested,
    IReadOnlyList<ThresholdCandidate> Curve);
