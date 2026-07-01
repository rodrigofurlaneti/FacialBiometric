using Microsoft.AspNetCore.Http;

namespace FacialBiometric.API.Controllers.Requests;

/// <summary>multipart/form-data: Photo (arquivo a identificar entre os usuários cadastrados).</summary>
public sealed class AuthenticateFaceRequest
{
    public IFormFile Photo { get; init; } = null!;
}
