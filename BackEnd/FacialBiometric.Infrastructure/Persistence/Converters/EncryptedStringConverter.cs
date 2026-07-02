using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FacialBiometric.Infrastructure.Persistence.Converters;

/// <summary>
/// Criptografa/descriptografa uma coluna de texto ao gravar/ler do banco, usando a
/// Data Protection API do ASP.NET Core. Totalmente transparente para Domain/Application
/// — eles continuam lendo/escrevendo o valor em texto claro em memória; só o que fica
/// gravado na tabela é o ciphertext.
///
/// Usado no <c>Users.FaceEmbedding</c> (dado biométrico = dado sensível, LGPD/GDPR).
///
/// ⚠️ Gerência de chave: o <see cref="IDataProtectionProvider"/> é configurado em
/// <c>Infrastructure/DependencyInjection.cs</c> (<c>PersistKeysToFileSystem</c>). Perder
/// essas chaves (ex: apagar a pasta configurada) torna os embeddings já gravados
/// irrecuperáveis — os usuários afetados precisariam refazer o cadastro da foto.
/// Em produção, faça backup da pasta de chaves e considere protegê-las com um
/// certificado (<c>ProtectKeysWithCertificate</c>) e/ou persistência compartilhada
/// (ex: <c>PersistKeysToDbContext</c>, blob storage) se houver múltiplas instâncias.
/// </summary>
internal sealed class EncryptedStringConverter : ValueConverter<string?, string?>
{
    public EncryptedStringConverter(IDataProtector protector)
        : base(
            plainText => plainText == null ? null : protector.Protect(plainText),
            cipherText => cipherText == null ? null : TryUnprotect(protector, cipherText))
    {
    }

    private static string? TryUnprotect(IDataProtector protector, string cipherText)
    {
        try
        {
            return protector.Unprotect(cipherText);
        }
        catch (CryptographicException)
        {
            // Chave rotacionada/perdida, ou valor gravado antes da criptografia entrar em
            // vigor (texto claro legado). Tratamos como corrompido — a chamada seguinte
            // (ex: comparação de rosto) já lida com FaceEmbedding nulo/inválido sem quebrar.
            return null;
        }
    }
}
