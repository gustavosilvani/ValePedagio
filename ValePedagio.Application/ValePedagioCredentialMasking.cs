namespace ValePedagio.Application;

/// <summary>
/// Máscara de segredos na leitura de configuração e mescla parcial no PUT (placeholder = não alterar).
/// </summary>
public static class ValePedagioCredentialMasking
{
    public const string MaskPlaceholder = "********";

    public static bool IsMaskedSentinel(string? value) =>
        !string.IsNullOrEmpty(value) && string.Equals(value.Trim(), MaskPlaceholder, StringComparison.Ordinal);

    public static bool IsSensitiveKey(string key) =>
        SensitiveKeys.Contains(key.Trim());

    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "token",
        "apiKey",
        "secret",
        "integratorHash"
    };

    public static Dictionary<string, string> MaskForDisplay(IReadOnlyDictionary<string, string> credentials)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in credentials)
        {
            var v = kv.Value ?? string.Empty;
            if (!string.IsNullOrEmpty(v) && IsSensitiveKey(kv.Key))
            {
                result[kv.Key] = MaskPlaceholder;
            }
            else
            {
                result[kv.Key] = v;
            }
        }

        return result;
    }

    public static void MergeInto(IDictionary<string, string> stored, Dictionary<string, string>? incoming)
    {
        if (incoming is null)
        {
            return;
        }

        foreach (var pair in incoming)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            var key = pair.Key.Trim();
            var val = pair.Value?.Trim() ?? string.Empty;
            if (IsMaskedSentinel(val))
            {
                continue;
            }

            if (string.IsNullOrEmpty(val))
            {
                stored.Remove(key);
            }
            else
            {
                stored[key] = val;
            }
        }
    }
}
