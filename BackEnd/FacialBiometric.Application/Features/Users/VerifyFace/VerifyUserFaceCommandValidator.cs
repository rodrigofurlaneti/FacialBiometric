using FluentValidation;

namespace FacialBiometric.Application.Features.Users.VerifyFace;

public sealed class VerifyUserFaceCommandValidator : AbstractValidator<VerifyUserFaceCommand>
{
    private const int MaxPhotoSizeBytes = 5 * 1024 * 1024; // 5 MB

    public VerifyUserFaceCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);

        RuleFor(x => x.PhotoContent)
            .NotEmpty().WithMessage("Foto para comparação é obrigatória.")
            .Must(p => p.Length <= MaxPhotoSizeBytes)
            .WithMessage($"Foto deve ter no máximo {MaxPhotoSizeBytes / 1024 / 1024} MB.");
    }
}
