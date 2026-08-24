using System.Globalization;
using iTextSharp.text;
using iTextSharp.text.pdf;
using KlyonHubPlatform.Application.Helpers;
using PdfDocument = iTextSharp.text.Document;

namespace KlyonHubPlatform.Application.Services
{
    public class AddressDeclarationPdfRenderOptions
    {
        public required string Title { get; init; }
        public required string DeclarationNumber { get; init; }
        public required string ContractNumber { get; init; }
        public required string ClientName { get; init; }
        public required string ClientCnpjCpf { get; init; }
        public required DateTime StartDate { get; init; }
        public required DateTime EndDate { get; init; }
        public required bool IsCommercial { get; init; }
        public required string CoworkingName { get; init; }
        public required string CoworkingCnpj { get; init; }
        public required string CoworkingFullAddress { get; init; }
        public required string SignatureCity { get; init; }
        public required string SignatureDate { get; init; }
        public required IReadOnlyList<(string Name, string Role, string Cpf)> Signatories { get; init; }
        public required string DigitalSignaturePreview { get; init; }
        public byte[]? WatermarkImageBytes { get; init; }
    }

    public static class AddressDeclarationPdfRenderer
    {
        private static readonly BaseColor BrandPrimary = new(5, 19, 35);
        private static readonly BaseColor SectionBackground = new(248, 249, 252);
        private static readonly BaseColor BorderColor = new(222, 226, 230);
        private static readonly BaseColor TextMuted = new(108, 117, 125);
        private static readonly BaseColor TextDark = new(33, 37, 41);
        private static readonly BaseColor White = BaseColor.White;
        private const float WatermarkOpacity = 0.2f;

        public static byte[] Build(AddressDeclarationPdfRenderOptions options)
        {
            using var ms = new MemoryStream();
            var pdfDocument = new PdfDocument(PageSize.A4, 54, 54, 72, 54);
            var writer = PdfWriter.GetInstance(pdfDocument, ms);
            writer.PageEvent = new DeclarationPageEventHelper(
                options.DeclarationNumber,
                options.WatermarkImageBytes);

            var fonts = CreateFonts();
            pdfDocument.Open();

            AddTitleBanner(pdfDocument, options, fonts);
            AddClientInfoCard(pdfDocument, options, fonts);

            if (options.IsCommercial)
                AddCommercialBody(pdfDocument, options, fonts);
            else
                AddFiscalBody(pdfDocument, options, fonts);

            AddSignatureSection(pdfDocument, options, fonts);

            pdfDocument.Close();
            return ms.ToArray();
        }

        private static DeclarationFonts CreateFonts() => new()
        {
            Title = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, Font.BOLD, White),
            Subtitle = FontFactory.GetFont(FontFactory.HELVETICA, 9.5f, Font.NORMAL, new BaseColor(200, 210, 224)),
            SectionHeader = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, Font.BOLD, White),
            Label = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8.5f, Font.BOLD, TextMuted),
            Value = FontFactory.GetFont(FontFactory.HELVETICA, 9, Font.NORMAL, TextDark),
            Body = FontFactory.GetFont(FontFactory.HELVETICA, 9, Font.NORMAL, TextDark),
            BodyBold = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, Font.BOLD, TextDark),
            Bullet = FontFactory.GetFont(FontFactory.HELVETICA, 9, Font.NORMAL, TextDark),
            Small = FontFactory.GetFont(FontFactory.HELVETICA, 8, Font.NORMAL, TextMuted),
            SignatureName = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, Font.BOLD, TextDark),
            SignatureRole = FontFactory.GetFont(FontFactory.HELVETICA, 8, Font.NORMAL, TextMuted)
        };

        private static void AddTitleBanner(PdfDocument pdfDocument, AddressDeclarationPdfRenderOptions options, DeclarationFonts fonts)
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

            titleCell.AddElement(new Paragraph($"Nº {options.DeclarationNumber}", fonts.Subtitle)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 4
            });

            titleCell.AddElement(new Paragraph($"Contrato vinculado: {options.ContractNumber}", fonts.Subtitle)
            {
                Alignment = Element.ALIGN_CENTER
            });

            banner.AddCell(titleCell);
            pdfDocument.Add(banner);
        }

        private static void AddClientInfoCard(PdfDocument pdfDocument, AddressDeclarationPdfRenderOptions options, DeclarationFonts fonts)
        {
            var outer = new PdfPTable(1) { WidthPercentage = 100, SpacingAfter = 14 };
            var card = new PdfPCell
            {
                Border = Rectangle.BOX,
                BorderColor = BorderColor,
                BorderWidth = 0.8f,
                Padding = 0,
                BackgroundColor = White
            };

            var inner = new PdfPTable(1) { WidthPercentage = 100 };
            inner.AddCell(new PdfPCell(new Phrase("DADOS DO AUTORIZADO", fonts.SectionHeader))
            {
                BackgroundColor = BrandPrimary,
                Border = Rectangle.NO_BORDER,
                PaddingTop = 7,
                PaddingBottom = 7,
                PaddingLeft = 10,
                PaddingRight = 10
            });

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

            foreach (var (label, value) in new[]
                     {
                         ("Empresa / Pessoa", options.ClientName),
                         ("CNPJ / CPF", options.ClientCnpjCpf),
                         ("Data Inicial", options.StartDate.ToString("dd/MM/yyyy")),
                         ("Data Final", options.EndDate.ToString("dd/MM/yyyy"))
                     })
            {
                var row = new PdfPCell { Border = Rectangle.NO_BORDER, PaddingBottom = 5 };
                row.AddElement(new Paragraph(label, fonts.Label) { SpacingAfter = 1 });
                row.AddElement(new Paragraph(PdfFieldFormatter.OrDash(value), fonts.Value));
                fieldsTable.AddCell(row);
            }

            body.AddElement(fieldsTable);
            inner.AddCell(body);
            card.AddElement(inner);
            outer.AddCell(card);
            pdfDocument.Add(outer);
        }

        private static void AddFiscalBody(PdfDocument pdfDocument, AddressDeclarationPdfRenderOptions options, DeclarationFonts fonts)
        {
            AddAuthorizationParagraph(pdfDocument, options, fonts,
                "exclusivamente como ENDEREÇO FISCAL",
                null);

            AddLinkedContractParagraph(pdfDocument, fonts, "Contrato de Cessão de Endereço Fiscal");

            AddSectionTitle(pdfDocument, "Esclarecemos que:", fonts);
            AddBullet(pdfDocument, "O endereço será utilizado apenas para fins fiscais e cadastrais;", fonts);
            AddBullet(pdfDocument, "Não caracteriza vínculo societário, empregatício ou comercial entre as partes;", fonts);
            AddBullet(pdfDocument, "O coworking não se responsabiliza por atividades operacionais, fiscais ou jurídicas exercidas pelo autorizado.", fonts);

            AddClosingParagraph(pdfDocument, fonts,
                "Esta declaração é emitida a pedido do interessado para apresentação junto à ",
                "Receita Federal, Junta Comercial, Prefeitura Municipal e demais órgãos competentes",
                ".");
        }

        private static void AddCommercialBody(PdfDocument pdfDocument, AddressDeclarationPdfRenderOptions options, DeclarationFonts fonts)
        {
            AddAuthorizationParagraph(pdfDocument, options, fonts,
                "para fins de APOIO COMERCIAL E INSTITUCIONAL",
                ", exclusivamente em cartão de visita, site, materiais de marketing e comunicações comerciais.");

            AddLinkedContractParagraph(pdfDocument, fonts, "Contrato de Uso de Endereço Comercial");

            AddSectionTitle(pdfDocument, "Esclarecemos que:", fonts);
            AddBullet(pdfDocument, "O endereço NÃO deve ser utilizado como sede fiscal, domicílio tributário ou endereço cadastral perante a Receita Federal, Junta Comercial ou Prefeitura;", fonts);
            AddBullet(pdfDocument, "Destina-se exclusivamente à imagem comercial, correspondências e apoio administrativo contratado;", fonts);
            AddBullet(pdfDocument, "Não caracteriza vínculo societário, empregatício ou comercial entre as partes;", fonts);
            AddBullet(pdfDocument, "O coworking não se responsabiliza por atividades operacionais, fiscais ou jurídicas exercidas pelo autorizado.", fonts);

            AddClosingParagraph(pdfDocument, fonts,
                "Esta declaração é emitida a pedido do interessado para comprovação do uso comercial autorizado junto a parceiros, fornecedores e terceiros interessados, ",
                "não substituindo documentos fiscais, cadastrais ou de regularização do imóvel",
                ".");
        }

        private static void AddAuthorizationParagraph(
            PdfDocument pdfDocument,
            AddressDeclarationPdfRenderOptions options,
            DeclarationFonts fonts,
            string usageHighlight,
            string? suffix)
        {
            var paragraph = new Paragraph { Alignment = Element.ALIGN_JUSTIFIED, SpacingAfter = 12, Leading = 13f };
            paragraph.Add(new Chunk("Declaramos, para os devidos fins, que o ", fonts.Body));
            paragraph.Add(new Chunk(options.CoworkingName, fonts.BodyBold));
            paragraph.Add(new Chunk(", inscrito no CNPJ sob nº ", fonts.Body));
            paragraph.Add(new Chunk(options.CoworkingCnpj, fonts.BodyBold));
            paragraph.Add(new Chunk(", com sede à ", fonts.Body));
            paragraph.Add(new Chunk(options.CoworkingFullAddress, fonts.BodyBold));
            paragraph.Add(new Chunk(", ", fonts.Body));
            paragraph.Add(new Chunk("AUTORIZA", fonts.BodyBold));
            paragraph.Add(new Chunk(" a empresa/pessoa acima identificada a utilizar o referido endereço ", fonts.Body));
            paragraph.Add(new Chunk(usageHighlight, fonts.BodyBold));
            paragraph.Add(new Chunk(suffix ?? ".", fonts.Body));
            pdfDocument.Add(paragraph);
        }

        private static void AddLinkedContractParagraph(PdfDocument pdfDocument, DeclarationFonts fonts, string contractLabel)
        {
            var paragraph = new Paragraph { Alignment = Element.ALIGN_JUSTIFIED, SpacingAfter = 12, Leading = 13f };
            paragraph.Add(new Chunk("A presente autorização está vinculada ao ", fonts.Body));
            paragraph.Add(new Chunk(contractLabel, fonts.BodyBold));
            paragraph.Add(new Chunk(" firmado entre as partes, sendo válida exclusivamente durante o período acima informado, desde que o contrato permaneça ativo e adimplente.", fonts.Body));
            pdfDocument.Add(paragraph);
        }

        private static void AddSectionTitle(PdfDocument pdfDocument, string title, DeclarationFonts fonts)
        {
            pdfDocument.Add(new Paragraph(title, fonts.BodyBold) { SpacingAfter = 6, SpacingBefore = 4 });
        }

        private static void AddBullet(PdfDocument pdfDocument, string text, DeclarationFonts fonts)
        {
            var symbol = new Chunk("•  ", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, Font.BOLD, BrandPrimary));
            var paragraph = new Paragraph { IndentationLeft = 14, SpacingAfter = 3, Leading = 13f, Alignment = Element.ALIGN_JUSTIFIED };
            paragraph.Add(symbol);
            paragraph.Add(new Chunk(text, fonts.Bullet));
            pdfDocument.Add(paragraph);
        }

        private static void AddClosingParagraph(PdfDocument pdfDocument, DeclarationFonts fonts, string prefix, string highlight, string suffix)
        {
            var paragraph = new Paragraph { Alignment = Element.ALIGN_JUSTIFIED, SpacingAfter = 16, Leading = 13f };
            paragraph.Add(new Chunk(prefix, fonts.Body));
            paragraph.Add(new Chunk(highlight, fonts.BodyBold));
            paragraph.Add(new Chunk(suffix, fonts.Body));
            pdfDocument.Add(paragraph);
        }

        private static void AddSignatureSection(PdfDocument pdfDocument, AddressDeclarationPdfRenderOptions options, DeclarationFonts fonts)
        {
            pdfDocument.Add(new Paragraph($"{options.SignatureCity}, {options.SignatureDate}.", fonts.Body)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingBefore = 8,
                SpacingAfter = 24
            });

            if (options.Signatories.Count == 0)
            {
                AddSignatureBlock(pdfDocument, options.CoworkingName, $"CNPJ: {options.CoworkingCnpj}", fonts);
            }
            else
            {
                foreach (var (name, role, cpf) in options.Signatories)
                {
                    var details = $"Cargo: {role}\nCPF: {cpf}";
                    AddSignatureBlock(pdfDocument, name, details, fonts);
                }

                AddSignatureBlock(pdfDocument, options.CoworkingName, $"CNPJ: {options.CoworkingCnpj}", fonts);
            }

            pdfDocument.Add(new Paragraph(
                $"Declaração Nº: {options.DeclarationNumber}\nAssinatura Digital: {options.DigitalSignaturePreview}...",
                fonts.Small)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingBefore = 12
            });
        }

        private static void AddSignatureBlock(PdfDocument pdfDocument, string name, string details, DeclarationFonts fonts)
        {
            pdfDocument.Add(new Paragraph(new string('_', 42), fonts.Small)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 6
            });

            var block = new Paragraph { Alignment = Element.ALIGN_CENTER, SpacingAfter = 14 };
            block.Add(new Chunk(name, fonts.SignatureName));
            block.Add(new Chunk("\n" + details, fonts.SignatureRole));
            pdfDocument.Add(block);
        }

        private sealed class DeclarationFonts
        {
            public Font Title { get; init; } = null!;
            public Font Subtitle { get; init; } = null!;
            public Font SectionHeader { get; init; } = null!;
            public Font Label { get; init; } = null!;
            public Font Value { get; init; } = null!;
            public Font Body { get; init; } = null!;
            public Font BodyBold { get; init; } = null!;
            public Font Bullet { get; init; } = null!;
            public Font Small { get; init; } = null!;
            public Font SignatureName { get; init; } = null!;
            public Font SignatureRole { get; init; } = null!;
        }

        private sealed class DeclarationPageEventHelper : PdfPageEventHelper
        {
            private readonly string _declarationNumber;
            private readonly byte[]? _watermarkImageBytes;
            private readonly Font _footerFont = FontFactory.GetFont(FontFactory.HELVETICA, 8, Font.NORMAL, TextMuted);
            private PdfTemplate? _totalPagesTemplate;

            public DeclarationPageEventHelper(string declarationNumber, byte[]? watermarkImageBytes)
            {
                _declarationNumber = declarationNumber;
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
                    // Logo opcional: não interrompe a geração da declaração.
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
                    new Phrase($"Declaração nº {_declarationNumber}", _footerFont),
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
