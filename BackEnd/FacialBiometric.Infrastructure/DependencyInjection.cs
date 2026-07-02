using FacialBiometric.Domain.Biometrics;
using FacialBiometric.Domain.Repositories;
using FacialBiometric.Domain.Storage;
using FacialBiometric.Infrastructure.Biometrics;
using FacialBiometric.Infrastructure.Persistence;
using FacialBiometric.Infrastructure.Persistence.Repositories;
using FacialBiometric.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FacialBiometric.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Chave usada pra criptografar/descriptografar dado biométrico em repouso
        // (Users.FaceEmbedding — ver EncryptedStringConverter). Por padrão grava num
        // diretório local configurável; em produção com múltiplas instâncias, troque
        // por uma persistência compartilhada (ex: PersistKeysToDbContext, blob storage)
        // e considere ProtectKeysWithCertificate.
        var keysPath = configuration["DataProtection:KeysPath"] ?? "App_Data/DataProtection-Keys";
        services.AddDataProtection()
            .SetApplicationName("FacialBiometric")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPhotoStorageService, FileSystemPhotoStorageService>();

        // Singleton de propósito: FaceDetector/Face68LandmarksExtractor/FaceEmbedder
        // carregam sessões ONNX Runtime (caro de inicializar). Ver comentário em
        // LocalFacialBiometricProvider sobre thread-safety.
        services.AddSingleton<IFacialBiometricProvider, LocalFacialBiometricProvider>();

        // Índice em memória dos embeddings (usado na autenticação 1:N e na checagem
        // de duplicidade no cadastro) — Singleton de propósito, resolve o
        // IUserRepository (Scoped) via IServiceScopeFactory quando precisa recarregar.
        services.AddSingleton<IFaceEmbeddingIndex, InMemoryFaceEmbeddingIndex>();

        // Heurística passiva de liveness (ver disclaimer em ILivenessDetector) — stateless,
        // sem custo de inicialização, então Singleton é só conveniência.
        services.AddSingleton<ILivenessDetector, PassiveLivenessDetector>();

        services.Configure<PhotoStorageOptions>(
            configuration.GetSection(PhotoStorageOptions.SectionName));

        return services;
    }
}
