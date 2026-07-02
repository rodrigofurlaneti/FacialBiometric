using FacialBiometric.Domain.Entities;
using FacialBiometric.Infrastructure.Persistence.Converters;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FacialBiometric.Infrastructure.Persistence.Configurations;

/// <param name="embeddingProtector">
/// Protetor dedicado (purpose string próprio) usado só pra criptografar
/// <see cref="User.FaceEmbedding"/> em repouso — ver <see cref="EncryptedStringConverter"/>.
/// </param>
internal sealed class UserConfiguration(IDataProtector embeddingProtector) : IEntityTypeConfiguration<User>
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

        // Dado biométrico = dado sensível (LGPD/GDPR) — criptografado em repouso.
        // Domain/Application continuam lendo/escrevendo o JSON em texto claro;
        // só a coluna no banco guarda o ciphertext (ver EncryptedStringConverter).
        builder.Property(x => x.FaceEmbedding)
            .HasColumnType("nvarchar(max)")
            .HasConversion(new EncryptedStringConverter(embeddingProtector));

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
