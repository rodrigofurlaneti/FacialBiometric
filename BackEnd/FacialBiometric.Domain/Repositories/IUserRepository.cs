using FacialBiometric.Domain.Entities;

namespace FacialBiometric.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<User>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Usuários ativos que já têm embedding facial cadastrado (usado para checar duplicidade no cadastro).</summary>
    Task<IReadOnlyCollection<User>> GetActiveWithFaceEmbeddingAsync(CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
