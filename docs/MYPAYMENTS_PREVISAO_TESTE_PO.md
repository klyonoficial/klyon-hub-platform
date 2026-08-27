# Guia de teste PO – Previsibilidade de pagamentos (MyPayments)

Este guia descreve como validar a **próxima parcela prevista** em `/Client/MyPayments` e o fluxo completo de renovação via webhook Asaas.

**Referências técnicas:** `docs/WEBHOOK_ASAAS_SIMULACAO.md`, `docs/ASAAS_WEBHOOK_EVENTOS.md`, `docs/CONFIGURAR_PAGAMENTO.md`

---

## Cenário base (dados do PO)

| Campo | Valor esperado |
|-------|----------------|
| Contrato | `CT-2026-000002` |
| Valor | R$ 85,00 |
| Método | Cartão (assinatura mensal ou anual) |
| Status atual | **Pago** |
| Vencimento da parcela paga | 26/08/2026 (ou data do contrato) |
| Pago em | 26/08/2026 19:20 |
| Próximo vencimento previsto | **26/08/2027** (plano anual `Card1x`) ou +1 mês se `CardMonthly` |

---

## O que foi implementado

Quando o cliente tem **assinatura recorrente ativa** (`CardMonthly`, `Card1x`, `PixMonthly`, `PixAnnual`) e **não existe cobrança futura real** no banco (Pending/Processing/AwaitingValidation), a tela exibe uma linha extra com:

| Coluna | Comportamento |
|--------|---------------|
| Status | Badge **Previsto** (cartão/PIX/boleto assinatura) ou **Aguardando emissão** (PIX Automático) |
| Vencimento | Último pagamento **pago** + 1 mês (`Monthly`) ou + 1 ano (`Yearly`), conforme `SnapshotBillingCycle` → `CardPlanType` → `Subscription.BillingCycle` |
| Valor / método / copy | Do ciclo vigente do pagamento pago (ex.: cartão anual R$ 675, texto **anual**); não usar cobrança cancelada residual nem sub Asaas PIX mensal órfã |
| Ações | "Em breve" (sem link até existir cobrança real) |

Quando o Asaas emite a cobrança (`PAYMENT_CREATED`), a linha prevista **some** e aparece a cobrança real com status **Aguardando Pagamento**.

---

## Pré-requisitos

1. Ambiente com deploy ou `dotnet run --project KlyonHubPlatform.Api` + banco acessível.
2. Webhook Asaas apontando para `https://<host>/api/PaymentWebhook/asaas`.
3. Token configurado em **Configurações → Asaas.WebhookToken** (mesmo valor do header `asaas-access-token`).
4. Cliente logado dono do contrato `CT-2026-000002`.
5. Sandbox Asaas com assinatura `sub_*` vinculada ao contrato.

---

## Passo 1 – Validar estado atual (só parcela paga + prevista)

1. Acesse **Cobranças de Endereço Fiscal** (`/Client/MyPayments`).
2. Confirme a linha **Pago** do contrato `CT-2026-000002` (R$ 85,00, Cartão).
3. Confirme linha adicional com badge **Previsto** e vencimento **26/08/2027** (ou próximo ciclo conforme plano).
4. Cards de resumo (Pendentes / Processando / Pagas) **não** devem incluir o valor previsto — apenas cobranças reais.

**Critério de aceite:** PO vê previsibilidade imediata após pagamento, sem esperar o Asaas emitir a próxima fatura.

---

## Passo 2 – Conferir dados no banco (opcional, suporte/dev)

```sql
-- Contrato e assinatura
SELECT c.ContractNumber, c.Status, c.CardPlanType, s.NextBillingDate, s.ExternalSubscriptionId, s.Status
FROM Contracts c
JOIN Subscriptions s ON s.ContractId = c.Id
WHERE c.ContractNumber = 'CT-2026-000002';

-- Pagamentos do contrato
SELECT p.Id, p.Status, p.Amount, p.DueDate, p.PaidAt, p.ExternalPaymentId
FROM Payments p
JOIN Subscriptions s ON p.SubscriptionId = s.Id
JOIN Contracts c ON s.ContractId = c.Id
WHERE c.ContractNumber = 'CT-2026-000002'
ORDER BY p.DueDate DESC;
```

Esperado antes da emissão Asaas:
- 1 pagamento `Paid`
- 0 pagamentos `Pending` com vencimento futuro
- `Subscriptions.NextBillingDate` = data da próxima parcela

---

## Passo 3 – Ajustar nextDueDate no sandbox Asaas

1. Painel Asaas → **Assinaturas** → localizar assinatura `sub_*` do contrato.
2. Altere **Próximo vencimento** para a data desejada (ex.: 26/08/2027).
3. Opcional: no banco, alinhe `Subscriptions.NextBillingDate` à mesma data.

> Para cartão mensal/anual, o Asaas gera a cobrança automaticamente conforme ciclo da assinatura. A data alterada deve refletir na próxima emissão.

---

## Passo 4 – Simular `PAYMENT_CREATED` (próxima cobrança emitida)

Substitua os placeholders:
- `SEU_WEBHOOK_TOKEN` → valor de `Asaas.WebhookToken`
- `sub_XXXXX` → `ExternalSubscriptionId` da assinatura
- `pay_NOVO_ID` → ID fictício ou real da cobrança Asaas
- `799` → valor em centavos (85,00 → `85`)

```bash
curl -X POST "https://localhost:5001/api/PaymentWebhook/asaas" \
  -H "Content-Type: application/json" \
  -H "asaas-access-token: SEU_WEBHOOK_TOKEN" \
  -d '{
  "id": "evt_test_payment_created",
  "event": "PAYMENT_CREATED",
  "dateCreated": "2026-08-26 19:30:00",
  "payment": {
    "object": "payment",
    "id": "pay_NOVO_ID",
    "subscription": "sub_XXXXX",
    "customer": "cus_XXXXX",
    "value": 85,
    "billingType": "CREDIT_CARD",
    "status": "PENDING",
    "dueDate": "2027-08-26",
    "description": "Renovação CT-2026-000002"
  }
}'
```

**Resultado esperado:**
- Novo `Payment` local com status **Pending**
- `/Client/MyPayments`: linha **Previsto** desaparece; aparece **Aguardando Pagamento** com botão **Ver Detalhes**

> `externalReference` vazio no payload é suportado: o handler usa `payment.subscription` (`sub_*`) + `payment.id` (`pay_*`).

---

## Passo 5 – Simular vencimento (`PAYMENT_OVERDUE`) — cartão assinatura

Use o **mesmo** `pay_*` do Passo 4 (ex.: `pay_pw6f5y78jcjkn5a0` no teste PO). No sandbox, force o vencimento antes da data original.

```bash
curl -X POST "https://localhost:5001/api/PaymentWebhook/asaas" \
  -H "Content-Type: application/json" \
  -H "asaas-access-token: SEU_WEBHOOK_TOKEN" \
  -d '{
  "id": "evt_test_payment_overdue",
  "event": "PAYMENT_OVERDUE",
  "dateCreated": "2026-08-26 20:27:00",
  "payment": {
    "object": "payment",
    "id": "pay_NOVO_ID",
    "subscription": "sub_XXXXX",
    "customer": "cus_XXXXX",
    "value": 85,
    "billingType": "CREDIT_CARD",
    "status": "OVERDUE",
    "dueDate": "2026-08-25",
    "originalDueDate": "2026-09-26",
    "description": "Renovação CT-2026-000002"
  }
}'
```

**Resultado esperado (comportamento correto):**
- Mesmo `Payment` do Passo 4 permanece **Pending** (não vira **Cancelado**)
- `DueDate` atualizado para **25/08/2026**
- `/Client/MyPayments`: **Previsto** some; badge **Vencido** (vencimento passado) + **Ver Detalhes**
- Reenvio idempotente do mesmo `pay_*` atualiza o registro existente (sem duplicata)

**Bug corrigido (comportamento anterior):** status **Cancelado** + linha **Previsto** ainda visível.

---

## Passo 5b – Alterar vencimento no Asaas (`PAYMENT_UPDATED`)

Cenário PO: cobrança **Vencida** (`pay_pw6f5y78jcjkn5a0`, contrato `CT-2026-000002`) com `DueDate` 25/08/2026; PO altera vencimento no painel Asaas para **27/08/2026**.

```bash
curl -X POST "https://localhost:5001/api/PaymentWebhook/asaas" \
  -H "Content-Type: application/json" \
  -H "asaas-access-token: SEU_WEBHOOK_TOKEN" \
  -d '{
  "id": "evt_test_payment_updated",
  "event": "PAYMENT_UPDATED",
  "dateCreated": "2026-08-26 21:00:00",
  "payment": {
    "object": "payment",
    "id": "pay_pw6f5y78jcjkn5a0",
    "subscription": "sub_qh2xu0r9b9dwbbmc",
    "customer": "cus_XXXXX",
    "value": 85,
    "billingType": "CREDIT_CARD",
    "status": "PENDING",
    "dueDate": "2026-08-27",
    "description": "Renovação CT-2026-000002"
  }
}'
```

**Resultado esperado:**
- Mesmo `Payment` local (ex.: Id 178) permanece **Pending**
- `DueDate` atualizado para **27/08/2026**
- `UpdatedAt` preenchido
- `/Client/MyPayments`: badge **Aguardando Pagamento** (não **Vencido**, pois vencimento ≥ hoje)
- Se `value` mudou no Asaas, `Amount` local também sincroniza

**Mapeamento de status Asaas → local:**

| Status Asaas | Status local | UI (Pending) |
|--------------|--------------|--------------|
| `PENDING` | Pending | Aguardando Pagamento se dueDate ≥ hoje; Vencido se passado |
| `OVERDUE` | Pending | Vencido se dueDate passado |
| `RECEIVED` / `CONFIRMED` | Paid (via ConfirmPaymentAsync) | Pago |
| `DELETED` | Cancelled | Cancelado |

> Se o pagamento local não existir, o handler cria via `subscription` (`sub_*`), igual ao `PAYMENT_CREATED`.

---

## Passo 6 – Confirmar pagamento (`PAYMENT_RECEIVED`)

```bash
curl -X POST "https://localhost:5001/api/PaymentWebhook/asaas" \
  -H "Content-Type: application/json" \
  -H "asaas-access-token: SEU_WEBHOOK_TOKEN" \
  -d '{
  "id": "evt_test_payment_received",
  "event": "PAYMENT_RECEIVED",
  "dateCreated": "2027-08-26 10:00:00",
  "payment": {
    "object": "payment",
    "id": "pay_NOVO_ID",
    "subscription": "sub_XXXXX",
    "value": 85,
    "billingType": "CREDIT_CARD",
    "status": "RECEIVED",
    "dueDate": "2027-08-26",
    "paymentDate": "2027-08-26",
    "clientPaymentDate": "2027-08-26"
  }
}'
```

**Resultado esperado:**
- Pagamento marcado **Pago** com `PaidAt` preenchido
- `Subscription.NextBillingDate` avança +1 ano (Card1x/PixAnnual) ou +1 mês (CardMonthly/PixMonthly)
- Nova linha **Previsto** para o ciclo seguinte (ex.: 26/08/2028)

---

## Passo 7 – Fluxo PIX Automático (PixMonthly / PixAnnual)

| Etapa | Quem emite | O que o PO vê |
|-------|------------|---------------|
| Entre ciclos | Ninguém ainda | **Aguardando emissão** + texto explicativo (2–10 dias úteis antes) |
| Worker / Asaas emite | `PAYMENT_CREATED` | **Aguardando Pagamento** (cobrança real) |
| Cliente paga | `PAYMENT_RECEIVED` | **Pago** + nova previsão |

Para forçar emissão em dev, aguarde o worker `PixAutomaticBillingBackgroundService` ou simule webhook conforme Passos 4–5 com `"billingType": "PIX"`.

---

## Matriz de validação rápida

| Situação | MyPayments deve mostrar |
|----------|-------------------------|
| 1 pago, sem pending futuro | Pago + **Previsto** |
| Pending com mesma DueDate | Só **Aguardando Pagamento** (sem duplicar) |
| Após PAYMENT_CREATED | Pending real (sem Previsto) |
| Após PAYMENT_OVERDUE (cartão) | **Vencido** / Pending + DueDate atualizado (sem Previsto, sem Cancelado) |
| Após PAYMENT_UPDATED (novo vencimento) | **Aguardando Pagamento** se dueDate futuro; DueDate/Amount sincronizados |
| Após PAYMENT_RECEIVED | Pago + novo **Previsto** |
| Contrato cancelado/expirado | Sem previsão |
| Plano não recorrente (PIX único, boleto) | Sem previsão |

---

## Troubleshooting

| Problema | Verificação |
|----------|-------------|
| Não aparece Previsto | Contrato `Active`? Assinatura `Active`? Plano recorrente? Já existe Pending futuro? |
| Previsto e Pending juntos | DueDate diferente no banco vs previsão — alinhar `NextBillingDate` |
| Previsto mensal/PIX após cartão anual | Previsão local usa **pagamento pago** (`SnapshotBillingCycle` / método / `Amount`) + `CardPlanType`, não `NextBillingDate` de sub Asaas residual. Se `sub_*` no Asaas ainda for PIX `MONTHLY` órfã após troca para cartão YEARLY, cancelar/atualizar a assinatura no painel Asaas (sandbox) para alinhar o gateway; a UI MyPayments já deve mostrar **+1 ano**, copy **anual** e método **Cartão**. |
| Webhook 401 | Header `asaas-access-token` ≠ `Asaas.WebhookToken` |
| PAYMENT_CREATED não cria pagamento | `subscription` no JSON deve ser `sub_*` existente em `Subscriptions.ExternalSubscriptionId` |
| OVERDUE virou Cancelado | Corrigido: cartão assinatura permanece Pending; Cancelado só em PAYMENT_DELETED |
| PAYMENT_UPDATED não atualiza DueDate | Corrigido: handler sincroniza DueDate, Amount e Status |
| Valor divergente | Conferir valor do último `Paid` e `Subscription.MonthlyAmount` vs ciclo (anual ≠ parcela mensal) |

---

## Checklist PO (aceite)

- [ ] Após pagamento inicial, MyPayments mostra parcela paga **e** próxima prevista
- [ ] Badge **Previsto** / **Aguardando emissão** distinto de **Aguardando Pagamento**
- [ ] Cards de totais ignoram parcelas previstas
- [ ] Simulação `PAYMENT_CREATED` substitui previsto por cobrança real
- [ ] Simulação `PAYMENT_OVERDUE` mantém Pending e exibe Vencido quando dueDate passado
- [ ] Simulação `PAYMENT_UPDATED` sincroniza vencimento alterado no Asaas
- [ ] Simulação `PAYMENT_RECEIVED` confirma pagamento e gera nova previsão
- [ ] Sem duplicata quando já existe Pending na mesma data de vencimento
