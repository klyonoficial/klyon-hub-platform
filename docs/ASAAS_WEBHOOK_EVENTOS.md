# Eventos de Webhook Asaas – O que marcar no painel

Antes de implementar PIX Automático (e para garantir que cobranças atuais funcionem), configure no **Asaas** os eventos que a aplicação deve receber.

**Onde configurar:** Asaas → **Integrações** → **Webhooks** → criar ou editar o webhook que aponta para a sua URL (ex.: `https://seu-dominio.com/api/PaymentWebhook/asaas`).

---

## 1. Cobranças (obrigatório – já usado hoje)

Estes eventos **já são tratados** pelo `PaymentWebhookController` (confirmação, cancelamento, vencimento). Marque:

| Evento | Uso na aplicação |
|--------|-------------------|
| `PAYMENT_RECEIVED` | Cobrança recebida (valor na conta) – PIX, boleto, cartão e **recebimento em dinheiro** (nesse caso o payload vem com `billingType: "RECEIVED_IN_CASH"`). **Confirmar pagamento.** |
| `PAYMENT_CONFIRMED` | Cobrança confirmada (pagamento efetuado, saldo ainda não creditado). **Confirmar pagamento.** |
| `PAYMENT_DELETED` | Cobrança removida. **Marcar pagamento como cancelado.** |
| `PAYMENT_OVERDUE` | Cobrança vencida. **Marcar pagamento como cancelado/vencido.** |

**Nota:** No painel do Asaas pode não existir o evento `PAYMENT_RECEIVED_IN_CASH`. O recebimento em dinheiro é notificado como `PAYMENT_RECEIVED`; a aplicação já trata isso. Se existir `PAYMENT_RECEIVED_IN_CASH_UNDONE` (recebimento em dinheiro desfeito), marque apenas se quiser reverter o status do pagamento quando alguém desfizer o recebimento em dinheiro.

Sem os quatro eventos acima, a confirmação de PIX/boleto/cartão e o tratamento de vencimento não funcionam corretamente.

---

## 2. Cobranças (recomendado – cartão e outros)

Úteis para cartão, estorno e falhas. Vale marcar:

| Evento | Uso na aplicação |
|--------|-------------------|
| `PAYMENT_CREATED` | Nova cobrança criada. Útil para log/auditoria e para vincular cobrança ao pagamento interno. |
| `PAYMENT_CREDIT_CARD_CAPTURE_REFUSED` | Falha na captura do cartão. Permite marcar pagamento como falho e notificar. |
| `PAYMENT_REFUNDED` | Cobrança estornada. Atualizar status do pagamento e, se quiser, da assinatura. |
| `PAYMENT_PARTIALLY_REFUNDED` | Estorno parcial. Atualizar valor/status se a aplicação tratar estorno parcial. |
| `PAYMENT_UPDATED` | Alteração de vencimento ou valor. Sincronizar com o pagamento interno. |

---

## 3. PIX Automático (cobranças anuais autorizadas)

Quando usar PIX Automático (`PixAnnual`), marque também estes eventos no **mesmo** webhook (ou em um dedicado, se preferir):

> **Atenção de nomenclatura:** no Asaas, “Pix Recorrente” é outro produto (transferência agendada pelo pagador). O Klyon usa só **Pix Automático**. Ver `docs/PIX_AUTOMATICO.md`.

### Autorização

| Evento | Uso na aplicação |
|--------|-------------------|
| `PIX_AUTOMATIC_RECURRING_AUTHORIZATION_CREATED` | Autorização criada (QR Code gerado). Log/estado. |
| `PIX_AUTOMATIC_RECURRING_AUTHORIZATION_ACTIVATED` | Primeiro PIX pago e recorrência ativa. Confirmar primeiro pagamento e guardar que a assinatura está em PIX automático. |
| `PIX_AUTOMATIC_RECURRING_AUTHORIZATION_EXPIRED` | Autorização expirou (ex.: não pagou no prazo). Atualizar estado e, se aplicável, permitir novo fluxo. |
| `PIX_AUTOMATIC_RECURRING_AUTHORIZATION_CANCELLED` | Autorização cancelada (pagador ou você). Parar de criar instruções para essa assinatura. |
| `PIX_AUTOMATIC_RECURRING_AUTHORIZATION_REFUSED` | Pagador não autorizou (ex.: fechou sem pagar). Tratar como “não ativado”. |

### Instruções de pagamento (cobranças recorrentes)

| Evento | Uso na aplicação |
|--------|-------------------|
| `PIX_AUTOMATIC_RECURRING_PAYMENT_INSTRUCTION_CREATED` | Instrução de débito criada. Log/vinculação ao pagamento interno. |
| `PIX_AUTOMATIC_RECURRING_PAYMENT_INSTRUCTION_SCHEDULED` | Débito agendado no banco do pagador. |
| `PIX_AUTOMATIC_RECURRING_PAYMENT_INSTRUCTION_REFUSED` | **Falha no débito** (ex.: saldo insuficiente). Marcar pagamento como falho e assinatura como em atraso (PastDue). |
| `PIX_AUTOMATIC_RECURRING_PAYMENT_INSTRUCTION_CANCELLED` | Instrução cancelada. Atualizar status do pagamento/instrução. |

O evento `PIX_AUTOMATIC_RECURRING_ELIGIBILITY_UPDATED` é opcional (mudança de elegibilidade da conta para PIX Automático).

---

## 4. Outros eventos (opcional)

Se a aplicação for tratar chargeback, negativação, dinheiro desfeito ou visualização:

| Evento | Descrição |
|--------|-----------|
| `PAYMENT_RECEIVED_IN_CASH_UNDONE` | Recebimento em dinheiro desfeito (cobrança que tinha sido marcada como paga em dinheiro foi desfeita). |
| `PAYMENT_CHARGEBACK_REQUESTED` | Chargeback solicitado no cartão. |
| `PAYMENT_CHARGEBACK_DISPUTE` | Disputa em análise. |
| `PAYMENT_DUNNING_REQUESTED` | Negativação solicitada (ex.: Serasa). |
| `PAYMENT_DUNNING_RECEIVED` | Negativação recebida. |
| `PAYMENT_BANK_SLIP_VIEWED` | Boleto visualizado. |
| `PAYMENT_CHECKOUT_VIEWED` | Fatura/checkout visualizado. |

---

## Resumo – o que marcar

- **Hoje (cobranças PIX/boleto/cartão/dinheiro):**  
  `PAYMENT_RECEIVED`, `PAYMENT_CONFIRMED`, `PAYMENT_DELETED`, `PAYMENT_OVERDUE`.  
  (Recebimento em dinheiro cai em `PAYMENT_RECEIVED`; não é necessário `PAYMENT_RECEIVED_IN_CASH` se não aparecer na lista.)

- **Recomendado (cartão e estorno):**  
  `PAYMENT_CREATED`, `PAYMENT_CREDIT_CARD_CAPTURE_REFUSED`, `PAYMENT_REFUNDED`, `PAYMENT_PARTIALLY_REFUNDED`, `PAYMENT_UPDATED`.

- **Quando implementar PIX Automático:**  
  Todos os eventos da seção 3 (autorização + instruções de pagamento).

- **Opcional:**  
  Eventos da seção 4, conforme necessidade (chargeback, negativação, visualização).

A aplicação deve sempre responder **200 OK** rapidamente e processar o payload em background se necessário, para não bloquear a fila de webhooks do Asaas.

---

## 5. Status de implementação (`AsaasWebhookHandler`)

Centralizado em `KlyonHubPlatform.Application/Services/AsaasWebhookHandler.cs`, roteado por `PaymentWebhookController` (`/api/PaymentWebhook/asaas`) e `AsaasWebhookController`. Idempotência por estado do Payment/Subscription (e motivo de falha), alinhada ao padrão existente.

| Evento | Ação |
|--------|------|
| **Cobranças** | |
| `PAYMENT_CREATED` | Cria/vincula Payment local para recorrência (`sub_*` / PIX Automático) e **cobranças avulsas** criadas no painel Asaas (resolve cliente via `GET /customers/{id}` → `client_{id}`) |
| `PAYMENT_UPDATED` | Sincroniza DueDate/Amount; confirma se RECEIVED/CONFIRMED; cancela se DELETED; Failed se REFUSED/DUNNING |
| `PAYMENT_RECEIVED` / `CONFIRMED` / `RECEIVED_IN_CASH` | Confirma pagamento local |
| `PAYMENT_DELETED` | Pending → Cancelled |
| `PAYMENT_OVERDUE` | Mantém Pending; PIX pendente dispara regeneração de QR |
| `PAYMENT_REJECTED` | Failed + FailureReason; assinatura PastDue |
| `PAYMENT_CREDIT_CARD_CAPTURE_REFUSED` | Mesmo handler de REJECTED (branch direto no nome do evento) |
| `PAYMENT_REFUNDED` | Status → Refunded |
| `PAYMENT_RESTORED` | Reabre Pending (limpa FailureReason se Failed/Cancelled) |
| `PAYMENT_PARTIALLY_REFUNDED` | **Ignorado** (log only — marcar no painel se/quando tratar estorno parcial) |
| **PIX Automático — autorização** | |
| `PIX_AUTOMATIC_RECURRING_AUTHORIZATION_CREATED` | Log + sync `PixAutomaticAuthorizationId` / QR se assinatura local encontrada |
| `PIX_AUTOMATIC_RECURRING_AUTHORIZATION_ACTIVATED` | Confirma primeiro pagamento Pending |
| `PIX_AUTOMATIC_RECURRING_AUTHORIZATION_REFUSED` | Failed + FailureReason; limpa auth/QR local; assinatura **não** ativada |
| `PIX_AUTOMATIC_RECURRING_AUTHORIZATION_EXPIRED` | Igual REFUSED (motivo “expirada sem pagamento”) |
| `PIX_AUTOMATIC_RECURRING_AUTHORIZATION_CANCELLED` | Limpa auth local; Pending → Cancelled |
| `PIX_AUTOMATIC_RECURRING_ELIGIBILITY_UPDATED` | **Ignorado** (log only) |
| **PIX Automático — instrução** | |
| `PIX_AUTOMATIC_RECURRING_PAYMENT_INSTRUCTION_CREATED` | Sync DueDate; mantém Pending se `paymentId` presente |
| `PIX_AUTOMATIC_RECURRING_PAYMENT_INSTRUCTION_SCHEDULED` | Idem CREATED |
| `PIX_AUTOMATIC_RECURRING_PAYMENT_INSTRUCTION_REFUSED` | Failed + PastDue na assinatura |
| `PIX_AUTOMATIC_RECURRING_PAYMENT_INSTRUCTION_CANCELLED` | Pending → Cancelled |
| **Assinaturas (cartão `sub_*`)** | |
| `SUBSCRIPTION_CREATED` | Log; sync se assinatura local já existir |
| `SUBSCRIPTION_UPDATED` | Sync `NextBillingDate`, `MonthlyAmount`; Cancelled se status INACTIVE/EXPIRED no payload |
| `SUBSCRIPTION_INACTIVATED` / `SUBSCRIPTION_DELETED` | Subscription → Cancelled (**contrato não** cancelado automaticamente) |
| **Opcionais (seção 4)** | |
| `PAYMENT_RECEIVED_IN_CASH_UNDONE`, chargeback, negativação, visualização | **Ignorados** (log only) |

**Resolução PIX autorização:** `authorization.id`, `immediateQrCode.conciliationIdentifier`, `authorization.contractId` (parse do Id interno do contrato), pagamento Pending da assinatura.

**UX cliente:** `PaymentNavigationHelper` — falha de autorização PIX → CTA **“Gerar novo QR”** em PaymentDetails; mensagem em PaymentSummary.

---

## Onde ver autorizações PIX Automático no painel Asaas

Quando a aplicação chama `POST /v3/pix/automatic/authorizations`, a API retorna **200** e `status: "CREATED"` com `id`, `payload` (QR PIX) e `immediateQrCode.expirationDate`. **O envio e a resposta estão corretos** e a aplicação grava o `id` na assinatura (`PixAutomaticAuthorizationId`) e o `payload` no pagamento (`PixCode`) para exibir o QR ao cliente.

No **painel do Asaas (sandbox ou produção)**, as autorizações PIX Automático **podem não aparecer** na tela do cliente (ex.: em "Assinaturas" ou "Cobranças" do cadastro do cliente). O Asaas pode listá-las em outro lugar, por exemplo:

- **Cobranças** (lista geral) — a primeira cobrança pode surgir quando o cliente paga o QR ou quando o Asaas gera a cobrança pendente.
- Menu específico **PIX Automático** ou **Autorizações** (se existir no seu plano/ambiente).

Se você não encontrar a autorização em nenhum lugar, **isso não indica erro na integração**: a API confirmou a criação (`id`, `payload`, `status: "CREATED"`). O fluxo segue assim: (1) a aplicação exibe o QR ao cliente na tela de pagamento; (2) ao pagar, o Asaas envia webhooks (`PAYMENT_CREATED`, `PAYMENT_RECEIVED`, `PIX_AUTOMATIC_RECURRING_AUTHORIZATION_ACTIVATED` etc.); (3) a cobrança pode aparecer em **Cobranças** após o pagamento ou a geração da instrução. Para **simular pagamento PIX no sandbox**, use a API abaixo (ex.: Postman).

---

## Simular pagamento no Sandbox via Postman

### 1. PIX normal (cobrança já criada) — confirmar por ID da cobrança

Quando você já tem o **id da cobrança** no Asaas (ex.: `pay_vmigph2diy588dc4` ou o número que aparece na URL do sandbox, ex.: `13423511` — use o id retornado pela API, não o número da fatura).

- **Método:** `POST`
- **URL:** `https://api-sandbox.asaas.com/v3/sandbox/payment/{id}/confirm`
  - Ex.: `https://api-sandbox.asaas.com/v3/sandbox/payment/13423511/confirm`  
  - **Importante:** use o **id da cobrança** (ex.: `pay_xxxx` ou o id numérico que a API usar). A URL que você citou tinha `?=` no final — remova; o correto é só `/confirm` sem query string.
- **Headers:**
  - `access_token`: sua API Key do Asaas (sandbox)
  - `Content-Type`: `application/json`
- **Body:** vazio ou `{}`

Referência: [Confirmar o pagamento (sandbox)](https://docs.asaas.com/reference/confirmar-pagamento).

---

### 2. PIX Automático — simular pagamento do QR Code

Para a **primeira cobrança** do PIX Automático não existe cobrança criada ainda; o Asaas gera a cobrança quando o QR é “pago”. Use o endpoint **Pagar um QRCode**: você envia o **payload** do QR (o mesmo que a aplicação exibe e que veio na resposta da autorização).

- **Método:** `POST`
- **URL:** `https://api-sandbox.asaas.com/v3/pix/qrCodes/pay`
- **Headers:**
  - `access_token`: sua API Key do Asaas (sandbox)
  - `Content-Type`: `application/json`
- **Body (JSON):**

```json
{
  "qrCode": {
    "payload": "COLE_AQUI_O_PAYLOAD_DA_RESPOSTA_DA_AUTORIZAÇÃO"
  },
  "value": 999.79
}
```

**Onde pegar o payload:**

1. Nos **logs da aplicação** ou na **resposta** do `POST /v3/pix/automatic/authorizations` (campo `payload`), ou  
2. No banco: tabela `Payments`, coluna `PixCode` do pagamento pendente do contrato PIX anual.

**Exemplo** (valor conforme seu contrato, ex.: 999.79):

```json
{
  "qrCode": {
    "payload": "00020101021226810014br.gov.bcb.pix2559pix-h.asaas.com/qr/cob/07180987-8909-4615-afdc-adaff511c3ef5204000053039865802BR5920KLYON COWORKING LTDA6009Cariacica62070503***80810014br.gov.bcb.pix2559pix-h.asaas.com/qr/rec/4d5a20fd-3250-44b3-b7cc-87e6e015f2806304C484"
  },
  "value": 999.79
}
```

Ao chamar esse endpoint, o Asaas **cria a cobrança**, registra o pagamento PIX e deve disparar os webhooks (`PAYMENT_CREATED`, `PAYMENT_RECEIVED`, `PIX_AUTOMATIC_RECURRING_AUTHORIZATION_ACTIVATED`). Sua aplicação recebe os webhooks e confirma o pagamento.

**Vinculação ao cliente no Sandbox:** o endpoint **não aceita parâmetro de cliente** (não existe `customerId` ou `payerId` no body). A cobrança criada fica vinculada ao **customer que está na própria autorização** (o QR foi gerado para esse cliente quando você criou o contrato). No sandbox pode aparecer um registro de “pagador” com nome do recebedor (ex.: “Klyon Coworking Ltda”, que está no payload do QR); a **cobrança** em si deve estar no cliente usado na autorização (o mesmo que “Cliente Teste 10/03” se o contrato foi criado com esse cliente). Para testar na conta do cliente desejado: (1) use um contrato criado **com esse cliente** (faça login como “Cliente Teste 10/03”, escolha PIX anual e crie o contrato); (2) use o payload do QR desse contrato no `POST /v3/pix/qrCodes/pay`; (3) em **Cobranças** (lista geral do sandbox), confira o campo `customer` da cobrança criada — deve ser o `cus_xxx` desse cliente. Se ainda assim a cobrança aparecer em outro cliente, é limitação do sandbox; não há outro endpoint ou parâmetro na API para informar o cliente ao simular o pagamento do QR.

Referência: [Pagar um QRCode](https://docs.asaas.com/reference/pagar-um-qrcode), [Testar pagamento de QRCodes PIX](https://docs.asaas.com/docs/testar-pagamento-de-qrcodes-pix).
