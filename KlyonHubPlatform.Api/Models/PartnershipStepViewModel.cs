namespace KlyonHubPlatform.Api.Models
{
    public sealed record PartnershipStepViewModel(
        string Key,
        string Label,
        bool Done,
        bool Pending,
        string Action,
        string Controller);
}
