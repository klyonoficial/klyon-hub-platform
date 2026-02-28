namespace KlyonHubPlatform.Domain.Entities
{
    public class CorrespondencePhoto
    {
        public int Id { get; set; }
        public int CorrespondenceId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public virtual Correspondence Correspondence { get; set; } = null!;
    }
}
