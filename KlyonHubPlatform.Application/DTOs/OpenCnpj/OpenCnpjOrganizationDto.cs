using System.Text.Json.Serialization;

namespace KlyonHubPlatform.Application.DTOs.OpenCnpj
{
    /// <summary>Resposta da API https://api.opencnpj.org/{cnpj}</summary>
    public class OpenCnpjOrganizationDto
    {
        [JsonPropertyName("cnpj")]
        public string? Cnpj { get; set; }

        [JsonPropertyName("razao_social")]
        public string? RazaoSocial { get; set; }

        [JsonPropertyName("nome_fantasia")]
        public string? NomeFantasia { get; set; }

        [JsonPropertyName("situacao_cadastral")]
        public string? SituacaoCadastral { get; set; }

        [JsonPropertyName("cnae_principal")]
        public string? CnaePrincipal { get; set; }

        [JsonPropertyName("cnaes_secundarios")]
        public List<string>? CnaesSecundarios { get; set; }
    }
}
