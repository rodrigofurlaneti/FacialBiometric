using FacialBiometric.Application.Abstractions.Messaging;
using FacialBiometric.Domain.Biometrics;
using FacialBiometric.Domain.Primitives;

namespace FacialBiometric.Application.Features.Calibration.CalibrateThreshold;

/// <summary>
/// Ferramenta de apoio (não substitui teste com dados reais do seu público): recebe
/// fotos rotuladas por pessoa, extrai o embedding de cada uma, gera todos os pares
/// possíveis (genuínos = mesmo rótulo, impostores = rótulos diferentes) e testa uma
/// faixa de thresholds candidatos, reportando FAR (falso aceite) e FRR (falso
/// rejeite) pra cada um. Sugere o threshold onde |FAR - FRR| é mínimo (aproximação
/// do "Equal Error Rate").
/// </summary>
internal sealed class CalibrateThresholdCommandHandler(IFacialBiometricProvider facialBiometricProvider)
    : ICommandHandler<CalibrateThresholdCommand, CalibrateThresholdResponse>
{
    // 0.30 a 0.95, passo 0.01 — cobre a faixa plausível pra similaridade de cosseno
    // de embeddings faciais (abaixo de 0.30 ou acima de 0.95 raramente é um limiar útil).
    private static readonly double[] CandidateThresholds =
        Enumerable.Range(30, 66).Select(i => Math.Round(i / 100.0, 2)).ToArray();

    public async Task<Result<CalibrateThresholdResponse>> Handle(
        CalibrateThresholdCommand request, CancellationToken cancellationToken)
    {
        if (request.Samples.Select(s => s.PersonLabel).Distinct().Count() < 2)
        {
            return Result.Failure<CalibrateThresholdResponse>(new Error(
                "Calibration.NotEnoughPeople",
                "É preciso pelo menos 2 pessoas diferentes (rótulos distintos) nas amostras, para gerar pares impostores."));
        }

        // 1. Extrair o embedding de cada foto — ignora silenciosamente as que não
        //    tiverem rosto detectável (não derruba a calibração inteira por causa
        //    de uma foto ruim isolada).
        var extracted = new List<(string Label, float[] Embedding)>();

        foreach (var sample in request.Samples)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extraction = await facialBiometricProvider.ExtractEmbeddingAsync(sample.PhotoContent, cancellationToken);
            if (!extraction.IsSuccess)
            {
                continue;
            }

            var vector = facialBiometricProvider.DecodeEmbedding(extraction.Value.Embedding);
            if (vector is not null)
            {
                extracted.Add((sample.PersonLabel, vector));
            }
        }

        if (extracted.Count < 2)
        {
            return Result.Failure<CalibrateThresholdResponse>(new Error(
                "Calibration.NotEnoughValidPhotos",
                "Poucas fotos com rosto detectável para calibrar (mínimo 2)."));
        }

        // 2. Gerar todos os pares únicos e classificar como genuíno (mesmo rótulo) ou impostor.
        var genuineSimilarities = new List<double>();
        var impostorSimilarities = new List<double>();

        for (var i = 0; i < extracted.Count; i++)
        {
            for (var j = i + 1; j < extracted.Count; j++)
            {
                var match = facialBiometricProvider.CompareDecodedEmbeddings(extracted[i].Embedding, extracted[j].Embedding);

                if (extracted[i].Label == extracted[j].Label)
                {
                    genuineSimilarities.Add(match.Confidence);
                }
                else
                {
                    impostorSimilarities.Add(match.Confidence);
                }
            }
        }

        if (genuineSimilarities.Count == 0)
        {
            return Result.Failure<CalibrateThresholdResponse>(new Error(
                "Calibration.NoGenuinePairs",
                "Nenhuma pessoa tem 2 ou mais fotos — sem pares genuínos não dá pra calcular FRR. Envie ao menos 2 fotos da mesma pessoa."));
        }

        // 3. Testar cada threshold candidato.
        var curve = CandidateThresholds
            .Select(threshold => new ThresholdCandidate(
                threshold,
                FalseAcceptRate: impostorSimilarities.Count == 0
                    ? 0
                    : impostorSimilarities.Count(s => s >= threshold) / (double)impostorSimilarities.Count,
                FalseRejectRate: genuineSimilarities.Count(s => s < threshold) / (double)genuineSimilarities.Count))
            .ToList();

        // 4. Sugestão: threshold onde |FAR - FRR| é mínimo (aproximação do Equal Error Rate).
        var suggested = curve.OrderBy(c => Math.Abs(c.FalseAcceptRate - c.FalseRejectRate)).First();

        var response = new CalibrateThresholdResponse(
            request.Samples.Count,
            extracted.Count,
            genuineSimilarities.Count,
            impostorSimilarities.Count,
            suggested.Threshold,
            suggested.FalseAcceptRate,
            suggested.FalseRejectRate,
            curve);

        return Result.Success(response);
    }
}
