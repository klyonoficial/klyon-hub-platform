# User Stories — Klyon Hub Platform

Índice das jornadas de conversão documentadas após implementação do plano de UX.

## Cliente (compra de endereço fiscal)

| ID | Título | Arquivo |
|----|--------|---------|
| 001 | Buscar e contratar endereço fiscal | [Client/001-buscar-contratar-endereco-fiscal.md](Client/001-buscar-contratar-endereco-fiscal.md) |
| 002 | Checkout unificado (onboarding + pagamento) | [Client/002-checkout-onboarding-pagamento.md](Client/002-checkout-onboarding-pagamento.md) |
| 003 | Declaração PDF fiscal vs comercial | [Client/003-declaracao-endereco-fiscal-comercial.md](Client/003-declaracao-endereco-fiscal-comercial.md) |

## Coworking / Parceiro

| ID | Título | Arquivo |
|----|--------|---------|
| 001 | Candidatura e onboarding de parceiro | [Coworking/001-candidatura-parceiro.md](Coworking/001-candidatura-parceiro.md) |
| 002 | Status da parceria até endereço no ar | [Coworking/002-status-parceria-no-ar.md](Coworking/002-status-parceria-no-ar.md) |

## Métricas de funil

Eventos instrumentados via `wwwroot/js/funnel-analytics.js`:

- `funnel_page_view` — visualização de página
- `funnel_cta_click` — clique em CTAs com `data-funnel`
- `funnel_onboarding_submit` — envio do formulário fiscal
- `funnel_partner_wizard_submit` — envio da candidatura de parceiro
