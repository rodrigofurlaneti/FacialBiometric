using FacialBiometric.Domain.Entities;
using FacialBiometric.Domain.Repositories;
using FacialBiometric.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacialBiometric.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<User>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<User>> GetActiveWithFaceEmbeddingAsync(CancellationToken cancellationToken = default)
        => await context.Users
            .AsNoTracking()
            .Where(x => x.IsActive && x.FaceEmbedding != null)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        => await context.Users.AddAsync(user, cancellationToken);
}
