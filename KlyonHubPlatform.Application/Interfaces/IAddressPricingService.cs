using KlyonHubPlatform.Application.DTOs.Pricing;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IAddressPricingService
    {
        decimal GetAnnualPrice(decimal basePrice);
        /// <summary>Boleto cobra o valor anual base (igual PIX e cartão 1x).</summary>
        decimal GetBoletoPrice(decimal basePrice);
        decimal GetMonthlyInstallment(decimal basePrice, decimal? surchargePercent);
        decimal GetChargeAmount(decimal basePrice, decimal? surchargePercent, ContractCardPlanType? planType);
        bool SupportsCardPayment(decimal basePrice);
        AddressPricesDto GetPricesForAddress(CoworkingAddress address);
        void SyncDerivedCardPrices(CoworkingAddress address);
    }
}
