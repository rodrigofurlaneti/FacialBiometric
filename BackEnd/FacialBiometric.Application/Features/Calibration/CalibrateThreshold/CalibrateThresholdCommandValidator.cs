using FluentValidation;

namespace FacialBiometric.Application.Features.Calibration.CalibrateThreshold;

public sealed class CalibrateThresholdCommandValidator : AbstractValidator<CalibrateThresholdCommand>
{
    private const int MaxPhotoSizeBytes = 5 * 1024 * 1024; // 5 MB
    private const int MaxSamples = 200; // gera até ~20 mil pares — suficiente pra um dataset de calibração

    public CalibrateThresholdCommandValidator()
    {
        RuleFor(x => x.Samples)
            .Must(s => s.Count >= 2)
            .WithMessage("Envie ao menos 2 fotos rotuladas.")
            .Must(s => s.Count <= MaxSamples)
            .WithMessage($"Máximo de {MaxSamples} fotos por chamada.");

        RuleForEach(x => x.Samples).ChildRules(sample =>
        {
            sample.RuleFor(s => s.PersonLabel)
                .NotEmpty().WithMessage("Cada foto precisa de um rótulo (nome da pessoa).");

            sample.RuleFor(s => s.PhotoContent)
                .NotEmpty().WithMessage("Foto vazia.")
                .Must(p => p.Length <= MaxPhotoSizeBytes)
                .WithMessage($"Cada foto deve ter no máximo {MaxPhotoSizeBytes / 1024 / 1024} MB.");
        });
    }
}
