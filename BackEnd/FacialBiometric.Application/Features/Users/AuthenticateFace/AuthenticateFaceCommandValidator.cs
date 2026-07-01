using FluentValidation;

namespace FacialBiometric.Application.Features.Users.AuthenticateFace;

public sealed class AuthenticateFaceCommandValidator : AbstractValidator<AuthenticateFaceCommand>
{
    private const int MaxPhotoSizeBytes = 5 * 1024 * 1024; // 5 MB

    public AuthenticateFaceCommandValidator()
    {
        RuleFor(x => x.PhotoContent)
            .NotEmpty().WithMessage("Foto para autenticação é obrigatória.")
            .Must(p => p.Length <= MaxPhotoSizeBytes)
            .WithMessage($"Foto deve ter no máximo {MaxPhotoSizeBytes / 1024 / 1024} MB.");
    }
}
