namespace KlyonHubPlatform.Application.DTOs.ClientProfile
{
    public class ClientContractPartyStatus
    {
        public bool IsComplete { get; set; }
        public IReadOnlyList<string> MissingFields { get; set; } = Array.Empty<string>();
        public string? ErrorMessage { get; set; }
    }

    public class ClientProfileSaveInput
    {
        public int UserId { get; set; }
        public string AccountEmail { get; set; } = string.Empty;
        public string? RazaoSocial { get; set; }
        public string? NomeFantasia { get; set; }
        public string? CNPJ { get; set; }
        public string? CPF { get; set; }
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? ZipCode { get; set; }
        public string? Address { get; set; }
        public string? AddressNumber { get; set; }
        public string? Neighborhood { get; set; }
        public string? Complement { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public ClientProfileLogoInput? Logo { get; set; }
        public long MaxLogoBytes { get; set; } = 5 * 1024 * 1024;
    }

    public class ClientProfileLogoInput
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = string.Empty;
    }

    public class ClientProfileSaveResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ContactName { get; set; }
        public string? DisplayName { get; set; }
        /// <summary>Aviso não-bloqueante (ex.: falha ao sincronizar o Asaas). O perfil local foi salvo.</summary>
        public string? WarningMessage { get; set; }

        public static ClientProfileSaveResult Ok(string? contactName, string displayName, string? warningMessage = null) =>
            new()
            {
                Success = true,
                ContactName = contactName,
                DisplayName = displayName,
                WarningMessage = warningMessage
            };

        public static ClientProfileSaveResult Fail(string error) =>
            new() { Success = false, ErrorMessage = error };
    }
}
