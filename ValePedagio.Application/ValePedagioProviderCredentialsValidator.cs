using ValePedagio.Domain;

namespace ValePedagio.Application;

public static class ValePedagioProviderCredentialsValidator
{
    /// <summary>
    /// Simula o merge de credenciais sem alterar o armazenamento.
    /// </summary>
    public static Dictionary<string, string> MergePreview(IReadOnlyDictionary<string, string> stored, Dictionary<string, string>? incoming)
    {
        var dict = new Dictionary<string, string>(stored, StringComparer.OrdinalIgnoreCase);
        ValePedagioCredentialMasking.MergeInto(dict, incoming);
        return dict;
    }

    /// <summary>
    /// Garante que o dicionário já mesclado contém os campos mínimos para o provedor.
    /// </summary>
    public static void EnsureCompleteForProvider(ValePedagioProviderType provider, IReadOnlyDictionary<string, string> merged)
    {
        if (provider == ValePedagioProviderType.EFrete)
        {
            EnsureEfrete(merged);
            return;
        }

        EnsureGeneric(merged);
    }

    private static void EnsureEfrete(IReadOnlyDictionary<string, string> merged)
    {
        if (!HasNonWhitespace(merged, "integratorHash"))
        {
            throw new ValePedagioConfigurationValidationException("Informe o integratorHash do e-Frete.");
        }

        var hasUserPass = HasNonWhitespace(merged, "username") && HasNonWhitespace(merged, "password");
        var hasToken = HasNonWhitespace(merged, "token");
        if (!hasUserPass && !hasToken)
        {
            throw new ValePedagioConfigurationValidationException(
                "Informe usuário e senha ou token de autenticação do e-Frete.");
        }
    }

    private static void EnsureGeneric(IReadOnlyDictionary<string, string> merged)
    {
        if (!HasNonWhitespace(merged, "clientId"))
        {
            throw new ValePedagioConfigurationValidationException("Informe o Client ID do provedor.");
        }

        if (!HasNonWhitespace(merged, "apiKey"))
        {
            throw new ValePedagioConfigurationValidationException("Informe a API Key do provedor.");
        }

        if (!HasNonWhitespace(merged, "secret"))
        {
            throw new ValePedagioConfigurationValidationException("Informe o segredo (secret) do provedor.");
        }
    }

    private static bool HasNonWhitespace(IReadOnlyDictionary<string, string> merged, string key)
    {
        return merged.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);
    }
}
