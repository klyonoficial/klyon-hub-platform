namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Registro de execução do script de emissão de notas fiscais (SEFAZ).
    /// Garante rastreabilidade: data, usuário e quantidade de NFs emitidas.
    /// </summary>
    public class FiscalNoteScriptExecution
    {
        public int Id { get; set; }
        /// <summary>Data/hora da execução (horário Brasil)</summary>
        public DateTime ExecutedAt { get; set; }
        /// <summary>ID do usuário que executou (opcional, pode ser null se usuário removido)</summary>
        public int? ExecutedByUserId { get; set; }
        /// <summary>Nome do usuário no momento da execução</summary>
        public string ExecutedByUserName { get; set; } = string.Empty;
        /// <summary>Tipo: Intermediacao ou Servico</summary>
        public string NoteType { get; set; } = "Intermediacao";
        /// <summary>Data inicial do período filtrado</summary>
        public DateTime StartDate { get; set; }
        /// <summary>Data final do período filtrado</summary>
        public DateTime EndDate { get; set; }
        /// <summary>Quantidade de NFs criadas (registros pendentes)</summary>
        public int NotesCreated { get; set; }
        /// <summary>Quantidade de NFs efetivamente emitidas na SEFAZ</summary>
        public int NotesEmitted { get; set; }
        /// <summary>Quantidade de erros (validação ou emissão)</summary>
        public int ErrorsCount { get; set; }
        /// <summary>Registros ignorados por estado sem integração</summary>
        public int UnsupportedStatesCount { get; set; }
        /// <summary>Detalhes dos erros (truncado se necessário)</summary>
        public string? ErrorDetails { get; set; }

        public virtual User? ExecutedByUser { get; set; }
    }
}
