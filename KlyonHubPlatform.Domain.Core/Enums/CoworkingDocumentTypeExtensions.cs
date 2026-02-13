namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Extensões para obter textos de exibição do enum CoworkingDocumentType.
    /// </summary>
    public static class CoworkingDocumentTypeExtensions
    {
        public static string ToDisplayString(this CoworkingDocumentType type) => type switch
        {
            CoworkingDocumentType.AddressCessionContract => DisplayStrings.AddressCessionContract,
            CoworkingDocumentType.AddressProof => DisplayStrings.AddressProof,
            CoworkingDocumentType.CNPJCard => DisplayStrings.CNPJCard,
            CoworkingDocumentType.AddressAuthorization => DisplayStrings.AddressAuthorization,
            CoworkingDocumentType.UtilityBill => DisplayStrings.UtilityBill,
            _ => type.ToString()
        };

        /// <summary>Identificador para API/storage (ex: address_cession_contract).</summary>
        public static string ToApiKey(this CoworkingDocumentType type) => type switch
        {
            CoworkingDocumentType.AddressCessionContract => "address_cession_contract",
            CoworkingDocumentType.AddressProof => "address_proof",
            CoworkingDocumentType.CNPJCard => "cnpj_card",
            CoworkingDocumentType.AddressAuthorization => "address_authorization",
            _ => "other"
        };

        public static class DisplayStrings
        {
            public const string AddressCessionContract = "Contrato de Cessão de Endereço";
            public const string AddressProof = "Comprovante de Endereço";
            public const string CNPJCard = "Cartão CNPJ";
            public const string AddressAuthorization = "Declaração de Autorização";
            public const string UtilityBill = "Conta de Utilidade";
        }
    }
}
