namespace KlyonHubPlatform.Domain.Core.Constants
{
    public static class ServiceCodes
    {
        /// <summary>Prefixo usado para filtrar serviços de endereço fiscal (ex.: exclusão em listas).</summary>
        public const string FiscalAddressCodePrefix = "FISCAL_ADDRESS";

        public const string FiscalAddressOffer = "FISCAL_ADDRESS_OFFER";
        public const string FiscalAddressContracting = "FISCAL_ADDRESS_CONTRACTING";

        /// <summary>Prefixo para serviços de endereço comercial.</summary>
        public const string CommercialAddressCodePrefix = "COMMERCIAL_ADDRESS";

        /// <summary>Contratação de endereço comercial (cartão de visita, site — sem uso no CNPJ).</summary>
        public const string CommercialAddressContracting = "COMMERCIAL_ADDRESS_CONTRACTING";

        public const string AuditoriumRental = "AUDITORIUM_RENTAL";
        public const string RoomReservation = "ROOM_RESERVATION";
        public const string VirtualOffice = "VIRTUAL_OFFICE";

        public static bool IsCommercialAddress(string? serviceCode) =>
            !string.IsNullOrWhiteSpace(serviceCode) &&
            serviceCode.StartsWith(CommercialAddressCodePrefix, StringComparison.OrdinalIgnoreCase);

        public static bool IsFiscalAddress(string? serviceCode) =>
            !string.IsNullOrWhiteSpace(serviceCode) &&
            serviceCode.StartsWith(FiscalAddressCodePrefix, StringComparison.OrdinalIgnoreCase);
    }
}
