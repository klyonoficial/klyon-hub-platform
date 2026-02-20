namespace KlyonHubPlatform.Application.Commands
{
    /// <summary>
    /// Resultado de um comando admin. O controller usa para setar TempData e decidir o redirect (ou resposta JSON para AJAX).
    /// </summary>
    public class AdminCommandResult
    {
        public bool Success { get; set; }
        /// <summary>Mensagem de erro para TempData ou resposta JSON (quando IsAjax).</summary>
        public string? ErrorMessage { get; set; }
        /// <summary>Mensagem de sucesso para TempData ou resposta JSON (quando IsAjax).</summary>
        public string? SuccessMessage { get; set; }
        /// <summary>Quando preenchido, o controller redireciona para ViewCoworking(id).</summary>
        public int? RedirectCoworkingId { get; set; }
        /// <summary>Quando true, o controller redireciona para Dashboard.</summary>
        public bool RedirectToDashboard { get; set; }
    }
}
