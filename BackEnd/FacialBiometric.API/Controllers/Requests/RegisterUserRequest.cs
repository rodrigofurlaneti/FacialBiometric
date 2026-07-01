using Microsoft.AspNetCore.Http;

namespace FacialBiometric.API.Controllers.Requests;

/// <summary>multipart/form-data: FullName (texto) + Photo (arquivo).</summary>
public sealed class RegisterUserRequest
{
    public string FullName { get; init; } = null!;
    public IFormFile Photo { get; init; } = null!;
}
