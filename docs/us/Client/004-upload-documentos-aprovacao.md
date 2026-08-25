# US Client 004 — Upload de documentos e aprovação

## Como
cliente que já pagou o endereço fiscal (ou comercial no mesmo checkout),

## Quero
enviar documentos de identidade e o comprovante de residência, e acompanhar a análise do parceiro ou da Klyon,

## Para que
meu contrato e a declaração sejam gerados só depois da conferência dos documentos.

## Jornada

1. Pagamento confirmado → se faltar dado essencial do contratante, `/Client/Profile` (US 005); depois `/Client/UploadDocuments?contractId={id}`
2. Upload obrigatório em **um único envio** (botão **Enviar documentos**):
   - identidade (caminhos exclusivos):
     - **Caminho A:** RG **e** CPF
       - RG exige **dois arquivos**: frente e verso
       - CPF é um arquivo
     - **Caminho B:** somente a **CNH** (um arquivo; substitui RG e CPF porque a CNH já contém os dois)
     - Não aceitar só um lado do RG, só CPF, nem exigir CNH + RG + CPF juntos
   - comprovante de residência (obrigatório nos dois caminhos)
   - o conjunto incompleto não é persistido: o botão fica desabilitado e a Application rejeita o POST
3. Arquivos (PDF, JPG, PNG; **soma máxima de 15 MB** no envio — identidade + comprovante) vão para Oracle Object Storage (`client-onboarding/{clientId}/{contractId}`). A tela valida a soma **antes** do POST; a Application rejeita no `UploadSetAsync`; o Nginx de produção usa `client_max_body_size 20m` (folga) e `error_page 413` para `/errors/413.html`, sem a página branca padrão.
4. Estado **Aguardando aprovação** só quando a identidade do caminho escolhido e o comprovante estiverem enviados
5. Parceiro (Coworking) **ou** Admin aprova/rejeita em `/Parceiro/OnboardingDocuments` ou `/Admin/OnboardingDocuments`
6. Rejeição informa o motivo e permite reenvio
7. Aprovação grava auditoria (`DocumentsReviewedByUserId`, `DocumentsReviewedAt`) e chama `IContractDocumentsEnsureService.EnsureAsync` (contrato PDF + declaração) **somente se** `ClientContractPartyIsComplete`; caso contrário o Ensure devolve erro claro e não gera PDF com nome/login lixo.
8. O **Histórico do contrato** (`/Client/ContractDetails/{id}` e tela equivalente do parceiro) registra na mesma lista paginada:
   - cliente enviou os documentos (timestamp e tipos: RG frente/verso, CPF, CNH, comprovante)
   - cliente preencheu/atualizou dados cadastrais essenciais (nome, endereço, telefone, CPF/CNPJ) ao salvar o perfil
   - admin ou parceiro aprovou (nome, papel e data)
   - admin ou parceiro rejeitou (nome, papel, data e motivo)
   Eventos são gravados na Application (`ClientOnboardingDocumentService`, `ClientProfileService.SaveProfileAsync` / `ClientAppService.SaveClientProfileAsync`) via `IContractHistoryService` e exibidos pelo `IContractTimelineService`.

## Critérios de aceite

- [ ] Pós-pagamento o cliente completa os dados do contratante (se faltarem) e cai na tela de upload, não em “contrato ativo / ver declaração”
- [ ] Identidade completa = (RG frente + RG verso + CPF) **ou** somente CNH; comprovante de residência sempre obrigatório
- [ ] Um único botão envia identidade **e** comprovante; sem o conjunto do caminho escolhido o POST é rejeitado e nada é gravado pela metade
- [ ] Reenvio após rejeição usa o mesmo botão, com o motivo visível, e ainda exige o conjunto completo
- [ ] Se faltar a frente: “Envie a frente do RG”; se faltar o verso: “Envie o verso do RG”
- [ ] Validação da regra na Application (`ClientOnboardingDocumentRules`); controller só orquestra
- [ ] MIME e tamanho validados na Application (até 15 MB **por arquivo** e 15 MB **no total** do envio)
- [ ] Se a soma passar de 15 MB, a tela não envia o POST (banner/mensagem) e o loading não fica preso
- [ ] Nginx de produção aceita o POST (20m) e, se ainda assim devolver 413, mostra página amigável (`/errors/413.html` ou `/Home/RequestEntityTooLarge`)
- [ ] Documento de outro cliente/parceiro não vaza (autorização por ClientId, CoworkingId ou Admin)
- [ ] Parceiro ou admin pode aprovar/rejeitar; rejeição permite reenvio com o motivo visível
- [ ] Telas de revisão mostram os dois lados do RG (`RG (frente)` e `RG (verso)`)
- [ ] Aprovado gera contrato + declaração com o mesmo renderer atual
- [ ] Pagamento/webhook não chama `EnsureAsync` antes da aprovação (`Contract.DocumentsApproved`)
- [ ] Histórico do contrato inclui envio de documentos, atualização cadastral, aprovação e rejeição (com motivo), no mesmo padrão visual e paginação já existentes

## Modelo

Entidade `ClientOnboardingDocument`: ClientId, ContractId, Type (`IdentityRgFront`, `IdentityRgBack`, `IdentityCpf`, `IdentityCnh`, `ProofOfResidence`), FilePath (storage key), Status (Pending/Approved/Rejected), ReviewedByUserId, ReviewedAt, RejectionReason.

Flag `Contract.DocumentsApproved` libera a geração e a visualização dos PDFs.

Entidade `ContractHistory`: eventos da jornada (envio de documentos, atualização cadastral, aprovação e rejeição) no mesmo Histórico do contrato.
