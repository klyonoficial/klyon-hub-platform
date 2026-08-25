namespace KlyonHubPlatform.Application.DTOs.Contracts
{
    public class ContractTimelinePageDto
    {
        public List<object> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
    }
}
