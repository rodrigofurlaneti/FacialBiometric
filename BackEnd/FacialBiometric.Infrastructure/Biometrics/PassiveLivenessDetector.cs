using FacialBiometric.Domain.Biometrics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FacialBiometric.Infrastructure.Biometrics;

/// <summary>
/// Heurística passiva de liveness (ver <see cref="ILivenessDetector"/> para o
/// disclaimer completo — isto NÃO é anti-spoofing de produção). Três sinais simples,
/// calculados sobre a própria foto enviada, sem nenhum modelo de ML dedicado:
///
/// 1. Nitidez (variância do Laplaciano) — fotos recapturadas de tela/impressão
///    tendem a perder detalhe fino e ficar mais "lisas" que uma captura direta.
/// 2. Estouro de luz / reflexo (% de pixels quase-brancos) — comum em fotos de
///    tela (brilho da tela, flash refletido no vidro/plástico).
/// 3. Variedade de cor (paleta quantizada) — recompressão/reprodução por tela às
///    vezes reduz a variedade de cor efetiva da imagem.
///
/// Cada sinal é um voto binário (passou/não passou); o score final é a fração de
/// sinais que passaram. Os limiares abaixo são pontos de partida arbitrários — assim
/// como o <c>MatchThreshold</c> do <see cref="LocalFacialBiometricProvider"/>, precisam
/// ser calibrados com fotos reais (e, idealmente, com tentativas reais de spoofing)
/// antes de confiar neles em produção.
/// </summary>
internal sealed class PassiveLivenessDetector : ILivenessDetector
{
    /// <summary>Fração mínima de sinais "aprovados" para considerar a foto provavelmente ao vivo.</summary>
    private const double LiveScoreThreshold = 0.5;

    private const double MinSharpnessVariance = 50.0;
    private const double MaxGlarePixelRatio = 0.015;
    private const int MinDistinctColorBuckets = 40;

    public Task<LivenessResult> AssessAsync(byte[] photoContent, CancellationToken cancellationToken = default)
    {
        using var image = Image.Load<Rgb24>(photoContent);

        // Reduz a imagem — as heurísticas não precisam de resolução total e ficam mais rápidas.
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(256, 0),
            Mode = ResizeMode.Max
        }));

        var sharpness = ComputeSharpnessVariance(image);
        var glareRatio = ComputeGlarePixelRatio(image);
        var distinctColorBuckets = ComputeDistinctColorBuckets(image);

        var signals = new List<string>();
        var passed = 0;

        if (sharpness >= MinSharpnessVariance)
        {
            passed++;
        }
        else
        {
            signals.Add("baixa_nitidez");
        }

        if (glareRatio <= MaxGlarePixelRatio)
        {
            passed++;
        }
        else
        {
            signals.Add("possivel_reflexo_de_tela");
        }

        if (distinctColorBuckets >= MinDistinctColorBuckets)
        {
            passed++;
        }
        else
        {
            signals.Add("paleta_de_cor_suspeita");
        }

        var score = passed / 3.0;
        var result = new LivenessResult(score >= LiveScoreThreshold, score, signals);

        return Task.FromResult(result);
    }

    /// <summary>Variância do Laplaciano (aproximação de nitidez/quantidade de detalhe fino).</summary>
    private static double ComputeSharpnessVariance(Image<Rgb24> image)
    {
        var gray = new double[image.Height, image.Width];

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < accessor.Width; x++)
                {
                    var p = row[x];
                    gray[y, x] = (0.299 * p.R) + (0.587 * p.G) + (0.114 * p.B);
                }
            }
        });

        if (image.Height < 3 || image.Width < 3)
        {
            return 0;
        }

        double sum = 0, sumSq = 0;
        var count = 0;

        for (var y = 1; y < image.Height - 1; y++)
        {
            for (var x = 1; x < image.Width - 1; x++)
            {
                var laplacian = (-4 * gray[y, x]) + gray[y - 1, x] + gray[y + 1, x] + gray[y, x - 1] + gray[y, x + 1];
                sum += laplacian;
                sumSq += laplacian * laplacian;
                count++;
            }
        }

        if (count == 0)
        {
            return 0;
        }

        var mean = sum / count;
        return (sumSq / count) - (mean * mean);
    }

    /// <summary>Fração de pixels "quase-brancos" nos 3 canais — indício de estouro de luz/reflexo.</summary>
    private static double ComputeGlarePixelRatio(Image<Rgb24> image)
    {
        long brightCount = 0;
        var total = (long)image.Width * image.Height;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < accessor.Width; x++)
                {
                    var p = row[x];
                    if (p.R >= 245 && p.G >= 245 && p.B >= 245)
                    {
                        brightCount++;
                    }
                }
            }
        });

        return total == 0 ? 0 : (double)brightCount / total;
    }

    /// <summary>Quantiza cada canal em 8 níveis (3 bits) e conta quantos dos 512 buckets aparecem.</summary>
    private static int ComputeDistinctColorBuckets(Image<Rgb24> image)
    {
        var buckets = new HashSet<int>();

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < accessor.Width; x++)
                {
                    var p = row[x];
                    var bucket = ((p.R >> 5) << 6) | ((p.G >> 5) << 3) | (p.B >> 5);
                    buckets.Add(bucket);
                }
            }
        });

        return buckets.Count;
    }
}
