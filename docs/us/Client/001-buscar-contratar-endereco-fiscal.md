# US Client 001 — Buscar e contratar endereço fiscal

## Como
cliente em busca de endereço fiscal para CNPJ,

## Quero
escolher cidade e endereço, confirmar a contratação e pagar online,

## Para que
eu registre minha empresa sem burocracia desnecessária.

## Jornada implementada

1. Home ou LP Fiscal → **Ver endereços** (`/Client/ChooseLocation`)
2. Lista filtrada por estado/cidade
3. Detalhe do endereço → CTA primário **Contratar endereço fiscal**
4. Onboarding (`/Client/FiscalOnboarding`) — CPF, CNPJ opcional, e-mail, telefone, LGPD
5. Pagamento PIX-first (`/Client/PaymentDetails`)
6. Confirmação (`/Client/PaymentSuccess`)

## Critérios de aceite

- [ ] Anônimo conclui compra sem cadastro prévio em `/Auth/Register`
- [ ] Logado com perfil incompleto usa onboarding (não `RegisterClient`) no meio da compra
- [ ] Validação de CNAE ocorre ao sair do campo CNPJ (blur)
- [ ] Endereço comercial é alternativa secundária, não CTA duplicado
