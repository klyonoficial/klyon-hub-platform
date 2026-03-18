namespace KlyonHubPlatform.Application.Constants
{
    /// <summary>
    /// Mensagens de resultado dos comandos admin (mesmo texto exibido ao usuário que em Api.Constants).
    /// Usado pela camada Application para não depender da Api.
    /// </summary>
    public static class AdminResultMessages
    {
        public const string InformReprovalReason = "Informe o motivo da reprovação.";
        public const string CapacityChangeRequestNotFound = "Solicitação de alteração de capacidade não encontrada.";
        public const string RequestAlreadyProcessed = "Esta solicitação já foi processada.";
        public const string MaxQuantityChangeRequestNotFound = "Solicitação de alteração de quantidade máxima não encontrada.";
        public const string CardPriceChangeRequestNotFound = "Solicitação de alteração de preços no cartão não encontrada.";
        public const string SolicitacaoAlteracaoPrecosCartaoNaoEncontrada = "Solicitação de alteração de preços no cartão não encontrada.";
        public const string EstaSolicitacaoJaFoiProcessada = "Esta solicitação já foi processada.";
        public const string InformeMotivoReprovacao = "Informe o motivo da reprovação.";
        public const string CapacityChangeRejected = "Alteração de capacidade reprovada. O parceiro foi notificado.";
        public const string MaxQuantityChangeRejected = "Alteração de quantidade máxima reprovada. O parceiro foi notificado.";
        public const string CardPricesRejected = "Preços no cartão reprovados. O parceiro foi notificado.";
        public const string CardPricesApproved = "Preços no cartão aprovados.";
        public const string PrecosNoCartaoAprovados = "Preços no cartão aprovados.";
        public const string PrecosNoCartaoReprovados = "Preços no cartão reprovados.";
        public static string FiscalCapacityApproved(int capacity) => $"Capacidade fiscal aprovada. Nova capacidade: {capacity}";
        public static string MaxContractsApproved(int quantity) => $"Quantidade máxima de contratos aprovada. Nova quantidade: {quantity}";
        public const string CardPricesApprovedSuccess = "Preços no cartão aprovados. Os valores já estão visíveis aos clientes.";
        public const string CoworkingNotFound = "Coworking não encontrado.";
        public const string CoworkingCannotApproveWithPendingCardPrices = "Não é possível aprovar o cadastro enquanto houver preços no cartão aguardando aprovação. Aprove ou reprove as solicitações na seção \"Preços no cartão\" acima.";
        public static string CoworkingUpdated(string name) => $"Coworking '{name}' atualizado com sucesso.";
        public static string CoworkingApproved(string name) => $"Cadastro do coworking '{name}' aprovado com sucesso.";
        public const string CoworkingRejected = "Cadastro do coworking reprovado. O parceiro foi notificado.";
        public const string DocumentApproved = "Documento aprovado com sucesso.";
        public const string DocumentRejected = "Documento reprovado. O parceiro foi notificado.";
        public const string DocumentNotFound = "Documento não encontrado.";
        public const string InformRejectionReason = "Informe o motivo da reprovação.";
    }
}
