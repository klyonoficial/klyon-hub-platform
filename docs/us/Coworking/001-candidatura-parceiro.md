# US Coworking 001 — Candidatura e onboarding de parceiro

## Como
dono de coworking interessado em ser parceiro,

## Quero
entender requisitos, criar conta e enviar empresa + endereço + documentos em um wizard,

## Para que
minha candidatura seja analisada sem formulários confusos.

## Jornada implementada

1. LP `/Home/Partner` — benefícios, requisitos, passos
2. `/Auth/Register?role=CoworkingPartner` — role fixo, sem select Cliente/Parceiro
3. Wizard `/Coworking/Register` — 3 etapas: Empresa, Endereço fiscal, Documentos
4. Validação server-side dos 3 documentos obrigatórios
5. Dashboard com checklist de status

## Critérios de aceite

- [ ] Documentos obrigatórios bloqueiam submit se ausentes
- [ ] Endereço fiscal criado no mesmo fluxo do cadastro inicial
- [ ] Fotos limitadas a 2 no processamento (mensagem alinhada à UI)
