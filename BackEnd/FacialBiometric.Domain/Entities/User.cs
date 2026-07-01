using FacialBiometric.Domain.Primitives;

namespace FacialBiometric.Domain.Entities;

/// <summary>
/// Usuário cadastrado no FacialBiometricDB — independente do usuário de
/// autenticação do OpenFinancialExchange (sem FK entre bancos). Só guarda
/// o necessário para o cadastro biométrico: nome completo + foto/embedding.
/// A extração/comparação do rosto em si é feita por um provedor externo
/// (ver <see cref="Biometrics.IFacialBiometricProvider"/>), ainda não
/// implementado.
/// </summary>
public sealed class User : AggregateRoot
{
    public string FullName { get; private set; } = null!;

    public string? PhotoPath { get; private set; }
    public string? FaceEmbedding { get; private set; }
    public DateTime? PhotoRegisteredAt { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Verdadeiro quando já existe foto/embedding cadastrados para o usuário.</summary>
    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPath) && PhotoRegisteredAt.HasValue;

    private User() : base(0)
    {
        // EF Core
    }

    private User(string fullName) : base(0)
    {
        FullName = fullName;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static Result<User> Create(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result.Failure<User>(
                new Error("User.EmptyFullName", "Nome completo é obrigatório."));
        }

        if (fullName.Trim().Length < 3)
        {
            return Result.Failure<User>(
                new Error("User.InvalidFullName", "Nome completo deve ter ao menos 3 caracteres."));
        }

        return Result.Success(new User(fullName.Trim()));
    }

    /// <summary>
    /// Grava o resultado do cadastro biométrico (chamado depois que a foto
    /// foi salva no storage e o embedding foi extraído pelo provedor).
    /// </summary>
    public Result RegisterPhoto(string photoPath, string? faceEmbedding)
    {
        if (string.IsNullOrWhiteSpace(photoPath))
        {
            return Result.Failure(
                new Error("User.EmptyPhotoPath", "Caminho da foto é obrigatório."));
        }

        PhotoPath = photoPath;
        FaceEmbedding = faceEmbedding;
        PhotoRegisteredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success();
    }

    public Result UpdateFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result.Failure(
                new Error("User.EmptyFullName", "Nome completo é obrigatório."));
        }

        FullName = fullName.Trim();
        UpdatedAt = DateTime.UtcNow;

        return Result.Success();
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
