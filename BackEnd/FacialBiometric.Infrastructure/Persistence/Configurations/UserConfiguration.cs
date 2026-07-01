using FacialBiometric.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FacialBiometric.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();

        builder.Property(x => x.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.PhotoPath)
            .HasMaxLength(500);

        builder.Property(x => x.FaceEmbedding)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.PhotoRegisteredAt)
            .HasColumnType("datetime2");

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        // Propriedade calculada — não mapeada
        builder.Ignore(x => x.HasPhoto);

        builder.HasIndex(x => x.Id)
            .HasDatabaseName("IX_Users_Id_PhotoPath");
    }
}
