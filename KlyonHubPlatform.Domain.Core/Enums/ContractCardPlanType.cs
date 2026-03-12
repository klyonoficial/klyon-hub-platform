namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>Tipo de plano para cobrança: cartão 1x (anual), mensal, 12x ou PIX recorrente anual.</summary>
    public enum ContractCardPlanType
    {
        None = 0,
        Card1x = 1,
        CardMonthly = 2,
        Card12x = 3,
        /// <summary>PIX Automático com recorrência anual (débito automático todo ano).</summary>
        PixAnnual = 4
    }
}
