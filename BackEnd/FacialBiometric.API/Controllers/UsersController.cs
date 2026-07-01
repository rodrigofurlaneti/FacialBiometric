using FacialBiometric.API.Controllers.Requests;
using FacialBiometric.Application.Features.Users.AuthenticateFace;
using FacialBiometric.Application.Features.Users.HasPhoto;
using FacialBiometric.Application.Features.Users.Register;
using FacialBiometric.Application.Features.Users.VerifyFace;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FacialBiometric.API.Controllers;

[Authorize]
public sealed class UsersController(IMediator mediator) : ApiController(mediator)
{
    /// <summary>Cadastra um usuário (nome completo + foto) para biometria facial.</summary>
    [HttpPost]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Register(
        [FromForm] RegisterUserRequest request, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await request.Photo.CopyToAsync(stream, ct);

        var command = new RegisterUserCommand(request.FullName, stream.ToArray(), request.Photo.FileName);
        var result = await Mediator.Send(command, ct);

        return result.IsFailure
            ? HandleFailure(result)
            : CreatedAtAction(nameof(HasPhoto), new { id = result.Value }, new { id = result.Value });
    }

    /// <summary>Retorna se o usuário já tem foto/biometria cadastrada.</summary>
    [HttpGet("{id:long}/has-photo")]
    public async Task<IActionResult> HasPhoto(long id, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetUserHasPhotoQuery(id), ct);
        return result.IsFailure ? HandleFailure(result) : Ok(result.Value);
    }

    /// <summary>
    /// Compara uma foto enviada com a foto cadastrada do usuário.
    /// ⚠️ Retorna 400 "FacialBiometric.NotConfigured" até o modelo de
    /// biometria facial ser implementado (ver README_FACIALBIOMETRIC_API.md).
    /// </summary>
    [HttpPost("{id:long}/verify-face")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> VerifyFace(
        long id, [FromForm] VerifyUserFaceRequest request, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await request.Photo.CopyToAsync(stream, ct);

        var command = new VerifyUserFaceCommand(id, stream.ToArray());
        var result = await Mediator.Send(command, ct);

        return result.IsFailure ? HandleFailure(result) : Ok(result.Value);
    }

    /// <summary>
    /// Autenticação por Face ID (1:N): recebe só uma foto e procura entre todos os
    /// usuários cadastrados quem corresponde. Retorna IsAuthenticated=true + nome
    /// completo quando encontra, ou IsAuthenticated=false + mensagem quando não.
    /// </summary>
    [HttpPost("authenticate-face")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> AuthenticateFace(
        [FromForm] AuthenticateFaceRequest request, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        await request.Photo.CopyToAsync(stream, ct);

        var command = new AuthenticateFaceCommand(stream.ToArray());
        var result = await Mediator.Send(command, ct);

        return result.IsFailure ? HandleFailure(result) : Ok(result.Value);
    }
}
