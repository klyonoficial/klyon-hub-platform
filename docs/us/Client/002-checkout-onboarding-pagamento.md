# US Client 002 — Checkout unificado (onboarding + pagamento)

## Como
cliente no checkout,

## Quero
preencher dados mínimos e pagar preferencialmente via PIX,

## Para que
eu finalize a compra na mesma sessão e avance para o envio de documentos.

## Critérios de aceite

- [ ] Conta criada no submit do onboarding (senha por e-mail depois)
- [ ] PIX destacado como método recomendado na tela de pagamento
- [ ] Redirecionamento automático para `/Client/PaymentSuccess` quando pago ou aguardando validação
- [ ] `CreateContract` / FiscalOnboarding / **Continuar e pagar** exigem **somente** os dados da tela (tipo, CPF/CNPJ, e-mail, telefone, termos). Não exigem nome completo, endereço nem perfil completo; o pedido interno é criado e o fluxo segue para o pagamento.
- [ ] Se o pagamento estiver **confirmado**, o cliente é enviado para `/Client/Profile` quando os dados do contratante estiverem incompletos, ou para `/Client/UploadDocuments` (identidade + comprovante de residência) — o contrato PDF e a declaração **não** são gerados neste momento
- [ ] Polling `GetPaymentStatus` retorna `redirectUrl` para Profile (se cadastro incompleto) ou upload quando pago e documentos ainda não aprovados
- [ ] `EnsureAsync` / geração de PDF recusa com `ClientPartyIncomplete` se faltar dado essencial; esse gate **não** roda na criação do pedido
