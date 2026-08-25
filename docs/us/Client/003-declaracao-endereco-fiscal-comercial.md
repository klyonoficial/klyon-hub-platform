# US Client 003 — Declaração por tipo de endereço

## Como
cliente com pagamento confirmado **e documentos de onboarding aprovados** pelo parceiro ou admin,

## Quero
receber uma declaração PDF coerente com o serviço contratado (fiscal ou comercial),

## Para que
eu use o documento correto junto à Receita (fiscal) ou em materiais comerciais (comercial), sem confusão entre os conceitos.

## Regra de negócio

| Serviço (`Contracts.ServiceId` / `Service.Code`) | Declaração gerada |
|--------------------------------------------------|-------------------|
| `FISCAL_ADDRESS_CONTRACTING` | Autorização de **endereço fiscal** — uso cadastral, Receita/Junta/Prefeitura |
| `COMMERCIAL_ADDRESS_CONTRACTING` | Autorização de **endereço comercial** — cartão de visita, site, marketing; **não** para CNPJ |

A geração automática ocorre **somente após aprovação** dos documentos de identidade e comprovante de residência (`Contract.DocumentsApproved == true`), via `IContractDocumentsEnsureService.EnsureAsync` (reusa `AddressDeclarationService` e `DocumentService`).

`PaymentService.ConfirmPaymentAsync` **não** enfileira mais a geração de contrato PDF nem declaração. Webhooks de pagamento também não geram esses documentos.

## Critérios de aceite

- [ ] Pagamento confirmado não gera PDF de contrato nem declaração
- [ ] Contrato fiscal gera PDF com título e cláusulas de domicílio fiscal **depois da aprovação**
- [ ] Contrato comercial gera PDF com título e cláusulas de apoio comercial (sem Receita Federal como destino) **depois da aprovação**
- [ ] Botão **Emitir Declaração** só aparece se: contrato vigente, pagamento confirmado, documentos aprovados e **sem declaração válida para o `ServiceId` atual**
- [ ] Cliente não vê contrato/declaração em `Declarations`, `ContractDetails` ou `PaymentSuccess` antes da aprovação
- [ ] Troca fiscal ↔ comercial no contrato permite nova emissão (nova versão) para o tipo atual
- [ ] Reemissão manual em `/Client/Declarations` respeita as mesmas regras

## Arquivos principais

- `KlyonHubPlatform.Application/Services/AddressDeclarationService.cs`
- `KlyonHubPlatform.Application/Services/ContractDocumentsEnsureService.cs`
- `KlyonHubPlatform.Application/Services/ClientOnboardingDocumentService.cs`
- `KlyonHubPlatform.Domain.Core/Constants/AddressDeclarationLabels.cs`
- `KlyonHubPlatform.Domain.Core/Constants/ServiceCodes.cs`
