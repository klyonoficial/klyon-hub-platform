namespace KlyonHubPlatform.Application.DTOs.OpenCnpj
{
    public sealed class OpenCnpjValidationResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public string? RazaoSocial { get; init; }
        public string? CnaePrincipal { get; init; }

        public static OpenCnpjValidationResult Ok(string? razaoSocial, string? cnaePrincipal) =>
            new() { Success = true, RazaoSocial = razaoSocial, CnaePrincipal = cnaePrincipal };

        public static OpenCnpjValidationResult Fail(string message) =>
            new() { Success = false, ErrorMessage = message };
    }
}
