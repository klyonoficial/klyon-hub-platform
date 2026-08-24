# US Client 002 — Checkout unificado (onboarding + pagamento)

## Como
cliente no checkout,

## Quero
preencher dados mínimos e pagar preferencialmente via PIX,

## Para que
eu finalize a compra na mesma sessão.

## Critérios de aceite

- [ ] Conta criada no submit do onboarding (senha por e-mail depois)
- [ ] PIX destacado como método recomendado na tela de pagamento
- [ ] Redirecionamento automático para `/Client/PaymentSuccess` quando pago ou aguardando validação
- [ ] Polling `GetPaymentStatus` retorna `redirectUrl` quando aplicável
