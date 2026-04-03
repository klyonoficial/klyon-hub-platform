using KlyonHubPlatform.Application.DTOs.OpenCnpj;

namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>Consulta pública de CNPJ (OpenCNPJ) para onboarding fiscal.</summary>
    public interface IOpenCnpjQueryService
    {
        /// <summary>CNPJ apenas dígitos (14). Valida situação cadastral e CNAEs bloqueados.</summary>
        Task<OpenCnpjValidationResult> ValidateForFiscalOnboardingAsync(string cnpj14, CancellationToken cancellationToken = default);
    }
}
