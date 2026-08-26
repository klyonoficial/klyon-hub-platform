namespace KlyonHubPlatform.Application.DTOs.Pricing
{
    public class AddressPricesDto
    {
        public decimal PixPrice { get; set; }
        public decimal CardPrice1x { get; set; }
        public decimal BoletoPrice { get; set; }
        public decimal CardPriceMonthly { get; set; }
        public decimal BasePrice { get; set; }
        public decimal? CardMonthlySurchargePercent { get; set; }
    }
}
