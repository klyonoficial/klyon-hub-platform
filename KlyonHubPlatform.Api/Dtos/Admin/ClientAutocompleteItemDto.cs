namespace KlyonHubPlatform.Api.Dtos.Admin
{
    public class ClientAutocompleteItemDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? RazaoSocial { get; set; }
        public string? NomeFantasia { get; set; }
        public string? ContactName { get; set; }
        public string? Cnpj { get; set; }
        public string? Cpf { get; set; }
    }
}
