namespace KlyonHubPlatform.Api.Controllers
{
    public class ReservationSlotDto
    {
        public string Date { get; set; } = "";
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public int Participants { get; set; } = 1;
    }
}
