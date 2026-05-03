using BlazorClaw.Core.Utils;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BlazorClaw.Core.Security.Vault;

public class SystemJsonVaultProvider(
    ILogger<SystemJsonVaultProvider> logger)
{
    private readonly string _masterKey = "MASTERKEY";

    private string GetFilePath()
    {
        return Path.Combine("vault", "system_vault.json");
    }

    public async IAsyncEnumerable<IVaultKey> GetKeysAsync(string? searchQuery = null)
    {
        var data = await ReadAsync();
        var query = searchQuery?.Trim();

        if (data != null)
            foreach (var item in data)
            {
                if (!string.IsNullOrWhiteSpace(query)
                    && !item.Value.Title.Contains(query, StringComparison.InvariantCultureIgnoreCase)
                    && !item.Value.Notes.Contains(query, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                yield return new VaultKey() { Key = item.Key, Title = item.Value.Title };
            }
    }

    public async Task<IVaultEntry?> GetSecretAsync(string key)
    {
        var data = (await ReadAsync()) ?? [];
        return (data != null && data.TryGetValue(key, out var val)) ? val : null;
    }

    public async Task<string> SetSecretAsync(string title, string secret, string? note = null, string? key = null)
    {
        var data = (await ReadAsync()) ?? [];
        if (string.IsNullOrWhiteSpace(key))
            key = Guid.NewGuid().ToString();
        data.TryGetValue(key, out var existing);
        existing ??= new VaultEntry() { Key = key };
        existing.Title = title;
        existing.Secret = secret;
        existing.Notes = note ?? existing.Notes;
        data[key] = existing;
        await SaveAsync(data);
        return key;
    }

    public async Task RemoveSecretAsync(string key)
    {
        var data = (await ReadAsync()) ?? [];
        if (!data.Remove(key))
            throw new KeyNotFoundException($"Vault-Eintrag '{key}' nicht gefunden.");
        await SaveAsync(data);
    }

    private async Task<Dictionary<string, VaultEntry>?> ReadAsync()
    {
        try
        {
            using var sourceStream = File.OpenRead(GetFilePath());
            using var destStream = new MemoryStream();
            await sourceStream.DecryptAsync(destStream, _masterKey, string.Empty);
            destStream.Position = 0;
            return await JsonSerializer.DeserializeAsync<Dictionary<string, VaultEntry>>(destStream);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to decrypt system vault");
            return null; // oder throw, je nach Anforderung
        }
    }

    private async Task SaveAsync(Dictionary<string, VaultEntry> data)
    {
        var filePath = GetFilePath();
        if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var userId = string.Empty;
        using var tempStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(tempStream, data);
        tempStream.Position = 0;
        using (var destStream = File.Open(GetFilePath(), FileMode.Create, FileAccess.Write))
        {
            await tempStream.EncryptAsync(destStream, _masterKey, userId);
        }
        logger.LogInformation("System-Vault saved, {Count} entries", data.Count);
    }
}
