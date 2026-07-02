namespace FacialBiometric.Domain.Biometrics;

/// <summary>
/// Checagem de vivacidade (liveness/anti-spoofing) a partir de uma única foto estática.
///
/// ⚠️ Isto é uma heurística passiva, NÃO um sistema de anti-spoofing de produção.
/// Não substitui: desafio de múltiplos frames (piscar, virar a cabeça), câmera com
/// sensor de profundidade, ou um modelo dedicado treinado pra distinguir rosto real
/// de foto/tela/máscara. Serve como uma primeira camada de sinal, com falso positivo
/// e falso negativo esperados. Ver README_FACIALBIOMETRIC_API.md para detalhes e
/// para o caminho de evolução recomendado.
///
/// Implementação concreta: <c>PassiveLivenessDetector</c> (Infrastructure/Biometrics).
/// </summary>
public interface ILivenessDetector
{
    Task<LivenessResult> AssessAsync(byte[] photoContent, CancellationToken cancellationToken = default);
}

/// <param name="IsLikelyLive">Veredito final (score acima do limiar interno do detector).</param>
/// <param name="Score">Pontuação 0.0–1.0 — quanto maior, mais provável de ser uma captura ao vivo.</param>
/// <param name="Signals">Lista de heurísticas que "pegaram suspeita" nesta foto (vazia se nenhuma).</param>
public sealed record LivenessResult(bool IsLikelyLive, double Score, IReadOnlyList<string> Signals);
