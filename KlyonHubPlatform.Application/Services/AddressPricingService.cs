using KlyonHubPlatform.Application.DTOs.Pricing;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Services
{
    public class AddressPricingService : IAddressPricingService
    {
        public decimal GetAnnualPrice(decimal basePrice) => RoundCurrency(basePrice);

        public decimal GetBoletoPrice(decimal basePrice) => GetAnnualPrice(basePrice);

        public decimal GetMonthlyInstallment(decimal basePrice, decimal? surchargePercent)
        {
            var surcharge = NormalizeSurcharge(surchargePercent);
            var annualWithSurcharge = basePrice * (1 + surcharge / 100m);
            return RoundCurrency(annualWithSurcharge / 12m);
        }

        public decimal GetChargeAmount(decimal basePrice, decimal? surchargePercent, ContractCardPlanType? planType)
        {
            if (planType == ContractCardPlanType.CardMonthly)
                return GetMonthlyInstallment(basePrice, surchargePercent);

            return GetAnnualPrice(basePrice);
        }

        public bool SupportsCardPayment(decimal basePrice) => basePrice > 0;

        public AddressPricesDto GetPricesForAddress(CoworkingAddress address)
        {
            var annual = GetAnnualPrice(address.BasePrice);
            var monthly = GetMonthlyInstallment(address.BasePrice, address.CardMonthlySurchargePercent);

            return new AddressPricesDto
            {
                BasePrice = address.BasePrice,
                CardMonthlySurchargePercent = address.CardMonthlySurchargePercent,
                PixPrice = annual,
                CardPrice1x = annual,
                BoletoPrice = annual,
                CardPriceMonthly = monthly
            };
        }

        public void SyncDerivedCardPrices(CoworkingAddress address)
        {
            var prices = GetPricesForAddress(address);
            address.CardPrice1x = prices.CardPrice1x;
            address.CardPriceMonthly = prices.CardPriceMonthly;
        }

        private static decimal NormalizeSurcharge(decimal? surchargePercent)
        {
            var surcharge = surchargePercent ?? 0m;
            if (surcharge < 0) surcharge = 0;
            if (surcharge > 100) surcharge = 100;
            return surcharge;
        }

        private static decimal RoundCurrency(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
