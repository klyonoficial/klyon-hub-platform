namespace KlyonHubPlatform.Api.Constants
{
    public static class JsonMessage
    {
        public const string TipoInvalido = "Tipo inválido";
        public const string DadosInvalidos = "Dados inválidos.";
        public const string Empty = "";
        public const string SolicitacaoNaoEncontrada = "Solicitação não encontrada.";
        public const string EstaSolicitacaoJaFoiProcessada = "Esta solicitação já foi processada.";
        public const string SolicitacaoAlteracaoPrecosCartaoNaoEncontrada = "Solicitação de alteração de preços no cartão não encontrada.";
        public const string PrecosNoCartaoAprovados = "Preços no cartão aprovados.";
        public const string PrecosNoCartaoReprovados = "Preços no cartão reprovados.";
        public const string InformeMotivoReprovacao = "Informe o motivo da reprovação.";
        public const string SolicitacaoAlteracaoCapacidadeEnviadaComSucesso = "Solicitação de alteração de capacidade enviada com sucesso!";
        public const string JaExisteSolicitacaoAlteracaoCapacidadePendente = "Já existe uma solicitação de alteração de capacidade pendente para este endereço.";
        public const string CapacidadeSolicitadaMenorQueContratos = "A capacidade solicitada ({0}) não pode ser menor que o número de contratos ativos ({1}).";
        public const string LogNaoEncontrado = "Log não encontrado.";
        public const string PagamentoNaoEncontrado = "Pagamento não encontrado";
        public const string ReservaNaoEncontrada = "Reserva não encontrada";
        public const string IdPagamentoOuReservaNaoFornecido = "ID de pagamento ou reserva não fornecido";
        public const string PagamentoAprovadoContratoAtivado = "Pagamento aprovado! Seu contrato foi ativado.";
        public const string PagamentoSendoProcessadoAtualizePagina = "Pagamento está sendo processado. Atualize a página em instantes.";
        public const string PagamentoAprovadoCreditosCreditados = "Pagamento aprovado! Créditos creditados em sua conta.";
        public const string PagamentoEmProcessamentoSeraNotificado = "Pagamento em processamento. Você será notificado quando for confirmado.";
        public const string NotaFiscalNaoEncontrada = "Nota fiscal não encontrada";
        public const string CoworkingNaoEncontrado = "Coworking não encontrado.";
        public const string EnderecoNaoEncontradoOuSemPermissao = "Endereço não encontrado ou você não tem permissão para editá-lo.";
        public const string ServicoNaoEncontrado = "Serviço não encontrado.";
        public const string ServicoNaoDisponivelMenusDesabilitados = "Este serviço não está disponível quando os menus de coworking estão desabilitados.";
        public const string PercentagemEntreZeroECem = "A percentagem deve estar entre 0 e 100.";
        public const string ServicoParceiroNaoEncontrado = "Serviço do parceiro não encontrado.";
        public const string ServicoJaCadastradoParaParceiro = "Este serviço já está cadastrado para este parceiro.";
        public const string ServicoSalvoComSucesso = "Serviço salvo com sucesso!";
        public const string ServicoRemovidoComSucesso = "Serviço removido com sucesso!";
        public const string PerfilAtualizadoComSucesso = "Perfil atualizado com sucesso!";
        public const string ProdutosAtualizadosComSucesso = "Produtos atualizados com sucesso!";
        public const string DocumentoNaoInformado = "Documento não informado.";
        public const string TipoDeveSerCpfOuCnpj = "Tipo deve ser CPF ou CNPJ.";
        public const string InformeCpfValido11Digitos = "Informe um CPF válido (11 dígitos).";
        public const string InformeCnpjValido14Digitos = "Informe um CNPJ válido (14 dígitos).";
        public const string CpfJaCadastradoNoSistema = "O CPF {0} já está cadastrado no sistema.";
        public const string CnpjJaCadastradoNoSistema = "O CNPJ {0} já está cadastrado no sistema.";
    }
}