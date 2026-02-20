namespace KlyonHubPlatform.Application.DTOs
{
    public class QuickClientRegistrationInput
    {
        public string ContactEmail { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string? RazaoSocial { get; set; }
        public string? NomeFantasia { get; set; }
        public string? Cnpj { get; set; }
        public string? Cpf { get; set; }
        public string? ContactPhone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public int CoworkingAddressId { get; set; }
        public decimal MonthlyValue { get; set; }
        public string PaymentMethod { get; set; } = "PIX";
    }
}
