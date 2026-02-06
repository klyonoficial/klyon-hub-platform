namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Status da nota fiscal no sistema
    /// </summary>
    public enum FiscalNoteStatus
    {
        Pendente = 0,
        Processando = 1,
        Emitida = 2,
        Importada = 3,
        Cancelada = 4,
        Erro = 5,
        Rejeitada = 6
    }
}
