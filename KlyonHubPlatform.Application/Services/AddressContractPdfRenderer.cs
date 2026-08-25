using System.Globalization;
using System.Text.RegularExpressions;
using iTextSharp.text;
using iTextSharp.text.pdf;
using KlyonHubPlatform.Application.Helpers;
using PdfDocument = iTextSharp.text.Document;

namespace KlyonHubPlatform.Application.Services
{
    public class AddressContractPdfRenderOptions
    {
        public required string Title { get; init; }
        public string? Subtitle { get; init; }
        public required FiscalAddressContractPdfModel Model { get; init; }
        public required string IntroParagraph { get; init; }
        public required IReadOnlyList<string> Clauses { get; init; }
        public required string ClosingParagraph { get; init; }
        public byte[]? WatermarkImageBytes { get; init; }
    }

    public static class AddressContractPdfRenderer
    {
        private static readonly BaseColor BrandPrimary = new(5, 19, 35);
        private static readonly BaseColor BrandPrimaryDark = new(5, 19, 35);
        private static readonly BaseColor SectionBackground = new(248, 249, 252);
        private static readonly BaseColor BorderColor = new(222, 226, 230);
        private static readonly BaseColor TextMuted = new(108, 117, 125);
        private static readonly BaseColor TextDark = new(33, 37, 41);
        private static readonly BaseColor White = BaseColor.White;
        private static readonly BaseColor ClauseTitleBackground = new(230, 235, 242);
        private const float WatermarkOpacity = 0.2f;

        private static readonly Regex ClauseTitleRegex = new(
            @"^(?:CLÁUSULA|\d+ª\s+CLÁUSULA)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex NumberedItemRegex = new(
            @"^\d+\.\d+\.",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex LetterItemRegex = new(
            @"^[a-h]\)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static byte[] Build(AddressContractPdfRenderOptions options)
        {
            using var ms = new MemoryStream();
            var pdfDocument = new PdfDocument(PageSize.A4, 54, 54, 72, 54);
            var writer = PdfWriter.GetInstance(pdfDocument, ms);
            writer.PageEvent = new ContractPageEventHelper(
                PdfFieldFormatter.OrDash(options.Model.ContractNumberFormatted),
                options.WatermarkImageBytes);

            var fonts = CreateFonts();
            pdfDocument.Open();

            AddTitleBanner(pdfDocument, options, fonts);
            AddPartiesSection(pdfDocument, options.Model, fonts);
            AddIntroBox(pdfDocument, options.IntroParagraph, fonts);

            foreach (var clause in options.Clauses)
                RenderClause(pdfDocument, clause, fonts);

            AddClosingSection(pdfDocument, options, fonts);

            pdfDocument.Close();
            return ms.ToArray();
        }

        public static string FormatSignatureDate(DateTime date)
        {
            var culture = new CultureInfo("pt-BR");
            var month = culture.DateTimeFormat.GetMonthName(date.Month);
            if (!string.IsNullOrEmpty(month))
                month = char.ToUpper(month[0]) + month[1..];
            return $"{date.Day:D2} de {month} de {date.Year}";
        }

        private static ContractFonts CreateFonts()
        {
            return new ContractFonts
            {
                Title = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 15, Font.BOLD, White),
                Subtitle = FontFactory.GetFont(FontFactory.HELVETICA, 10, Font.NORMAL, new BaseColor(200, 210, 224)),
                ContractNumber = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, Font.BOLD, White),
                SectionHeader = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, Font.BOLD, White),
                ClauseTitle = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, Font.BOLD, BrandPrimary),
                Label = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8.5f, Font.BOLD, TextMuted),
                Value = FontFactory.GetFont(FontFactory.HELVETICA, 9, Font.NORMAL, TextDark),
                Body = FontFactory.GetFont(FontFactory.HELVETICA, 9, Font.NORMAL, TextDark),
                BodyItalic = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 9, Font.ITALIC, TextDark),
                Bullet = FontFactory.GetFont(FontFactory.HELVETICA, 9, Font.NORMAL, TextDark),
                Small = FontFactory.GetFont(FontFactory.HELVETICA, 8, Font.NORMAL, TextMuted),
                SignatureName = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, Font.BOLD, TextDark),
                SignatureRole = FontFactory.GetFont(FontFactory.HELVETICA, 8, Font.NORMAL, TextMuted)
            };
        }

        private static void AddTitleBanner(PdfDocument pdfDocument, AddressContractPdfRenderOptions options, ContractFonts fonts)
        {
            var banner = new PdfPTable(1) { WidthPercentage = 100, SpacingAfter = 14 };
            var titleCell = new PdfPCell
            {
                BackgroundColor = BrandPrimary,
                Border = Rectangle.NO_BORDER,
                PaddingTop = 16,
                PaddingBottom = 12,
                PaddingLeft = 14,
                PaddingRight = 14
            };

            titleCell.AddElement(new Paragraph(options.Title, fonts.Title)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 6
            });

            if (!string.IsNullOrWhiteSpace(options.Subtitle))
            {
                titleCell.AddElement(new Paragraph(options.Subtitle, fonts.Subtitle)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 4
                });
            }

            titleCell.AddElement(new Paragraph($"Nº {PdfFieldFormatter.OrDash(options.Model.ContractNumberFormatted)}", fonts.Subtitle)
            {
                Alignment = Element.ALIGN_CENTER
            });

            banner.AddCell(titleCell);
            pdfDocument.Add(banner);
        }

        private static void AddPartiesSection(PdfDocument pdfDocument, FiscalAddressContractPdfModel model, ContractFonts fonts)
        {
            var partiesTable = new PdfPTable(2) { WidthPercentage = 100, SpacingAfter = 14 };
            partiesTable.SetWidths(new[] { 50f, 50f });
            partiesTable.DefaultCell.Border = Rectangle.NO_BORDER;

            partiesTable.AddCell(CreatePartyCard("CONTRATANTE", BuildContractantFields(model), fonts));
            partiesTable.AddCell(CreatePartyCard("CONTRATADA", BuildContractedFields(model), fonts));
            pdfDocument.Add(partiesTable);
        }

        private static PdfPCell CreatePartyCard(string title, IReadOnlyList<(string Label, string Value)> fields, ContractFonts fonts)
        {
            var outer = new PdfPCell
            {
                Border = Rectangle.BOX,
                BorderColor = BorderColor,
                BorderWidth = 0.8f,
                Padding = 0,
                PaddingRight = 4,
                PaddingLeft = 4,
                BackgroundColor = White
            };

            var card = new PdfPTable(1) { WidthPercentage = 100 };

            var header = new PdfPCell(new Phrase(title, fonts.SectionHeader))
            {
                BackgroundColor = BrandPrimaryDark,
                Border = Rectangle.NO_BORDER,
                PaddingTop = 7,
                PaddingBottom = 7,
                PaddingLeft = 10,
                PaddingRight = 10
            };
            card.AddCell(header);

            var body = new PdfPCell
            {
                BackgroundColor = SectionBackground,
                Border = Rectangle.NO_BORDER,
                PaddingTop = 8,
                PaddingBottom = 10,
                PaddingLeft = 10,
                PaddingRight = 10
            };

            var fieldsTable = new PdfPTable(1) { WidthPercentage = 100 };
            fieldsTable.DefaultCell.Border = Rectangle.NO_BORDER;
            foreach (var (label, value) in fields)
            {
                var row = new PdfPCell { Border = Rectangle.NO_BORDER, PaddingBottom = 5 };
                row.AddElement(new Paragraph(label, fonts.Label) { SpacingAfter = 1 });
                row.AddElement(new Paragraph(value ?? string.Empty, fonts.Value));
                fieldsTable.AddCell(row);
            }

            body.AddElement(fieldsTable);
            card.AddCell(body);
            outer.AddElement(card);
            return outer;
        }

        private static IReadOnlyList<(string Label, string Value)> BuildContractantFields(FiscalAddressContractPdfModel model)
        {
            var fields = new List<(string, string)>();
            if (model.Variant == FiscalAddressContractVariant.WithCnpj)
            {
                fields.Add(("Razão Social", PdfFieldFormatter.OrDash(model.ClientRazaoSocial)));
                fields.Add(("CNPJ", PdfFieldFormatter.OrDash(model.ClientCnpj)));
                fields.Add(("Representante Legal", model.LegalRepresentative ?? string.Empty));
                fields.Add(("CPF", PdfFieldFormatter.OrDash(model.RepresentativeCpf)));
                fields.Add(("Endereço do Representante", PdfFieldFormatter.OrDash(model.RepresentativeAddress)));
            }
            else
            {
                fields.Add(("Nome Completo", model.ClientFullName ?? string.Empty));
                fields.Add(("CPF", PdfFieldFormatter.OrDash(model.ClientCpf)));
                fields.Add(("Endereço", PdfFieldFormatter.OrDash(model.ClientAddress)));
            }

            fields.Add(("E-mail", PdfFieldFormatter.OrDash(model.ClientEmail)));
            fields.Add(("Telefone", PdfFieldFormatter.OrDash(model.ClientPhone)));
            return fields;
        }

        private static IReadOnlyList<(string Label, string Value)> BuildContractedFields(FiscalAddressContractPdfModel model) =>
            new List<(string, string)>
            {
                ("Razão Social", PdfFieldFormatter.OrDash(model.ContractorCompanyName)),
                ("CNPJ", PdfFieldFormatter.OrDash(model.ContractorCnpj)),
                ("Endereço", PdfFieldFormatter.OrDash(model.ContractorAddress)),
                ("Inscrição Imobiliária", PdfFieldFormatter.OrDash(model.PropertyRegistration)),
                ("Representante Legal", PdfFieldFormatter.OrDash(model.ContractorLegalRepresentative))
            };

        private static void AddIntroBox(PdfDocument pdfDocument, string intro, ContractFonts fonts)
        {
            var table = new PdfPTable(1) { WidthPercentage = 100, SpacingAfter = 12 };
            var cell = new PdfPCell(new Phrase(intro, fonts.BodyItalic))
            {
                BackgroundColor = SectionBackground,
                BorderColor = BorderColor,
                BorderWidth = 0.8f,
                Padding = 10,
                HorizontalAlignment = Element.ALIGN_JUSTIFIED
            };
            table.AddCell(cell);
            pdfDocument.Add(table);
        }

        private static void RenderClause(PdfDocument pdfDocument, string clauseText, ContractFonts fonts)
        {
            var lines = clauseText
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (ClauseTitleRegex.IsMatch(line))
                {
                    AddClauseTitle(pdfDocument, line, fonts);
                    continue;
                }

                if (line.StartsWith('•'))
                {
                    AddBulletLine(pdfDocument, line[1..].Trim(), fonts);
                    continue;
                }

                if (LetterItemRegex.IsMatch(line))
                {
                    AddLetterLine(pdfDocument, line, fonts);
                    continue;
                }

                var paragraph = new Paragraph(line, fonts.Body)
                {
                    Alignment = Element.ALIGN_JUSTIFIED,
                    SpacingAfter = 4,
                    Leading = 13f
                };

                if (NumberedItemRegex.IsMatch(line))
                    paragraph.SpacingBefore = 2;

                pdfDocument.Add(paragraph);
            }

            pdfDocument.Add(new Paragraph(" ") { SpacingAfter = 8 });
        }

        private static void AddClauseTitle(PdfDocument pdfDocument, string title, ContractFonts fonts)
        {
            var table = new PdfPTable(1) { WidthPercentage = 100, SpacingBefore = 6, SpacingAfter = 6 };
            var cell = new PdfPCell(new Phrase(title, fonts.ClauseTitle))
            {
                BackgroundColor = ClauseTitleBackground,
                Border = Rectangle.LEFT_BORDER,
                BorderWidthLeft = 3f,
                BorderColorLeft = BrandPrimary,
                PaddingTop = 6,
                PaddingBottom = 6,
                PaddingLeft = 10,
                PaddingRight = 8
            };
            table.AddCell(cell);
            pdfDocument.Add(table);
        }

        private static void AddBulletLine(PdfDocument pdfDocument, string text, ContractFonts fonts)
        {
            var symbol = new Chunk("•  ", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, Font.BOLD, BrandPrimary));
            var paragraph = new Paragraph { IndentationLeft = 14, SpacingAfter = 3, Leading = 13f };
            paragraph.Add(symbol);
            paragraph.Add(new Chunk(text, fonts.Bullet));
            pdfDocument.Add(paragraph);
        }

        private static void AddLetterLine(PdfDocument pdfDocument, string text, ContractFonts fonts)
        {
            var paragraph = new Paragraph(text, fonts.Body)
            {
                IndentationLeft = 12,
                Alignment = Element.ALIGN_JUSTIFIED,
                SpacingAfter = 3,
                Leading = 13f
            };
            pdfDocument.Add(paragraph);
        }

        private static void AddClosingSection(PdfDocument pdfDocument, AddressContractPdfRenderOptions options, ContractFonts fonts)
        {
            pdfDocument.Add(new Paragraph(options.ClosingParagraph, fonts.BodyItalic)
            {
                Alignment = Element.ALIGN_JUSTIFIED,
                SpacingBefore = 8,
                SpacingAfter = 14,
                Leading = 13f
            });

            pdfDocument.Add(new Paragraph($"{options.Model.SignatureCity}, {options.Model.SignatureDate}.", fonts.Body)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 22
            });

            var signatures = new PdfPTable(2) { WidthPercentage = 100, SpacingAfter = 10 };
            signatures.SetWidths(new[] { 50f, 50f });
            signatures.AddCell(CreateSignatureCell(options.Model.ContractorCompanyName, "CONTRATADA", fonts));
            signatures.AddCell(CreateSignatureCell(options.Model.ClientSignatureName, "CONTRATANTE", fonts));
            pdfDocument.Add(signatures);
        }

        private static PdfPCell CreateSignatureCell(string name, string role, ContractFonts fonts)
        {
            var cell = new PdfPCell
            {
                Border = Rectangle.NO_BORDER,
                PaddingTop = 8,
                PaddingBottom = 4,
                HorizontalAlignment = Element.ALIGN_CENTER
            };

            var line = new Paragraph(new string('_', 38), fonts.Small)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 8
            };
            cell.AddElement(line);
            cell.AddElement(new Paragraph(PdfFieldFormatter.OrDash(name), fonts.SignatureName)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 2
            });
            cell.AddElement(new Paragraph(role, fonts.SignatureRole) { Alignment = Element.ALIGN_CENTER });
            return cell;
        }

        private sealed class ContractFonts
        {
            public Font Title { get; init; } = null!;
            public Font Subtitle { get; init; } = null!;
            public Font ContractNumber { get; init; } = null!;
            public Font SectionHeader { get; init; } = null!;
            public Font ClauseTitle { get; init; } = null!;
            public Font Label { get; init; } = null!;
            public Font Value { get; init; } = null!;
            public Font Body { get; init; } = null!;
            public Font BodyItalic { get; init; } = null!;
            public Font Bullet { get; init; } = null!;
            public Font Small { get; init; } = null!;
            public Font SignatureName { get; init; } = null!;
            public Font SignatureRole { get; init; } = null!;
        }

        private sealed class ContractPageEventHelper : PdfPageEventHelper
        {
            private readonly string _contractNumber;
            private readonly byte[]? _watermarkImageBytes;
            private readonly Font _footerFont = FontFactory.GetFont(FontFactory.HELVETICA, 8, Font.NORMAL, TextMuted);
            private PdfTemplate? _totalPagesTemplate;

            public ContractPageEventHelper(string contractNumber, byte[]? watermarkImageBytes)
            {
                _contractNumber = contractNumber;
                _watermarkImageBytes = watermarkImageBytes;
            }

            public override void OnOpenDocument(PdfWriter writer, PdfDocument document)
            {
                _totalPagesTemplate = writer.DirectContent.CreateTemplate(30, 16);
            }

            public override void OnStartPage(PdfWriter writer, PdfDocument document)
            {
                if (_watermarkImageBytes is not { Length: > 0 })
                    return;

                var pageSize = document.PageSize;
                var under = writer.DirectContentUnder;
                under.SaveState();

                try
                {
                    var watermark = Image.GetInstance(_watermarkImageBytes);
                    watermark.ScaleToFit(pageSize.Width * 0.45f, pageSize.Height * 0.32f);
                    var x = (pageSize.Width - watermark.ScaledWidth) / 2f;
                    var y = (pageSize.Height - watermark.ScaledHeight) / 2f;
                    watermark.SetAbsolutePosition(x, y);

                    var gState = new PdfGState
                    {
                        FillOpacity = WatermarkOpacity,
                        StrokeOpacity = WatermarkOpacity
                    };
                    under.SetGState(gState);
                    under.AddImage(watermark);
                }
                catch
                {
                    // Logo opcional: não interrompe a geração do contrato.
                }

                under.RestoreState();
            }

            public override void OnEndPage(PdfWriter writer, PdfDocument document)
            {
                var pageSize = document.PageSize;
                var cb = writer.DirectContent;

                cb.SetColorFill(BrandPrimary);
                cb.Rectangle(pageSize.GetLeft(54), pageSize.GetTop(42), pageSize.Width - 108, 3);
                cb.Fill();

                var footerY = pageSize.GetBottom(42);
                ColumnText.ShowTextAligned(cb, Element.ALIGN_LEFT,
                    new Phrase($"Contrato nº {_contractNumber}", _footerFont),
                    pageSize.GetLeft(54), footerY, 0);

                var pageText = $"Página {writer.PageNumber} de ";
                var textWidth = _footerFont.GetCalculatedBaseFont(true).GetWidthPoint(pageText, _footerFont.Size);
                ColumnText.ShowTextAligned(cb, Element.ALIGN_RIGHT,
                    new Phrase(pageText, _footerFont),
                    pageSize.GetRight(54) - textWidth, footerY, 0);

                if (_totalPagesTemplate != null)
                    cb.AddTemplate(_totalPagesTemplate, pageSize.GetRight(54), footerY);
            }

            public override void OnCloseDocument(PdfWriter writer, PdfDocument document)
            {
                if (_totalPagesTemplate == null)
                    return;

                _totalPagesTemplate.BeginText();
                _totalPagesTemplate.SetFontAndSize(_footerFont.BaseFont, _footerFont.Size);
                _totalPagesTemplate.ShowText((writer.PageNumber - 1).ToString(CultureInfo.InvariantCulture));
                _totalPagesTemplate.EndText();
            }
        }
    }
}
