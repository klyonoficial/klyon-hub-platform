namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Status do endereço do parceiro. Separado do status do parceiro (Coworking).
    /// Regras: Parceiro suspenso → não cria novos contratos. Endereço suspenso/removido →
    /// contratos ativos continuam válidos; apenas novas vendas são bloqueadas.
    /// </summary>
    public enum AddressStatus
    {
        /// <summary>Endereço ativo – aceita novos contratos (respeitando capacidade)</summary>
        Active = 0,

        /// <summary>Endereço suspenso – contratos ativos continuam válidos; bloqueia apenas novas vendas</summary>
        Suspended = 1,

        /// <summary>Endereço removido – não afeta contratos existentes; bloqueia novas vendas</summary>
        Removed = 2
    }
}
