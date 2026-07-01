using FacialBiometric.Domain.Entities;
using FacialBiometric.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FacialBiometric.Infrastructure.Persistence;

/// <summary>DbContext do banco FacialBiometricDB (independente do OpenFinancialExchange).</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
        => await SaveChangesAsync(cancellationToken);
}
