namespace KlyonHubPlatform.Api.Constants
{
    public static class InfoMessages
    {
        public const string CompleteRegistration = "Complete seu cadastro para continuar.";
        public const string CompleteRegistrationWithFields = "Complete os dados do contratante (nome ou razão social, CPF/CNPJ, endereço completo, telefone e e-mail) em uma única tela para continuar.";
        public const string CompleteRegistrationWithFieldsShort = "Complete os dados do contratante (nome ou razão social, CPF/CNPJ e endereço) para continuar.";
        public const string CompleteRegistrationForFiscalAddress = "Complete os dados do contratante para continuar com a contratação do endereço fiscal.";
        public const string CompleteRegistrationForContract = "Informe CPF ou CNPJ e telefone na tela de cadastro para continuar com a contratação.";
        public const string CompleteContractPartyData = "Preencha os dados do contratante em uma única tela. Esses dados vão para o contrato e a declaração.";
        public const string SystemNoLongerUsesCredits = "O sistema não utiliza mais o conceito de créditos. Todas as transações são realizadas diretamente em R$.";
        public const string TableAddressDeclarationsAlreadyExists = "A tabela AddressDeclarations já existe no banco de dados.";
        public const string NoPendingMigrations = "Não há migrations pendentes. O banco já está atualizado.";
        public const string MigrationMarkedAsApplied = "Migration marcada como aplicada (tabela já existia). Tente executar novamente.";
        public const string TableCoworkingOperatingHoursAlreadyExists = "A tabela CoworkingOperatingHours já existe. Nenhuma alteração feita.";
        public const string ColumnD4SignDocumentUuidAlreadyExists = "A coluna Documents.D4SignDocumentUuid já existe. Nenhuma alteração feita.";
        public const string NoTableCleaned = "Nenhuma tabela foi limpa. Verifique se as tabelas existem no banco de dados.";
        public const string ColumnMustChangePasswordAlreadyExists = "A coluna Users.MustChangePassword já existe. Nenhuma alteração feita.";
        public const string AllIndexesAlreadyExist = "Todos os índices já existem. Nenhuma alteração necessária.";
        public const string ColumnsAlreadyExist = "As colunas já existem. Nenhuma alteração necessária.";
        public const string CreditPurchasesColumnsAlreadyExist = "As colunas de CreditPurchases já existem. Nenhuma alteração necessária.";
        public const string TableInterestsAlreadyExists = "A tabela Interests já existe no banco de dados.";
        public const string TableCoworkingProductsAlreadyExists = "A tabela CoworkingProducts já existe no banco de dados.";
        public const string TableCoworkingRatingsAlreadyExists = "A tabela CoworkingRatings já existe no banco de dados.";
        public const string CreditPlansAlreadyInitialized = "Os planos de créditos já foram inicializados.";
        public const string ServiceInvoicesEmittedByPartnerAtSefaz = "Notas Fiscais de Serviço devem ser emitidas pelo parceiro diretamente na SEFAZ.";
        public static string AlreadyHasCoworking(string coworkingName) =>
            $"Você já possui um coworking cadastrado: {coworkingName}. Redirecionando para o perfil...";
        public const string ServiceRemovalRequestAlreadyPending = "Já existe uma solicitação de remoção pendente para este serviço. Aguarde a aprovação do administrador.";
        public static string Generic(string message) => message;
    }
}