# Simular webhook Asaas (PAYMENT_OVERDUE)

Endpoint que recebe o webhook: **POST** `https://localhost:5001/api/PaymentWebhook/asaas`

## Header obrigatório (validação)

- **Nome:** `asaas-access-token`
- **Valor:** o mesmo valor configurado em **Configurações do Sistema** para a chave **Asaas.WebhookToken**
- Se **Asaas.WebhookToken** não estiver configurado, a API aceita qualquer requisição (útil para testes locais). Em produção, configure o token no painel Asaas (webhook) e use o mesmo valor aqui.

## Corpo (Body)

Content-Type: **application/json**

Use o JSON do evento (ex.: PAYMENT_OVERDUE) exatamente como o Asaas envia. Exemplo:

```json
{
  "id": "evt_071da53af2a9613df04213d0157d6b88&14639649",
  "event": "PAYMENT_OVERDUE",
  "dateCreated": "2026-03-13 20:38:52",
  "account": {
    "id": "77308607-c893-45b4-898a-c0fd1507c74d",
    "ownerId": null
  },
  "payment": {
    "object": "payment",
    "id": "pay_jbiosm8g1qhkhbi0",
    "dateCreated": "2026-03-13",
    "customer": "cus_000007671964",
    "checkoutSession": null,
    "paymentLink": null,
    "value": 799,
    "netValue": 798.01,
    "originalValue": null,
    "interestValue": null,
    "description": "Pagamento de Endereço Fiscal",
    "billingType": "PIX",
    "pixTransaction": null,
    "status": "OVERDUE",
    "dueDate": "2026-03-12",
    "originalDueDate": "2026-03-13",
    "paymentDate": null,
    "clientPaymentDate": null,
    "installmentNumber": null,
    "invoiceUrl": "https://sandbox.asaas.com/i/jbiosm8g1qhkhbi0",
    "invoiceNumber": "13458854",
    "externalReference": "3b00a58c-76c9-4d20-916f-944b522aa11f",
    "deleted": false,
    "anticipated": false,
    "anticipable": false,
    "creditDate": null,
    "estimatedCreditDate": null,
    "transactionReceiptUrl": null,
    "nossoNumero": null,
    "bankSlipUrl": null,
    "lastInvoiceViewedDate": null,
    "lastBankSlipViewedDate": null,
    "discount": { "value": 0, "limitDate": null, "dueDateLimitDays": 0, "type": "FIXED" },
    "fine": { "value": 0, "type": "FIXED" },
    "interest": { "value": 0, "type": "PERCENTAGE" },
    "postalService": false,
    "escrow": null,
    "refunds": null
  }
}
```

Para o backend encontrar o pagamento e gerar novo PIX, na base deve existir um pagamento com:
- **ExternalPaymentId** = `payment.id` do JSON (ex.: `pay_jbiosm8g1qhkhbi0`), ou
- **ExternalPaymentId** = `payment.externalReference` (ex.: `3b00a58c-76c9-4d20-916f-944b522aa11f`).

---

## cURL

Substitua `SEU_WEBHOOK_TOKEN` pelo valor de **Asaas.WebhookToken** (ou omita o header se não estiver configurado).

```bash
curl -X POST "https://localhost:5001/api/PaymentWebhook/asaas" \
  -H "Content-Type: application/json" \
  -H "asaas-access-token: SEU_WEBHOOK_TOKEN" \
  -d '{
  "id": "evt_071da53af2a9613df04213d0157d6b88&14639649",
  "event": "PAYMENT_OVERDUE",
  "dateCreated": "2026-03-13 20:38:52",
  "account": { "id": "77308607-c893-45b4-898a-c0fd1507c74d", "ownerId": null },
  "payment": {
    "object": "payment",
    "id": "pay_jbiosm8g1qhkhbi0",
    "dateCreated": "2026-03-13",
    "customer": "cus_000007671964",
    "value": 799,
    "netValue": 798.01,
    "description": "Pagamento de Endereço Fiscal",
    "billingType": "PIX",
    "status": "OVERDUE",
    "dueDate": "2026-03-12",
    "originalDueDate": "2026-03-13",
    "invoiceUrl": "https://sandbox.asaas.com/i/jbiosm8g1qhkhbi0",
    "invoiceNumber": "13458854",
    "externalReference": "3b00a58c-76c9-4d20-916f-944b522aa11f",
    "deleted": false,
    "anticipated": false,
    "anticipable": false,
    "discount": { "value": 0, "limitDate": null, "dueDateLimitDays": 0, "type": "FIXED" },
    "fine": { "value": 0, "type": "FIXED" },
    "interest": { "value": 0, "type": "PERCENTAGE" },
    "postalService": false,
    "escrow": null,
    "refunds": null
  }
}'
```

Se usar HTTP (não HTTPS) na dev:

```bash
curl -X POST "http://localhost:5000/api/PaymentWebhook/asaas" \
  -H "Content-Type: application/json" \
  -H "asaas-access-token: SEU_WEBHOOK_TOKEN" \
  -d @payload.json
```

(Coloque o JSON completo em `payload.json` na mesma pasta.)

---

## Postman

1. **Method:** POST  
2. **URL:** `https://localhost:5001/api/PaymentWebhook/asaas` (ou `http://localhost:5000/...` se for o caso).
3. **Headers:**
   - `Content-Type`: `application/json`
   - `asaas-access-token`: valor de **Asaas.WebhookToken** (ou deixe em branco se não estiver configurado).
4. **Body:** escolha **raw** e **JSON**, e cole o JSON do evento (o bloco completo com `id`, `event`, `payment`, etc.).

Resposta esperada: **200 OK** (corpo vazio ou mensagem de sucesso). Se retornar **401**, o header `asaas-access-token` não confere com o **Asaas.WebhookToken** configurado.
