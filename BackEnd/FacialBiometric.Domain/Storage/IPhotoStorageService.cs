namespace FacialBiometric.Domain.Storage;

/// <summary>
/// Contrato para persistência física da foto do usuário.
/// Implementação concreta: sistema de arquivos do servidor
/// (<c>FileSystemPhotoStorageService</c>, em Infrastructure/Storage).
/// </summary>
public interface IPhotoStorageService
{
    /// <summary>Salva a foto e retorna o caminho relativo gravado em <c>Users.PhotoPath</c>.</summary>
    Task<string> SaveAsync(
        long userId,
        byte[] photoContent,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<byte[]?> ReadAsync(string photoPath, CancellationToken cancellationToken = default);
}
