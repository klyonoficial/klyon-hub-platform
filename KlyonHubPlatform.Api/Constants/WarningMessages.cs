namespace KlyonHubPlatform.Api.Constants
{
    /// <summary>
    /// Mensagens de aviso exibidas via TempData[TempDataKeys.Warning].
    /// Centraliza textos para evitar strings mágicas e facilitar manutenção e consistência.
    /// </summary>
    public static class WarningMessages
    {
        // ----- Coworking -----
        public static string CoworkingApprovedWithoutFiscalAddressPercentage(string coworkingName) =>
            $"Atenção: O coworking '{coworkingName}' foi aprovado, mas não possui percentagem configurada para o serviço 'Ofertar Endereço Fiscal'. Configure a percentagem para que os endereços fiquem disponíveis para compra.";
        public const string CoworkingRegisteredConfigureOperatingHours = "Coworking cadastrado, mas é necessário configurar os horários de funcionamento no perfil para ativar reservas.";
        public const string OperatingHoursRequiredConfigureTab = "É obrigatório configurar os horários de funcionamento. Configure na aba 'Horários de Funcionamento'.";

        // ----- Contrato / Pagamento -----
        public static string ContractCreatedPixNotGenerated(string message) =>
            $"Contrato criado, mas o código PIX não pôde ser gerado automaticamente: {message}. O código será gerado quando o cliente acessar a página de pagamento.";
        public const string PaymentSystemNotConfiguredConfigureAsaas = "Atenção: Sistema de pagamento não configurado. Configure o Asaas nas Configurações do Sistema para gerar códigos de pagamento.";
        public const string PaymentSystemNotConfiguredConfigureAsaasShort = "Sistema de pagamento não configurado. Configure o Asaas nas Configurações do Sistema para gerar códigos de pagamento.";
        public const string CouldNotGeneratePaymentCodePixOrCheckAsaas = "Aviso: Não foi possível gerar o código de pagamento automaticamente. Clique em \"Gerar Código PIX\" abaixo ou verifique a API Key Asaas nas Configurações do Sistema.";

        // ----- Cliente / Email -----
        public static string ClientRegisteredEmailSendFailed(string message) =>
            $"Cliente cadastrado com sucesso, mas houve um erro ao enviar o email: {message}";

        // ----- Nota fiscal -----
        public static string InvoiceSentValidationIssues(string autoValidationMessage) =>
            $"Nota fiscal enviada, mas a validação automática encontrou problemas: {autoValidationMessage}. O administrador irá analisar.";

        // ----- Genérico -----
        /// <summary>Aviso genérico com mensagem customizada.</summary>
        public static string GenericWarning(string message) => $"Atenção: {message}";
    }
}
