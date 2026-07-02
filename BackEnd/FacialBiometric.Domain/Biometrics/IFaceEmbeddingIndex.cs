namespace FacialBiometric.Domain.Biometrics;

/// <summary>
/// Índice em memória dos embeddings faciais de usuários ativos, usado na
/// autenticação/identificação 1:N (<c>authenticate-face</c>) e na checagem de
/// duplicidade no cadastro. Existe pra evitar reparsear o JSON do embedding
/// (e reconsultar o banco) a cada requisição — os embeddings já decodificados
/// (<c>float[]</c>) ficam em memória, prontos pra comparação.
///
/// Implementação concreta: <c>InMemoryFaceEmbeddingIndex</c> (Infrastructure/Biometrics),
/// registrada como Singleton. Consistência: eventual — populado sob demanda a partir
/// do banco e atualizado via <see cref="Upsert"/> logo após cada novo cadastro.
/// Em ambientes com múltiplas instâncias da API, cada instância mantém seu próprio
/// índice; um cadastro feito em uma instância só aparece nas outras depois que elas
/// derem <see cref="Invalidate"/>/recarregarem (ok pra um serviço single-instance;
/// vale revisar se escalar horizontalmente).
/// </summary>
public interface IFaceEmbeddingIndex
{
    Task<IReadOnlyList<FaceEmbeddingIndexEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Adiciona/atualiza uma entrada no índice (chamado logo após um cadastro novo).</summary>
    void Upsert(long userId, string fullName, float[] embedding);

    /// <summary>Força recarregar do banco na próxima chamada a <see cref="GetAllAsync"/>.</summary>
    void Invalidate();
}

public sealed record FaceEmbeddingIndexEntry(long UserId, string FullName, float[] Embedding);
