using Microsoft.AspNetCore.Http;

namespace FacialBiometric.API.Controllers.Requests;

/// <summary>multipart/form-data: Photo (arquivo a comparar com a foto cadastrada).</summary>
public sealed class VerifyUserFaceRequest
{
    public IFormFile Photo { get; init; } = null!;
}
