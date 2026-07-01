using FacialBiometric.Domain.Storage;
using Microsoft.Extensions.Options;

namespace FacialBiometric.Infrastructure.Storage;

/// <summary>
/// Grava a foto do usuário no sistema de arquivos do servidor, em
/// {RootPath}/{userId}/{guid}{extensão}. O caminho relativo retornado é
/// o que fica gravado em Users.PhotoPath.
/// </summary>
internal sealed class FileSystemPhotoStorageService(IOptions<PhotoStorageOptions> options)
    : IPhotoStorageService
{
    private readonly string _rootPath = options.Value.RootPath;

    public async Task<string> SaveAsync(
        long userId, byte[] photoContent, string fileName, CancellationToken cancellationToken = default)
    {
        var userFolder = Path.Combine(_rootPath, userId.ToString());
        Directory.CreateDirectory(userFolder);

        var extension = Path.GetExtension(fileName);
        var relativePath = Path.Combine(userId.ToString(), $"{Guid.NewGuid()}{extension}");
        var fullPath = Path.Combine(_rootPath, relativePath);

        await File.WriteAllBytesAsync(fullPath, photoContent, cancellationToken);

        return relativePath.Replace('\\', '/');
    }

    public async Task<byte[]?> ReadAsync(string photoPath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_rootPath, photoPath);
        return File.Exists(fullPath)
            ? await File.ReadAllBytesAsync(fullPath, cancellationToken)
            : null;
    }
}
