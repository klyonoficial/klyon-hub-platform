using KlyonHubPlatform.Domain.Core.Constants;
namespace KlyonHubPlatform.Domain.Entities
{
    public class FiscalNoteScriptExecution
    {
        public int Id { get; set; }
        public DateTime ExecutedAt { get; set; }
        public int? ExecutedByUserId { get; set; }
        public string ExecutedByUserName { get; set; } = string.Empty;
        public string NoteType { get; set; } = FiscalNoteType.Default;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int NotesCreated { get; set; }
        public int NotesEmitted { get; set; }
        public int ErrorsCount { get; set; }
        public int UnsupportedStatesCount { get; set; }
        public string? ErrorDetails { get; set; }
        public virtual User? ExecutedByUser { get; set; }
    }
}