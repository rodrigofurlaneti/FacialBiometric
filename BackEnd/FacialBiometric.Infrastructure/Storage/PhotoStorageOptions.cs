namespace FacialBiometric.Infrastructure.Storage;

/// <summary>Bind de appsettings.json:"PhotoStorage".</summary>
public sealed class PhotoStorageOptions
{
    public const string SectionName = "PhotoStorage";

    /// <summary>Pasta raiz no servidor onde as fotos são gravadas.</summary>
    public string RootPath { get; set; } = "App_Data/UserPhotos";
}
