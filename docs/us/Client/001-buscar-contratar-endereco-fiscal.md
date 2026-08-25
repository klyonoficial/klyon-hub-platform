# US Client 001 — Buscar e contratar endereço fiscal

## Como
cliente em busca de endereço fiscal para CNPJ,

## Quero
escolher o endereço do parceiro, confirmar a contratação e pagar online,

## Para que
eu registre minha empresa sem burocracia desnecessária.

## Jornada implementada

1. Home (`/`) — se houver **um único** endereço publicado e disponível, redireciona (302) para a página do parceiro (`/Client/AddressDetail?coworkingAddressId={id}` resolvido no banco). Se houver 0 ou 2+ endereços, permanece a LP atual.
2. Página do parceiro (listing): galeria, preço, o que está incluso, documentos de confiança (alvarás/CNPJ) e CTA **Contratar**.
3. Onboarding (`/Client/FiscalOnboarding`) — CPF, CNPJ opcional, e-mail, telefone, LGPD — se anônimo ou perfil incompleto.
4. Pagamento PIX-first (`/Client/PaymentDetails`)
5. Confirmação (`/Client/PaymentSuccess`) → se faltar nome/endereço do contratante, `/Client/Profile` (US 005) → em seguida **upload de documentos** (US 004), não geração imediata de contrato/declaração.

## Critérios de aceite

- [ ] Com 1 endereço publicado, `https://localhost:5001/` abre a página do parceiro (redirect AddressDetail correto, sem hardcode de id)
- [ ] Com 0 ou 2+ endereços, a home atual permanece
- [ ] Anônimo conclui compra sem cadastro prévio em `/Auth/Register`
- [ ] Logado com perfil incompleto usa onboarding (não `RegisterClient`) no meio da compra
- [ ] Endereço fiscal **não** bloqueia CNAE de indústria/fabricação (nem varejo/logística): a empresa pode usar o endereço só para notas e cadastros na prefeitura, com operacional em outro local
- [ ] Ao sair do campo CNPJ (blur), valida situação cadastral na base pública — sem recusar por CNAE
- [ ] Endereço comercial é alternativa secundária, não CTA duplicado
