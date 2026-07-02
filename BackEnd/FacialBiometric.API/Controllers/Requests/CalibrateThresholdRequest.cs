using Microsoft.AspNetCore.Http;

namespace FacialBiometric.API.Controllers.Requests;

/// <summary>
/// multipart/form-data: Photos[i] e PersonLabels[i] andam juntos pelo índice —
/// a foto Photos[0] pertence à pessoa PersonLabels[0], e assim por diante.
/// Fotos com o mesmo rótulo formam pares "genuínos"; rótulos diferentes, "impostores".
/// </summary>
public sealed class CalibrateThresholdRequest
{
    public List<IFormFile> Photos { get; init; } = [];

    public List<string> PersonLabels { get; init; } = [];
}
