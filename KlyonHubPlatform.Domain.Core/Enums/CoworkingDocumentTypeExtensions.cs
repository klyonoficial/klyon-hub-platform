namespace KlyonHubPlatform.Domain.Core.Enums
{
    public static class CoworkingDocumentTypeExtensions
    {
        public static string ToDisplayString(this CoworkingDocumentType type) => type switch
        {
            CoworkingDocumentType.AddressCessionContract => DisplayStrings.AddressCessionContract,
            CoworkingDocumentType.AddressProof => DisplayStrings.AddressProof,
            CoworkingDocumentType.CNPJCard => DisplayStrings.CNPJCard,
            CoworkingDocumentType.AddressAuthorization => DisplayStrings.AddressAuthorization,
            CoworkingDocumentType.UtilityBill => DisplayStrings.UtilityBill,
            CoworkingDocumentType.OperatingLicense => DisplayStrings.OperatingLicense,
            CoworkingDocumentType.FireDepartmentLicense => DisplayStrings.FireDepartmentLicense,
            _ => type.ToString()
        };
        public static string ToApiKey(this CoworkingDocumentType type) => type switch
        {
            CoworkingDocumentType.AddressCessionContract => "address_cession_contract",
            CoworkingDocumentType.AddressProof => "address_proof",
            CoworkingDocumentType.CNPJCard => "cnpj_card",
            CoworkingDocumentType.AddressAuthorization => "address_authorization",
            CoworkingDocumentType.UtilityBill => "utility_bill",
            CoworkingDocumentType.OperatingLicense => "operating_license",
            CoworkingDocumentType.FireDepartmentLicense => "fire_department_license",
            _ => "other"
        };

        public static bool IsPublicTrustDocument(this CoworkingDocumentType type) =>
            type is CoworkingDocumentType.OperatingLicense
                or CoworkingDocumentType.FireDepartmentLicense
                or CoworkingDocumentType.CNPJCard;

        /// <summary>Tipos da aba Documentos Obrigatórios do perfil do parceiro.</summary>
        public static readonly CoworkingDocumentType[] MandatoryProfileTypes =
        {
            CoworkingDocumentType.AddressCessionContract,
            CoworkingDocumentType.AddressProof,
            CoworkingDocumentType.CNPJCard,
            CoworkingDocumentType.AddressAuthorization,
            CoworkingDocumentType.OperatingLicense,
            CoworkingDocumentType.FireDepartmentLicense
        };

        public static bool IsClientDownloadablePartnerDocument(this CoworkingDocumentType type) =>
            MandatoryProfileTypes.Contains(type);

        public static class DisplayStrings
        {
            public const string AddressCessionContract = "Contrato de Cessão de Endereço";
            public const string AddressProof = "IPTU";
            public const string CNPJCard = "Cartão CNPJ";
            public const string AddressAuthorization = "Declaração de Autorização";
            public const string UtilityBill = "Conta de Utilidade";
            public const string OperatingLicense = "Alvará de Funcionamento";
            public const string FireDepartmentLicense = "Alvará dos Bombeiros";
        }
    }
}