using KlyonHubPlatform.Application.DTOs.Contracts;
using KlyonHubPlatform.Application.Helpers;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Services
{
    public class ContractTimelineService : IContractTimelineService
    {
        private readonly IContractHistoryService _contractHistoryService;

        public ContractTimelineService(IContractHistoryService contractHistoryService)
        {
            _contractHistoryService = contractHistoryService;
        }

        public async Task<ContractTimelinePageDto> BuildPagedAsync(
            Contract contract,
            IReadOnlyList<Payment> payments,
            IReadOnlyDictionary<int, List<PaymentHistory>> historiesByPaymentId,
            IReadOnlyList<AddressDeclaration> declarations,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var items = new List<ContractTimelineItemDto>
            {
                new()
                {
                    Date = contract.CreatedAt,
                    Type = "Criação",
                    Title = "Contrato Criado",
                    Description = $"Contrato {contract.ContractNumber} foi criado",
                    Icon = "bi-file-earmark-plus",
                    Color = BootstrapBadgeCss.Primary
                }
            };

            if (contract.UpdatedAt.HasValue && contract.UpdatedAt != contract.CreatedAt)
            {
                var statusPt = contract.Status.ToDisplayString();
                items.Add(new ContractTimelineItemDto
                {
                    Date = contract.UpdatedAt.Value,
                    Type = "Status",
                    Title = $"Status: {statusPt}",
                    Description = $"Status do contrato alterado para {statusPt}",
                    Icon = "bi-arrow-repeat",
                    Color = BootstrapBadgeCss.Info
                });
            }

            foreach (var payment in payments)
            {
                items.Add(new ContractTimelineItemDto
                {
                    Date = payment.CreatedAt,
                    Type = "Pagamento",
                    Title = $"Pagamento de R$ {payment.Amount:N2}",
                    Description = $"Status: {payment.Status.ToDisplayStringShort()} - Método: {payment.Method.ToDisplayString()}",
                    Icon = "bi-cash-coin",
                    Color = payment.Status == PaymentStatus.Paid ? BootstrapBadgeCss.Success : BootstrapBadgeCss.Warning
                });
                if (payment.PaidAt.HasValue)
                {
                    items.Add(new ContractTimelineItemDto
                    {
                        Date = payment.PaidAt.Value,
                        Type = "Pagamento",
                        Title = "Pagamento Confirmado",
                        Description = $"Pagamento de R$ {payment.Amount:N2} foi confirmado",
                        Icon = "bi-check-circle",
                        Color = BootstrapBadgeCss.Success
                    });
                }

                if (!historiesByPaymentId.TryGetValue(payment.Id, out var histories))
                    continue;

                foreach (var history in histories.OrderBy(h => h.ActionAt))
                {
                    if (history.Action == PaymentHistoryActions.ContractCreated)
                        continue;
                    var mapped = MapPaymentHistory(history, payment.Method);
                    if (mapped != null)
                        items.Add(mapped);
                }
            }

            foreach (var declaration in declarations)
            {
                items.Add(new ContractTimelineItemDto
                {
                    Date = declaration.CreatedAt,
                    Type = "Documento",
                    Title = $"Declaração {declaration.DeclarationNumber}",
                    Description = $"Versão {declaration.Version} gerada",
                    Icon = "bi-file-earmark-pdf",
                    Color = BootstrapBadgeCss.Info
                });
            }

            var journeyEvents = await _contractHistoryService.GetByContractIdAsync(contract.Id, cancellationToken);
            foreach (var history in journeyEvents)
            {
                var mapped = MapContractHistory(history);
                if (mapped != null)
                    items.Add(mapped);
            }

            var ordered = items.OrderByDescending(t => t.Date).ToList();
            if (pageSize <= 0) pageSize = 5;
            if (page < 1) page = 1;
            var total = ordered.Count;
            var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)pageSize);
            if (page > totalPages) page = totalPages;
            var paged = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Cast<object>()
                .ToList();

            return new ContractTimelinePageDto
            {
                Items = paged,
                TotalItems = total,
                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = pageSize
            };
        }

        private static ContractTimelineItemDto? MapPaymentHistory(PaymentHistory history, PaymentMethod paymentMethod)
        {
            string title, description, icon, color;
            if (history.Action == PaymentHistoryActions.Approved)
            {
                (title, description) = PaymentHistoryDisplayHelper.GetApprovedTimelineCopy(history, paymentMethod);
                icon = "bi-check-circle";
                color = BootstrapBadgeCss.Success;
            }
            else if (history.Action == PaymentHistoryActions.Cancelled)
            {
                title = "Pagamento Cancelado";
                description = history.RejectionReason ?? "Cancelado";
                icon = "bi-x-circle";
                color = BootstrapBadgeCss.Secondary;
            }
            else if (history.Action == PaymentHistoryActions.ReceiptUploaded)
            {
                title = "Comprovante enviado";
                description = "Cliente enviou o comprovante de pagamento. Aguardando validação.";
                icon = "bi-file-earmark-arrow-up";
                color = BootstrapBadgeCss.Info;
            }
            else if (history.Action == PaymentHistoryActions.Rejected)
            {
                title = "Pagamento Reprovado";
                description = $"Reprovado por {history.ActionByUser?.FullName ?? DefaultDisplayStrings.SystemAdminLabel}: {history.RejectionReason}";
                icon = "bi-x-circle";
                color = BootstrapBadgeCss.Danger;
            }
            else
            {
                return null;
            }

            return new ContractTimelineItemDto
            {
                Date = history.ActionAt,
                Type = "Validação",
                Title = title,
                Description = description,
                Icon = icon,
                Color = color
            };
        }

        private static ContractTimelineItemDto? MapContractHistory(ContractHistory history)
        {
            var actor = FormatActor(history.ActorName, history.ActorRole);
            return history.Action switch
            {
                ContractHistoryActions.DocumentsUploaded => new ContractTimelineItemDto
                {
                    Date = history.ActionAt,
                    Type = "Onboarding",
                    Title = "Documentos enviados",
                    Description = string.IsNullOrWhiteSpace(history.Details)
                        ? "Cliente enviou identidade e comprovante de residência."
                        : $"Enviados: {history.Details}.",
                    Icon = "bi-cloud-arrow-up",
                    Color = BootstrapBadgeCss.Info
                },
                ContractHistoryActions.ProfileUpdated => new ContractTimelineItemDto
                {
                    Date = history.ActionAt,
                    Type = "Cadastro",
                    Title = "Dados cadastrais atualizados",
                    Description = string.IsNullOrWhiteSpace(history.Details)
                        ? "Cliente preencheu ou atualizou nome, endereço, telefone e CPF/CNPJ."
                        : $"Cliente atualizou {history.Details.ToLowerInvariant()}.",
                    Icon = "bi-person-lines-fill",
                    Color = BootstrapBadgeCss.Primary
                },
                ContractHistoryActions.DocumentsApproved => new ContractTimelineItemDto
                {
                    Date = history.ActionAt,
                    Type = "Onboarding",
                    Title = "Documentos aprovados",
                    Description = $"Aprovado por {actor}.",
                    Icon = "bi-shield-check",
                    Color = BootstrapBadgeCss.Success
                },
                ContractHistoryActions.DocumentsRejected => new ContractTimelineItemDto
                {
                    Date = history.ActionAt,
                    Type = "Onboarding",
                    Title = "Documentos rejeitados",
                    Description = BuildRejectionDescription(actor, history.RejectionReason),
                    Icon = "bi-shield-x",
                    Color = BootstrapBadgeCss.Danger
                },
                ContractHistoryActions.ContractGenerated => new ContractTimelineItemDto
                {
                    Date = history.ActionAt,
                    Type = "Documento",
                    Title = "Contrato gerado",
                    Description = string.IsNullOrWhiteSpace(history.Details)
                        ? "PDF do contrato gerado."
                        : history.Details,
                    Icon = "bi-file-earmark-pdf",
                    Color = BootstrapBadgeCss.Success
                },
                _ => null
            };
        }

        private static string FormatActor(string? name, string? role)
        {
            var displayName = string.IsNullOrWhiteSpace(name) ? DefaultDisplayStrings.SystemAdminLabel : name.Trim();
            return string.IsNullOrWhiteSpace(role) ? displayName : $"{displayName} ({role})";
        }

        private static string BuildRejectionDescription(string actor, string? reason)
        {
            var text = $"Rejeitado por {actor}.";
            if (!string.IsNullOrWhiteSpace(reason))
                text += $" Motivo: {reason.Trim()}";
            return text;
        }
    }
}
