using FacialBiometric.Domain.Entities;
using FacialBiometric.Domain.Repositories;
using FacialBiometric.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace FacialBiometric.Infrastructure.Persistence;

/// <summary>DbContext do banco FacialBiometricDB (independente do OpenFinancialExchange).</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, IDataProtectionProvider dataProtectionProvider)
    : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Purpose string próprio (não muda mesmo que o app ganhe outros usos da Data
        // Protection API) — trocar essa string invalida os dados já criptografados.
        var embeddingProtector = dataProtectionProvider.CreateProtector("FacialBiometric.Users.FaceEmbedding.v1");

        modelBuilder.ApplyConfiguration(new UserConfiguration(embeddingProtector));
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
        => await SaveChangesAsync(cancellationToken);
}
