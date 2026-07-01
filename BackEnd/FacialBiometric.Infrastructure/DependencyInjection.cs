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

        services.Configure<PhotoStorageOptions>(
            configuration.GetSection(PhotoStorageOptions.SectionName));

        return services;
    }
}
