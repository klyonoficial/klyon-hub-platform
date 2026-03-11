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

## 3. PIX Automático (para a implementação de PIX recorrente)

Quando for implementar PIX Automático, marque também estes eventos no **mesmo** webhook (ou em um dedicado, se preferir):

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
