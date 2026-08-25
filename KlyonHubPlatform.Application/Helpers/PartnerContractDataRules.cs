using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Helpers
{
    public enum PartnerContractDataSection
    {
        All,
        Basic,
        Address
    }

    public static class PartnerContractDataRules
    {
        public const string NameRequired = "A Razão Social é obrigatória.";
        public const string CnpjRequired = "O CNPJ é obrigatório.";
        public const string CnpjInvalid = "CNPJ inválido. O CNPJ deve conter 14 dígitos numéricos.";
        public const string LegalRepresentativeRequired = "O Representante Legal é obrigatório.";
        public const string PropertyRegistrationRequired = "A Inscrição Imobiliária é obrigatória.";
        public const string AddressRequired = "O endereço (logradouro) é obrigatório.";
        public const string AddressNumberRequired = "O número do endereço é obrigatório.";
        public const string NeighborhoodRequired = "O bairro é obrigatório.";
        public const string CityRequired = "A cidade é obrigatória.";
        public const string StateRequired = "O estado (UF) é obrigatório.";
        public const string ZipCodeRequired = "O CEP é obrigatório.";

        public static string? Validate(Coworking coworking, PartnerContractDataSection section) =>
            Validate(
                coworking.Name,
                coworking.CNPJ,
                coworking.LegalRepresentative,
                coworking.PropertyRegistration,
                coworking.Address,
                coworking.AddressNumber,
                coworking.AddressNeighborhood,
                coworking.AddressCity,
                coworking.AddressState,
                coworking.AddressZipCode,
                section);

        public static string? Validate(
            string? name,
            string? cnpj,
            string? legalRepresentative,
            string? propertyRegistration,
            string? address,
            string? addressNumber,
            string? neighborhood,
            string? city,
            string? state,
            string? zipCode,
            PartnerContractDataSection section)
        {
            var validateBasic = section is PartnerContractDataSection.All or PartnerContractDataSection.Basic;
            var validateAddress = section is PartnerContractDataSection.All or PartnerContractDataSection.Address;

            if (validateBasic)
            {
                if (string.IsNullOrWhiteSpace(name))
                    return NameRequired;

                if (string.IsNullOrWhiteSpace(cnpj))
                    return CnpjRequired;

                var cnpjDigits = PdfFieldFormatter.OnlyDigits(cnpj);
                if (cnpjDigits.Length != 14)
                    return CnpjInvalid;

                if (string.IsNullOrWhiteSpace(legalRepresentative))
                    return LegalRepresentativeRequired;

                if (string.IsNullOrWhiteSpace(propertyRegistration))
                    return PropertyRegistrationRequired;
            }

            if (validateAddress)
            {
                if (string.IsNullOrWhiteSpace(address))
                    return AddressRequired;
                if (string.IsNullOrWhiteSpace(addressNumber))
                    return AddressNumberRequired;
                if (string.IsNullOrWhiteSpace(neighborhood))
                    return NeighborhoodRequired;
                if (string.IsNullOrWhiteSpace(city))
                    return CityRequired;
                if (string.IsNullOrWhiteSpace(state))
                    return StateRequired;
                if (string.IsNullOrWhiteSpace(zipCode))
                    return ZipCodeRequired;
            }

            return null;
        }
    }
}
