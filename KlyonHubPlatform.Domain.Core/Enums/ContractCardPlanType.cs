namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>Tipo de plano no cartão para cobrança: 1x (anual), mensal ou 12x (mensal).</summary>
    public enum ContractCardPlanType
    {
        None = 0,
        Card1x = 1,
        CardMonthly = 2,
        Card12x = 3
    }
}
