using FluentValidation;

namespace FacialBiometric.Application.Features.Users.Register;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    private const int MaxPhotoSizeBytes = 5 * 1024 * 1024; // 5 MB
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png"];

    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Nome completo é obrigatório.")
            .MinimumLength(3)
            .MaximumLength(200);

        RuleFor(x => x.PhotoContent)
            .NotEmpty().WithMessage("Foto é obrigatória.")
            .Must(p => p.Length <= MaxPhotoSizeBytes)
            .WithMessage($"Foto deve ter no máximo {MaxPhotoSizeBytes / 1024 / 1024} MB.");

        RuleFor(x => x.PhotoFileName)
            .NotEmpty().WithMessage("Nome do arquivo da foto é obrigatório.")
            .Must(HasAllowedExtension)
            .WithMessage("Foto deve ser .jpg, .jpeg ou .png.");
    }

    private static bool HasAllowedExtension(string fileName)
        => AllowedExtensions.Contains(Path.GetExtension(fileName)?.ToLowerInvariant());
}
