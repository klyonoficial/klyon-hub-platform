namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>Tipo de plano para cobrança: cartão 1x (anual), mensal, 12x ou PIX Automático (mensal/anual).</summary>
    public enum ContractCardPlanType
    {
        None = 0,
        Card1x = 1,
        CardMonthly = 2,
        Card12x = 3,
        /// <summary>PIX Automático Asaas com frequência ANNUALLY (não confundir com Pix Recorrente).</summary>
        PixAnnual = 4,
        /// <summary>PIX Automático Asaas com frequência MONTHLY (não confundir com Pix Recorrente).</summary>
        PixMonthly = 5
    }

    public static class ContractCardPlanTypeExtensions
    {
        /// <summary>PIX Automático (mensal ou anual) — autorização Asaas, não PIX único.</summary>
        public static bool IsPixAutomatic(this ContractCardPlanType? plan) =>
            plan == ContractCardPlanType.PixAnnual || plan == ContractCardPlanType.PixMonthly;

        public static bool IsPixAutomatic(this ContractCardPlanType plan) =>
            plan == ContractCardPlanType.PixAnnual || plan == ContractCardPlanType.PixMonthly;

        /// <summary>Ciclo anual de cobrança (cartão 1x ou PIX Automático anual).</summary>
        public static bool IsAnnualBillingCycle(this ContractCardPlanType? plan) =>
            plan == ContractCardPlanType.Card1x || plan == ContractCardPlanType.PixAnnual;

        public static bool IsAnnualBillingCycle(this ContractCardPlanType plan) =>
            plan == ContractCardPlanType.Card1x || plan == ContractCardPlanType.PixAnnual;

        /// <summary>Parcela mensal com acréscimo (cartão mensal ou PIX Automático mensal).</summary>
        public static bool IsMonthlyInstallmentPlan(this ContractCardPlanType? plan) =>
            plan == ContractCardPlanType.CardMonthly || plan == ContractCardPlanType.PixMonthly;

        public static bool IsMonthlyInstallmentPlan(this ContractCardPlanType plan) =>
            plan == ContractCardPlanType.CardMonthly || plan == ContractCardPlanType.PixMonthly;
    }
}
