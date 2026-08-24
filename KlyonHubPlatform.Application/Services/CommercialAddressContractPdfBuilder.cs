using KlyonHubPlatform.Application.Helpers;

namespace KlyonHubPlatform.Application.Services
{
    public static class CommercialAddressContractPdfBuilder
    {
        public const string Title = "CONTRATO DE PRESTAÇÃO DE SERVIÇOS DE ENDEREÇO COMERCIAL";

        public static byte[] BuildPdf(FiscalAddressContractPdfModel model, byte[]? watermarkImageBytes = null) =>
            AddressContractPdfRenderer.Build(new AddressContractPdfRenderOptions
            {
                Title = Title,
                Model = model,
                IntroParagraph = CommercialAddressContractClauses.IntroParagraph,
                Clauses = CommercialAddressContractClauses.GetClauses(model.Variant, model.PaymentClause),
                ClosingParagraph = CommercialAddressContractClauses.ClosingParagraph(model.Variant),
                WatermarkImageBytes = watermarkImageBytes
            });

        public static string FormatSignatureDate(DateTime date) =>
            AddressContractPdfRenderer.FormatSignatureDate(date);
    }
}
