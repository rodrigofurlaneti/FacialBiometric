using FacialBiometric.Application.Abstractions.Messaging;

namespace FacialBiometric.Application.Features.Calibration.CalibrateThreshold;

/// <param name="PersonLabel">Rótulo da pessoa (ex: nome) — fotos com o mesmo rótulo formam pares "genuínos".</param>
/// <param name="PhotoContent">Bytes da foto.</param>
public sealed record CalibrationSample(string PersonLabel, byte[] PhotoContent);

/// <param name="Samples">Fotos rotuladas: precisa de ao menos 2 pessoas distintas (pares impostores) e
/// pelo menos uma pessoa com 2+ fotos (pares genuínos) pra gerar uma curva útil.</param>
public sealed record CalibrateThresholdCommand(IReadOnlyList<CalibrationSample> Samples)
    : ICommand<CalibrateThresholdResponse>;
