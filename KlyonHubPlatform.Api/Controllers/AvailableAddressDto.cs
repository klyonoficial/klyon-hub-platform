namespace KlyonHubPlatform.Api.Controllers
{
    /// <summary>
    /// DTO para endereço disponível
    /// </summary>
    public class AvailableAddressDto
    {
        public int Id { get; set; }
        public string CoworkingName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public decimal BasePrice { get; set; }
        public int Vacancies { get; set; }
        public string DisplayText { get; set; } = string.Empty;
    }
}
