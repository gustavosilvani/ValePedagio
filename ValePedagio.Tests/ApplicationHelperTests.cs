using ValePedagio.Application;
using ValePedagio.Domain;

namespace ValePedagio.Tests;

public sealed class CredentialMaskingTests
{
    [Fact]
    public void IsMaskedSentinel_DeveDetectarPlaceholder()
    {
        Assert.True(ValePedagioCredentialMasking.IsMaskedSentinel("********"));
        Assert.True(ValePedagioCredentialMasking.IsMaskedSentinel(" ******** "));
        Assert.False(ValePedagioCredentialMasking.IsMaskedSentinel("xxx"));
        Assert.False(ValePedagioCredentialMasking.IsMaskedSentinel(""));
        Assert.False(ValePedagioCredentialMasking.IsMaskedSentinel(null));
    }

    [Fact]
    public void IsSensitiveKey_DeveIdentificarChavesSensiveis()
    {
        Assert.True(ValePedagioCredentialMasking.IsSensitiveKey("password"));
        Assert.True(ValePedagioCredentialMasking.IsSensitiveKey("token"));
        Assert.True(ValePedagioCredentialMasking.IsSensitiveKey("apiKey"));
        Assert.True(ValePedagioCredentialMasking.IsSensitiveKey("secret"));
        Assert.True(ValePedagioCredentialMasking.IsSensitiveKey("integratorHash"));
        Assert.False(ValePedagioCredentialMasking.IsSensitiveKey("username"));
    }

    [Fact]
    public void MaskForDisplay_DeveOmitirSensiveisMantendoOutros()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["password"] = "abc",
            ["username"] = "john",
            ["empty"] = "",
            ["apiKey"] = "k"
        };
        var r = ValePedagioCredentialMasking.MaskForDisplay(dict);
        Assert.Equal("********", r["password"]);
        Assert.Equal("********", r["apiKey"]);
        Assert.Equal("john", r["username"]);
        Assert.Equal("", r["empty"]);
    }

    [Fact]
    public void MergeInto_NullIncoming_NaoFazNada()
    {
        var stored = new Dictionary<string, string> { ["a"] = "b" };
        ValePedagioCredentialMasking.MergeInto(stored, null);
        Assert.Equal("b", stored["a"]);
    }

    [Fact]
    public void MergeInto_PlaceholderMantemValor()
    {
        var stored = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["password"] = "real" };
        var incoming = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["password"] = "********" };
        ValePedagioCredentialMasking.MergeInto(stored, incoming);
        Assert.Equal("real", stored["password"]);
    }

    [Fact]
    public void MergeInto_VazioRemoveChave()
    {
        var stored = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["x"] = "y" };
        var incoming = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["x"] = "" };
        ValePedagioCredentialMasking.MergeInto(stored, incoming);
        Assert.False(stored.ContainsKey("x"));
    }

    [Fact]
    public void MergeInto_KeyVazioIgnora()
    {
        var stored = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var incoming = new Dictionary<string, string> { ["  "] = "v" };
        ValePedagioCredentialMasking.MergeInto(stored, incoming);
        Assert.Empty(stored);
    }
}

public sealed class CredentialsValidatorTests
{
    [Fact]
    public void EnsureCompleteForProvider_EFrete_SemHash_Lanca()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Assert.Throws<ValePedagioConfigurationValidationException>(() =>
            ValePedagioProviderCredentialsValidator.EnsureCompleteForProvider(ValePedagioProviderType.EFrete, dict));
    }

    [Fact]
    public void EnsureCompleteForProvider_EFrete_SemUserPassENemToken_Lanca()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["integratorHash"] = "h"
        };
        Assert.Throws<ValePedagioConfigurationValidationException>(() =>
            ValePedagioProviderCredentialsValidator.EnsureCompleteForProvider(ValePedagioProviderType.EFrete, dict));
    }

    [Fact]
    public void EnsureCompleteForProvider_EFrete_ComToken_Passa()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["integratorHash"] = "h",
            ["token"] = "t"
        };
        ValePedagioProviderCredentialsValidator.EnsureCompleteForProvider(ValePedagioProviderType.EFrete, dict);
    }

    [Fact]
    public void EnsureCompleteForProvider_EFrete_ComUserPass_Passa()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["integratorHash"] = "h",
            ["username"] = "u",
            ["password"] = "p"
        };
        ValePedagioProviderCredentialsValidator.EnsureCompleteForProvider(ValePedagioProviderType.EFrete, dict);
    }

    [Fact]
    public void EnsureCompleteForProvider_Generic_SemClientId_Lanca()
    {
        var dict = new Dictionary<string, string>();
        var ex = Assert.Throws<ValePedagioConfigurationValidationException>(() =>
            ValePedagioProviderCredentialsValidator.EnsureCompleteForProvider(ValePedagioProviderType.Ambipar, dict));
        Assert.Contains("Client ID", ex.Message);
    }

    [Fact]
    public void EnsureCompleteForProvider_Generic_SemApiKey_Lanca()
    {
        var dict = new Dictionary<string, string> { ["clientId"] = "c" };
        Assert.Throws<ValePedagioConfigurationValidationException>(() =>
            ValePedagioProviderCredentialsValidator.EnsureCompleteForProvider(ValePedagioProviderType.Ambipar, dict));
    }

    [Fact]
    public void EnsureCompleteForProvider_Generic_SemSecret_Lanca()
    {
        var dict = new Dictionary<string, string> { ["clientId"] = "c", ["apiKey"] = "a" };
        Assert.Throws<ValePedagioConfigurationValidationException>(() =>
            ValePedagioProviderCredentialsValidator.EnsureCompleteForProvider(ValePedagioProviderType.Ambipar, dict));
    }

    [Fact]
    public void EnsureCompleteForProvider_Generic_Completo_Passa()
    {
        var dict = new Dictionary<string, string>
        {
            ["clientId"] = "c",
            ["apiKey"] = "a",
            ["secret"] = "s"
        };
        ValePedagioProviderCredentialsValidator.EnsureCompleteForProvider(ValePedagioProviderType.Ambipar, dict);
    }

    [Fact]
    public void MergePreview_NaoMutaOriginal()
    {
        var stored = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["a"] = "1" };
        var incoming = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["b"] = "2" };
        var preview = ValePedagioProviderCredentialsValidator.MergePreview(stored, incoming);
        Assert.Equal("1", preview["a"]);
        Assert.Equal("2", preview["b"]);
        Assert.False(stored.ContainsKey("b"));
    }
}
