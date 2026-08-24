# US Client 003 — Declaração por tipo de endereço

## Como
cliente com contrato ativo e pagamento confirmado,

## Quero
receber uma declaração PDF coerente com o serviço contratado (fiscal ou comercial),

## Para que
eu use o documento correto junto à Receita (fiscal) ou em materiais comerciais (comercial), sem confusão entre os conceitos.

## Regra de negócio

| Serviço (`Contracts.ServiceId` / `Service.Code`) | Declaração gerada |
|--------------------------------------------------|-------------------|
| `FISCAL_ADDRESS_CONTRACTING` | Autorização de **endereço fiscal** — uso cadastral, Receita/Junta/Prefeitura |
| `COMMERCIAL_ADDRESS_CONTRACTING` | Autorização de **endereço comercial** — cartão de visita, site, marketing; **não** para CNPJ |

A geração automática ocorre no primeiro pagamento confirmado (`PaymentService.ConfirmPaymentAsync`), via `AddressDeclarationService.GenerateDeclarationAsync`.

## Critérios de aceite

- [ ] Contrato fiscal gera PDF com título e cláusulas de domicílio fiscal
- [ ] Contrato comercial gera PDF com título e cláusulas de apoio comercial (sem Receita Federal como destino)
- [ ] Botão **Emitir Declaração** só aparece se: contrato vigente, pagamento confirmado e **sem declaração válida para o `ServiceId` atual**
- [ ] Troca fiscal ↔ comercial no contrato permite nova emissão (nova versão) para o tipo atual
- [ ] Reemissão manual em `/Client/Declarations` respeita as mesmas regras

## Arquivos principais

- `KlyonHubPlatform.Application/Services/AddressDeclarationService.cs`
- `KlyonHubPlatform.Domain.Core/Constants/AddressDeclarationLabels.cs`
- `KlyonHubPlatform.Domain.Core/Constants/ServiceCodes.cs`
