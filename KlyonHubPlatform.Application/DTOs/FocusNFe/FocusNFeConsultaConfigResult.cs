namespace KlyonHubPlatform.Application.DTOs.FocusNFe;

/// <summary>
/// Resultado da obtenção de configuração para consulta de NFSe.
/// Null = nota fiscal não encontrada.
/// ApiToken null = nota encontrada mas token não configurado.
/// </summary>
public class FocusNFeConsultaConfigResult
{
    public string? ApiToken { get; set; }
    public bool IsProduction { get; set; }
}
