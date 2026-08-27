namespace KlyonHubPlatform.Api.Constants
{
    public static class SuccessMessage
    {
        public const string TempDataKey = "Success";
        public const string UserUpdated = "Usuário atualizado com sucesso!";
        public const string EmailVerified = "E-mail verificado com sucesso!";
        public const string PasswordReset = "Senha resetada com sucesso!";
        public const string LoggedOut = "Você deslogou com sucesso.";
        public const string EmailVerifiedLogin = "Email verificado com sucesso! Faça login para continuar.";
        public const string PasswordChanged = "Senha alterada com sucesso!";
        public static string CoworkingRegistrationApproved(string coworkingName) =>
            $"Cadastro de '{coworkingName}' aprovado. O espaço já está disponível no marketplace.";
        public const string CoworkingRegistrationRejected = "Cadastro reprovado. O coworking foi notificado e poderá reenviar após os ajustes.";
        public const string DocumentApproved = "Documento aprovado.";
        public const string DocumentRejected = "Documento rejeitado. O coworking verá o motivo no perfil.";
        public static string FiscalCapacityApproved(int capacity) =>
            $"Capacidade fiscal aprovada. Nova capacidade: {capacity}";
        public const string CapacityChangeRejected = "Alteração de capacidade reprovada. O parceiro foi notificado.";
        public static string MaxContractsApproved(int quantity) =>
            $"Quantidade máxima de contratos aprovada. Nova quantidade: {quantity}";
        public const string MaxQuantityChangeRejected = "Alteração de quantidade máxima reprovada. O parceiro foi notificado.";
        public const string CardPricesApproved = "Preços no cartão aprovados. Os valores já estão visíveis aos clientes.";
        public const string CardPricesRejected = "Preços no cartão reprovados. O parceiro foi notificado.";
        public static string AddressStatusChanged(string label) =>
            $"Status do endereço alterado para {label}.";
        public static string CoworkingUpdated(string name) =>
            $"Coworking '{name}' atualizado com sucesso.";
        public static string CoworkingStatusChanged(string name, string statusText) =>
            $"Coworking '{name}' foi {statusText} com sucesso.";
        public static string CommissionUpdated(decimal percentage) =>
            $"Comissão atualizada para {percentage}%";
        public static string LocationCreated(string city, string state) =>
            $"Localidade {city}/{state} criada com sucesso!";
        public static string LocationUpdated(string city, string state) =>
            $"Localidade {city}/{state} atualizada com sucesso!";
        public const string LocationDeleted = "Localidade excluída com sucesso!";
        public static string ConfigurationUpdated(string key) =>
            $"Configuração '{key}' atualizada com sucesso!";
        public const string FtpSettingsUpdated = "Configurações FTP atualizadas com sucesso! O sistema agora usa FTP como provedor de armazenamento.";
        public const string OracleSettingsUpdated = "Provedor de armazenamento alterado para Oracle Cloud Object Storage. Configure Oracle.OS.* em Armazenamento e use 'Usar Oracle Cloud' para ativar.";
        public const string TableAddressDeclarationsCreated = "Tabela AddressDeclarations criada com sucesso!";
        public static string MigrationsExecuted(string list) =>
            $"Migrations executadas com sucesso: {list}";
        public const string TableCoworkingOperatingHoursCreated = "Tabela CoworkingOperatingHours criada com sucesso. Os horários de funcionamento passam a ser persistidos ao salvar no perfil do parceiro.";
        public const string ColumnD4SignDocumentUuidCreated = "Coluna Documents.D4SignDocumentUuid criada com sucesso. O erro em PaymentDetails/contratos deve estar resolvido.";
        public const string ColumnMustChangePasswordCreated = "Coluna Users.MustChangePassword criada com sucesso. O erro no login deve estar resolvido.";
        public static string IndexesCreated(int count, string joined) =>
            $"{count} índice(s) criado(s) para desempenho: {joined}";
        public static string ColumnsIndexFkAdded(string joined) =>
            "Colunas/índice/FK adicionados: " + joined;
        public static string CreditPurchasesColumnsAdded(string joined) =>
            "Colunas de CreditPurchases adicionadas: " + joined;
        public const string TableInterestsCreated = "Tabela Interests criada com sucesso!";
        public const string TableCoworkingProductsCreated = "Tabela CoworkingProducts criada com sucesso!";
        public const string SqlQueryExecuted = "Query executada com sucesso!";
        public const string TableCoworkingRatingsCreated = "Tabela CoworkingRatings criada com sucesso!";
        public const string CreditTablesCreated = "Tabelas de créditos criadas com sucesso!";
        public const string CreditPlansInitialized = "Planos de créditos inicializados com sucesso!";
        public const string RoomConfigColumnsAdded = "Colunas de configuração de salas adicionadas com sucesso na tabela CoworkingAddresses!";
        public const string RoomTypeTotalValueColumnsAdded = "Colunas RoomType e TotalValue adicionadas com sucesso na tabela Reservations!";
        public const string ServiceSaved = "Serviço salvo com sucesso!";
        public const string ServiceRemoved = "Serviço removido com sucesso!";
        public static string ServiceRemovalApproved(string serviceName, string coworkingName) =>
            $"Remoção do serviço '{serviceName}' do parceiro '{coworkingName}' aprovada e executada com sucesso!";
        public static string ServiceRemovalRejected(string serviceName, string coworkingName) =>
            $"Solicitação de remoção do serviço '{serviceName}' do parceiro '{coworkingName}' rejeitada.";
        public static string AsaasSandboxPrepared(string apiKeyConfigKey) =>
            "✅ Configurações do Asaas (Sandbox) preparadas.<br/>" +
            $"<strong>Próximo passo:</strong> Edite <strong>{apiKeyConfigKey}</strong> e cole o <strong>Token</strong> (Chave de API que começa com $aact_), obtido em Integrações → Chaves de API no <a href=\"https://sandbox.asaas.com\" target=\"_blank\">painel Asaas</a>.";
        public static string AsaasSandboxModeEnabled(string apiKeyConfigKey) =>
            "✅ Ambiente sandbox do Asaas ativado.<br/>" +
            $"A <strong>{apiKeyConfigKey}</strong> existente foi <strong>preservada</strong>. Se precisar trocar para uma chave de sandbox, edite manualmente a configuração.";
        public static string AsaasConnectionValidated(string environment, int statusCode) =>
            $"✅ Conexão com Asaas ({environment}) validada com sucesso!<br/><strong>Retorno {statusCode}</strong>: API Key configurada e funcionando.";
        public static string AsaasReturnOk(int statusCode, string message) =>
            $"✅ <strong>Retorno {statusCode}</strong>: {message}";
        public const string PaymentApproved = "Pagamento aprovado com sucesso!";
        public const string PaymentRejected = "Pagamento reprovado. O status foi alterado para 'Aguardando Pagamento'.";
        public const string SefazEsConfigSaved = "Configuração da SEFAZ ES salva com sucesso!";
        public const string ImportedInvoiceApproved = "NF importada aprovada com sucesso.";
        public const string ImportedInvoiceRejected = "NF importada rejeitada. O parceiro pode enviar uma nova NF.";
        public const string InvoiceCancelled = "Nota fiscal cancelada com sucesso!";
        public static string CommissionInvoiceEmitted(string accessKey) =>
            $"NF de comissão emitida com sucesso! Chave de acesso: {accessKey}";
        public const string CommissionInvoiceImported = "NF de comissão importada com sucesso!";
        public static string TransferApproved(decimal amount) =>
            $"Repasse de R$ {amount:N2} aprovado. Status: Aguardando NF. A transferência está agendada para o último dia do mês subsequente.";
        public static string ClientRegisteredContractCreated(string contractNumber) =>
            $"Cliente cadastrado e contrato criado com sucesso! Contrato #{contractNumber}";
        public const string InvoiceApprovedTransferScheduled = "Nota fiscal aprovada com sucesso! A transferência será agendada automaticamente.";
        public const string InvoiceRejectedPartnerNotified = "Nota fiscal rejeitada. O parceiro será notificado para enviar uma nova NF.";
        public const string TransferExecuted = "Transferência executada com sucesso!";
        public static string MonthClosed(int month, int year) =>
            $"Mês {month}/{year} fechado com sucesso!";
        public const string InvoiceGeneratedAutomatically = "Nota fiscal gerada automaticamente!";
        public const string CoworkingRegistered = "Coworking cadastrado com sucesso!";
        public const string AddressRegistered = "Endereço cadastrado com sucesso!";
        public const string AddressUpdated = "Endereço atualizado com sucesso!";
        public const string CardPriceChangeRequestSent = "Solicitação de alteração de preços enviada com sucesso! Os valores só serão exibidos aos clientes após aprovação administrativa.";
        public const string PromoCodeCreated = "Cupom criado com sucesso.";
        public const string PromoCodeDeactivated = "Cupom desativado com sucesso.";
        public const string MaxQuantityRequestSent = "Solicitação de alteração de quantidade máxima enviada com sucesso! Aguardando aprovação administrativa.";
        public const string ReservationStatusUpdated = "Status da reserva atualizado com sucesso!";
        public const string ReservationCompletedCreditsTransferred = "Reserva já concluída e créditos repassados.";
        public const string AttendanceConfirmed = "Comparecimento confirmado. Após a confirmação do cliente, o pagamento será processado automaticamente.";
        public static string PayoutRequested(decimal amount) =>
            $"Repasse de R$ {amount:N2} solicitado com sucesso! O status do repasse pode ser acompanhado na tela de Meus Ganhos.";
        public const string ProfileUpdated = "Perfil atualizado com sucesso!";
        public const string ProductsUpdated = "Produtos atualizados com sucesso!";
        public const string RoomAdded = "Sala adicionada com sucesso.";
        public const string RoomUpdated = "Sala atualizada com sucesso.";
        public const string RoomDeleted = "Sala excluída.";
        public static string PaymentApprovedContractActivated(bool withDeclaration) =>
            "Pagamento aprovado e contrato ativado com sucesso!" + (withDeclaration ? " Declaração gerada automaticamente." : "");
        public const string PaymentRejectedClientNotified = "Pagamento reprovado. O cliente foi notificado.";
        public static string PhotosAdded(int count) =>
            $"{count} foto(s) adicionada(s) com sucesso!";
        public const string MainPhotoUpdated = "Foto principal atualizada com sucesso!";
        public const string PhotoDeleted = "Foto excluída com sucesso!";
        public const string PartnerAdded = "Sócio adicionado com sucesso!";
        public const string PartnerUpdated = "Sócio atualizado com sucesso!";
        public const string PartnerDeleted = "Sócio excluído com sucesso!";
        public const string RegistrationResubmitted = "Cadastro reenviado para análise. Aguarde a aprovação do administrador.";
        public const string OperatingHoursSaved = "Horários de funcionamento salvos com sucesso!";
        public const string BlockedDateAdded = "Data bloqueada adicionada com sucesso!";
        public const string BlockedDateRemoved = "Data bloqueada removida com sucesso!";
        public static string ServiceAdded(string serviceName) =>
            $"Serviço '{serviceName}' adicionado com sucesso! A percentagem será definida em contrato pelo administrador.";
        public static string ServiceRemovalRequestSent(string serviceName) =>
            $"Solicitação de remoção do serviço '{serviceName}' enviada com sucesso! Aguardando aprovação do administrador.";
        public static string ServiceInvoiceEmitted(string accessKey) =>
            $"NF de serviço emitida com sucesso! Chave de acesso: {accessKey}";
        public const string InvoiceImportedPendingValidation = "NF importada com sucesso! Aguardando validação do administrador.";
        public const string SefazConfigSaved = "Configuração SEFAZ salva com sucesso!";
        public const string InvoiceSentValidationOk = "Nota fiscal enviada com sucesso! Validação automática OK. Aguardando aprovação do administrador.";
        public const string InvoiceSentPendingValidation = "Nota fiscal enviada com sucesso! Aguardando validação do administrador.";
        public const string RegistrationCompleted = "Cadastro realizado com sucesso!";
        public const string ClientUpdated = "Cliente atualizado com sucesso!";
        public const string ContractCreated = "Contrato criado com sucesso!";
        public const string CancellationRequestSent = "Solicitação de cancelamento enviada com sucesso! Aguarde a aprovação administrativa.";
        public const string PaymentMethodUpdated = "Método de pagamento atualizado com sucesso!";
        public const string PixCodeGenerated = "Código PIX gerado com sucesso!";
        public const string CardAdded = "Cartão adicionado com sucesso!";
        public const string CardRemoved = "Cartão removido com sucesso.";
        public const string CardSetAsDefault = "Cartão definido como padrão.";
        public const string CardNicknameUpdated = "Apelido do cartão atualizado com sucesso.";
        public static string DeclarationGenerated(string declarationNumber) =>
            $"Declaração gerada com sucesso! Número: {declarationNumber}";
        public static string ContractGenerated(string contractNumber) =>
            $"Contrato gerado com sucesso! Número: {contractNumber}";
        public static string SingleReservationConfirmed(DateTime date) =>
            $"Reserva confirmada para {date:dd/MM/yyyy}!";
        public static string MultipleReservationsConfirmed(int count) =>
            $"{count} reservas confirmadas com sucesso!";
        public const string ReservationAlreadyCompleted = "Reserva já concluída.";
        public const string ConfirmationRecorded = "Confirmação registrada. Após o coworking confirmar seu comparecimento, o pagamento será processado.";
        public const string InterestRegistered = "Seu interesse foi registrado com sucesso! Entraremos em contato em breve.";
        public static string Generic(string message) => message;
    }
}