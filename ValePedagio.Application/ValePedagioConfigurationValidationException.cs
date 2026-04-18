namespace ValePedagio.Application;

/// <summary>
/// Falha de validação de configuração do provedor (credenciais incompletas ou inválidas).
/// </summary>
public sealed class ValePedagioConfigurationValidationException : Exception
{
    public ValePedagioConfigurationValidationException(string message)
        : base(message)
    {
    }
}
