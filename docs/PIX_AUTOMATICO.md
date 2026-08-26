# PIX Automático vs Pix Recorrente (Klyon Hub)



Referência Asaas: [Diferença entre Pix Recorrente e Pix Automático](https://docs.asaas.com/docs/diferen%C3%A7a-entre-pix-recorrente-e-pix-autom%C3%A1tico).



## Decisão de produto



| Modalidade Asaas | O que é | Uso no Klyon |

|------------------|---------|--------------|

| **Pix Automático** | Cobrança recorrente iniciada pelo **recebedor**, após autorização prévia do pagador (Jornada 3: QR = 1º pagamento + consentimento) | **Sim** — planos `PixMonthly` (`frequency=MONTHLY`) e `PixAnnual` (`frequency=ANNUALLY`) |

| **Pix Recorrente** | Transferência agendada pelo **pagador** (API de transfers); o recebedor não controla a cobrança | **Não** — não se aplica a mensalidades/assinaturas do produto |



Na UI e nos textos ao cliente, usar **PIX Automático (mensal)**, **PIX Automático (anual)** e **PIX (pagamento único)**. Evitar o termo genérico “PIX recorrente” / “Pix Recorrente”, que no Asaas nomeia outro produto.



| Opção na UI | `ContractCardPlanType` | Asaas `frequency` | Valor cobrado |

|-------------|------------------------|-------------------|---------------|

| PIX Automático (mensal) | `PixMonthly` | `MONTHLY` | Parcela líquida `(BasePrice * (1+surcharge%)/12)` com cupom — mesma lógica do cartão mensal |

| PIX Automático (anual) | `PixAnnual` | `ANNUALLY` | BasePrice líquido (anual) — mesma lógica do cartão 1x |

| PIX (pagamento único) | `None` | — (POST `/payments`) | BasePrice líquido (anual), sem renovação automática |



Ocultar opções via `Payment.HiddenPaymentOptions` (vírgula): `PixMonthly`, `PixAnnual`, `PIX`, `Card1x`, `CardMonthly`, `Boleto`.



## O que a documentação Asaas exige



1. Criar autorização (`POST /pix/automatic/authorizations`) com QR imediato e `frequency` `MONTHLY` ou `ANNUALLY`.

2. Pagador liquida o 1º PIX → autorização `ACTIVE` (webhook `PIX_AUTOMATIC_RECURRING_AUTHORIZATION_ACTIVATED`).

3. **O primeiro pagamento não cria as cobranças futuras.** A integração deve criar cobranças (`POST /payments` com `pixAutomaticAuthorizationId`).

4. A instrução deve ser criada entre **2 e 10 dias úteis antes do vencimento**.

5. Acompanhar webhooks de autorização e de instrução de pagamento.



## Implementação no repositório



| Peça | Responsabilidade |

|------|------------------|

| `AsaasService.CreatePixAutomaticAuthorizationAsync` | Autorização com `frequency` MONTHLY ou ANNUALLY + QR da 1ª cobrança |

| `AsaasService.CreatePaymentWithPixAutomaticAuthorizationAsync` | Cobranças pós-ativação |

| `SubscriptionService.ProcessPixAutomaticRenewalsAsync` | Seleciona assinaturas `PixMonthly`/`PixAnnual` na janela 2–10 dias úteis e emite a cobrança |

| `PixAutomaticBillingBackgroundService` | HostedService a cada 6h |

| `PaymentWebhookController` | Ativação, liquidação, recusa de instrução |

| `ConfirmPayment` / `ApplySubscriptionAndContractOnPaidAsync` | Avança `NextBillingDate` (+1 mês ou +1 ano conforme o plano) |



Webhooks a marcar no painel Asaas: ver `docs/ASAAS_WEBHOOK_EVENTOS.md` (seção PIX Automático).



## Pendências conhecidas



- Recuperação quando `NextBillingDate` já passou sem cobrança emitida (fora da janela): hoje só há log de warning.

- Eventos de autorização cancelada/expirada/recusada: parcialmente cobertos; pode evoluir o estado da assinatura.


