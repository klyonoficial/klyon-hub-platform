namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>Ciclo de cobrança da assinatura (<see cref="Domain.Entities.Subscription.BillingCycle"/>).</summary>
    public static class BillingCycleConstants
    {
        public const string Monthly = "Monthly";
        public const string Yearly = "Yearly";

        public static bool IsMonthly(string? billingCycle) =>
            string.Equals(billingCycle, Monthly, StringComparison.OrdinalIgnoreCase);

        public static bool IsYearly(string? billingCycle) =>
            string.Equals(billingCycle, Yearly, StringComparison.OrdinalIgnoreCase);
    }
}
