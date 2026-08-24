namespace KlyonHubPlatform.Domain.Core.Constants
{
    public static class AddressDeclarationLabels
    {
        public static bool IsCommercial(string? serviceCode) => ServiceCodes.IsCommercialAddress(serviceCode);

        public static string SectionTitle(string? serviceCode) => "Declarações:";

        public static string PageTitle(string? serviceCode) =>
            IsCommercial(serviceCode)
                ? "Declarações de Uso de Endereço Comercial"
                : "Declarações de Uso de Endereço Fiscal";

        public static string PdfTitle(string? serviceCode) =>
            IsCommercial(serviceCode)
                ? "DECLARAÇÃO DE AUTORIZAÇÃO DE USO DE ENDEREÇO COMERCIAL"
                : "DECLARAÇÃO DE AUTORIZAÇÃO DE USO DE ENDEREÇO FISCAL";

        public static string DownloadFilePrefix(string? serviceCode) =>
            IsCommercial(serviceCode) ? "Declaracao_Comercial" : "Declaracao_Fiscal";
    }
}
