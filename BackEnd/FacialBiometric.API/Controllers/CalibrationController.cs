using FacialBiometric.API.Controllers.Requests;
using FacialBiometric.Application.Features.Calibration.CalibrateThreshold;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FacialBiometric.API.Controllers;

/// <summary>
/// Ferramentas de apoio operacional — não fazem parte do fluxo de cadastro/autenticação
/// em si. Pensada para uso pontual (calibração, diagnóstico), não pra tráfego de produção.
/// </summary>
[Authorize]
public sealed class CalibrationController(IMediator mediator) : ApiController(mediator)
{
    /// <summary>
    /// Recebe fotos rotuladas por pessoa (mesmo rótulo = mesma pessoa) e sugere um
    /// threshold de similaridade com base na distribuição real de FAR/FRR dessas
    /// amostras. É um ponto de partida, não substitui testar com uma amostra
    /// representativa do seu público antes de mudar o threshold em produção.
    /// </summary>
    [HttpPost("threshold")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> CalibrateThreshold(
        [FromForm] CalibrateThresholdRequest request, CancellationToken ct)
    {
        if (request.Photos.Count != request.PersonLabels.Count)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Calibration.MismatchedInput",
                Detail = "Photos e PersonLabels precisam ter a mesma quantidade de itens (um rótulo por foto, na mesma ordem)."
            });
        }

        var samples = new List<CalibrationSample>(request.Photos.Count);
        for (var i = 0; i < request.Photos.Count; i++)
        {
            await using var stream = new MemoryStream();
            await request.Photos[i].CopyToAsync(stream, ct);
            samples.Add(new CalibrationSample(request.PersonLabels[i], stream.ToArray()));
        }

        var command = new CalibrateThresholdCommand(samples);
        var result = await Mediator.Send(command, ct);

        return result.IsFailure ? HandleFailure(result) : Ok(result.Value);
    }
}
