using System.Collections.Concurrent;
using FacialBiometric.Domain.Biometrics;
using FacialBiometric.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace FacialBiometric.Infrastructure.Biometrics;

/// <summary>
/// Implementação em memória de <see cref="IFaceEmbeddingIndex"/>. Registrada como
/// Singleton — por isso resolve <see cref="IUserRepository"/> (Scoped) através de um
/// <see cref="IServiceScopeFactory"/> em vez de injetar direto no construtor.
/// </summary>
internal sealed class InMemoryFaceEmbeddingIndex(IServiceScopeFactory scopeFactory) : IFaceEmbeddingIndex
{
    private readonly ConcurrentDictionary<long, FaceEmbeddingIndexEntry> _entries = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private volatile bool _loaded;

    public async Task<IReadOnlyList<FaceEmbeddingIndexEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!_loaded)
        {
            await LoadFromDatabaseAsync(cancellationToken);
        }

        return _entries.Values.ToList();
    }

    public void Upsert(long userId, string fullName, float[] embedding)
        => _entries[userId] = new FaceEmbeddingIndexEntry(userId, fullName, embedding);

    public void Invalidate() => _loaded = false;

    private async Task LoadFromDatabaseAsync(CancellationToken cancellationToken)
    {
        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_loaded)
            {
                return; // outra thread já recarregou enquanto esperávamos o lock
            }

            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var facialBiometricProvider = scope.ServiceProvider.GetRequiredService<IFacialBiometricProvider>();

            var users = await repository.GetActiveWithFaceEmbeddingAsync(cancellationToken);

            _entries.Clear();
            foreach (var user in users)
            {
                // O filtro do repositório compara a coluna crua (nível SQL) — um registro
                // gravado ANTES da criptografia entrar em vigor tem valor não-nulo no banco,
                // mas falha ao descriptografar na leitura e chega aqui como null. Trata igual
                // a um embedding corrompido: fica de fora do índice, sem derrubar o restante.
                if (user.FaceEmbedding is null)
                {
                    continue;
                }

                var embedding = facialBiometricProvider.DecodeEmbedding(user.FaceEmbedding);
                if (embedding is not null)
                {
                    _entries[user.Id] = new FaceEmbeddingIndexEntry(user.Id, user.FullName, embedding);
                }
            }

            _loaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }
}
