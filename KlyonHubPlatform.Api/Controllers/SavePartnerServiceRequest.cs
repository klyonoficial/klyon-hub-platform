namespace KlyonHubPlatform.Api.Controllers
{
    public class SavePartnerServiceRequest
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public int ServiceId { get; set; }
        public decimal Percentage { get; set; }
    }
}
