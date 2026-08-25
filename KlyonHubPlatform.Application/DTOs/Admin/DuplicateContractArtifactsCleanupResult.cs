namespace KlyonHubPlatform.Application.DTOs.Admin
{
    /// <summary>Resultado da limpeza one-shot de documentos/histórico/declarações triplicados por contrato.</summary>
    public class DuplicateContractArtifactsCleanupResult
    {
        public int DocumentsRemoved { get; set; }
        public int HistoryEntriesRemoved { get; set; }
        public int DeclarationsRemoved { get; set; }
        public List<int> AffectedContractIds { get; set; } = new();
        public List<int> RemovedDocumentIds { get; set; } = new();
        public List<int> RemovedHistoryIds { get; set; } = new();
        public List<int> RemovedDeclarationIds { get; set; } = new();
        public bool DryRun { get; set; }

        public int TotalRemoved => DocumentsRemoved + HistoryEntriesRemoved + DeclarationsRemoved;

        public string Summary =>
            $"Documentos={DocumentsRemoved}, Histórico={HistoryEntriesRemoved}, Declarações={DeclarationsRemoved}, Contratos={AffectedContractIds.Count}"
            + (DryRun ? " (dry-run)" : "");
    }
}
