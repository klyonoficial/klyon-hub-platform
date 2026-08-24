using KlyonHubPlatform.Application.Helpers;

namespace KlyonHubPlatform.Application.Services
{
    public class FiscalAddressContractPdfModel
    {
        public FiscalAddressContractVariant Variant { get; set; }
        public string ContractNumberFormatted { get; set; } = "-";

        public string? ClientFullName { get; set; }
        public string? ClientCpf { get; set; }
        public string? ClientAddress { get; set; }
        public string? ClientEmail { get; set; }
        public string? ClientPhone { get; set; }

        public string? ClientRazaoSocial { get; set; }
        public string? ClientCnpj { get; set; }
        public string? LegalRepresentative { get; set; }
        public string? RepresentativeCpf { get; set; }
        public string? RepresentativeAddress { get; set; }

        public string ContractorCompanyName { get; set; } = "-";
        public string ContractorCnpj { get; set; } = "-";
        public string ContractorAddress { get; set; } = "-";
        public string PropertyRegistration { get; set; } = "-";
        public string ContractorLegalRepresentative { get; set; } = "-";

        public string PaymentClause { get; set; } = "-";
        public string SignatureCity { get; set; } = "Cariacica/ES";
        public string SignatureDate { get; set; } = "-";
        public string ClientSignatureName { get; set; } = "-";
    }

    public static class FiscalAddressContractPdfBuilder
    {
        public const string Title = "CONTRATO DE PRESTAÇÃO DE SERVIÇOS DE ENDEREÇO FISCAL";
        public const string SubtitleWithoutCnpj = "(Para Constituição de Pessoa Jurídica)";

        public static byte[] BuildPdf(FiscalAddressContractPdfModel model, byte[]? watermarkImageBytes = null) =>
            AddressContractPdfRenderer.Build(new AddressContractPdfRenderOptions
            {
                Title = Title,
                Subtitle = model.Variant == FiscalAddressContractVariant.WithoutCnpj
                    ? SubtitleWithoutCnpj
                    : null,
                Model = model,
                IntroParagraph = FiscalAddressContractClauses.IntroParagraph,
                Clauses = FiscalAddressContractClauses.GetClauses(model.Variant, model.PaymentClause),
                ClosingParagraph = FiscalAddressContractClauses.ClosingParagraph(model.Variant),
                WatermarkImageBytes = watermarkImageBytes
            });

        public static string FormatSignatureDate(DateTime date) =>
            AddressContractPdfRenderer.FormatSignatureDate(date);
    }
}
