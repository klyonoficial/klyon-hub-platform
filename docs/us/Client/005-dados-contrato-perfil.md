# US Client 005 — Dados do contratante numa única tela

## Como
cliente que já criou a conta (checkout) e precisa do contrato/declaração sem dados lixo,

## Quero
preencher nome, documento, endereço, telefone e e-mail **numa única tela**,

## Para que
o PDF do contrato não saia com login no lugar do nome, endereço vazio ou telefone sem máscara na tela.

## Jornada

1. Checkout (`/Client/FiscalOnboarding`) cria a conta com CPF/CNPJ, e-mail e telefone — **não** grava o prefixo do e-mail como Nome Completo.
2. Pagamento confirmado → se os dados do contratante estiverem incompletos, redireciona para `/Client/Profile` (Dados para o contrato).
3. O cliente completa **todos** os campos obrigatórios nesse formulário único e salva (`SaveProfileAsync`).
4. Em seguida segue para `/Client/UploadDocuments` (US 004). O PDF só é gerado depois da aprovação **e** com `ClientContractPartyIsComplete`.

## Campos obrigatórios

- Pessoa física: nome completo (nome e sobrenome; **não** login/prefixo de e-mail), CPF, endereço completo (CEP, logradouro, número, bairro, cidade, UF), telefone com máscara, e-mail (somente leitura).
- Pessoa jurídica: razão social, CNPJ, endereço completo, telefone, e-mail. Nome completo do responsável é opcional, mas se informado não pode ser o prefixo do e-mail.

## Critérios de aceite

- [ ] Cadastro/onboarding **não** grava `email.Split('@')[0]` em `ContactName` / `User.FullName`; campo fica em branco até o preenchimento real.
- [ ] Header, dashboard e título do perfil mostram o **e-mail** quando o nome estiver vazio ou for placeholder de login; quando preenchido, mostram o nome.
- [ ] Validação na Application (FluentValidation + `ClientContractPartyRules`); `ClientController.UpdateProfile` só orquestra.
- [ ] Ponto único de persistência: `IClientProfileService.SaveProfileAsync` (chama `IClientAppService.SaveClientProfileAsync` / `IContractHistoryService.RecordProfileUpdatedForClientAsync`).
- [ ] `IClientProfileService.ClientContractPartyIsComplete` / `GetContractPartyStatusByContractIdAsync` disponíveis para outros fluxos.
- [ ] `ContractDocumentsEnsureService.EnsureAsync` **não** gera PDF se os dados faltarem; devolve erro claro (`ClientPartyIncomplete`).
- [ ] Máscara de telefone na tela (`masks.js` em `#ContactPhone`).

## Fora de escopo

- Renderer PDF / templates HTML do contrato.
- Telas do parceiro (Inscrição Imobiliária, Representante Legal).
- Instrumentação extra de histórico além do `SaveProfileAsync` já existente.
