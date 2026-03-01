using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    public class ClientRegistrationDocument
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public ClientRegistrationDocumentType Type { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public virtual Client Client { get; set; } = null!;
    }
}
