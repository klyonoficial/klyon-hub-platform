namespace KlyonHubPlatform.Application.Helpers

{

    public static class CommercialAddressContractRules

    {

        public const string DescriptionWithCnpj = "Contrato Endereço Comercial (com CNPJ)";

        public const string DescriptionWithoutCnpj = "Contrato Endereço Comercial (reserva — sem CNPJ)";



        public static string DescriptionForVariant(FiscalAddressContractVariant variant) =>

            variant == FiscalAddressContractVariant.WithCnpj

                ? DescriptionWithCnpj

                : DescriptionWithoutCnpj;

    }

}

