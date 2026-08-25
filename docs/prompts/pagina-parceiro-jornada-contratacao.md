# Prompt Cursor — Página principal do parceiro e jornada de contratação

> **Como usar:** copie o conteúdo a partir da seção [Prompt para executar no Cursor](#prompt-para-executar-no-cursor) e cole em um chat **Agent** novo.

---

## Prompt para executar no Cursor

```
# Tarefa: Página principal do parceiro (estilo listing) + jornada de contratação com aprovação de documentos

Implemente no repositório **Klyon Hub Platform** (`klyon-hub-platform.sln`). Responda em português. Siga `AGENTS.md`: DDD/Clean Architecture, controllers sem regra de negócio e sem `ApplicationDbContext`, lógica na Application, Views só de apresentação. Documente US novas/alteradas em `docs/us/Client/` e atualize `docs/us/index.md`.

Não faça commit. Verifique UI no browser (desktop + mobile) antes de encerrar.
```

---

## Contexto do produto

Klyon vende **endereço fiscal** (e comercial como alternativa) em coworkings parceiros. Hoje existe **um único parceiro** com endereço em `/Client/AddressDetail?coworkingAddressId=2`.

A home atual (`https://localhost:5001/` → `Home/Index`) é uma LP genérica (“Ver endereços disponíveis”). Com um único parceiro, a home deve ser **a página do parceiro**.

### Referência de UX

Adaptar para endereço fiscal — **não copiar** Airbnb (marca, textos, fotos nem layout pixel a pixel):

https://www.airbnb.com.br/rooms/1735832304751741137

Inspirar-se em:

- Galeria grande no topo, foto principal + grid de secundárias no desktop
- **Swipe horizontal** (arrastar esquerda/direita) troca a foto no mobile, como no listing
- Setas, dots/contador, lightbox ao tocar na foto
- Barra/card sticky de preço + CTA **Contratar**
- Hierarquia: título do espaço, cidade, preço, descrição, amenities, host/parceiro, documentos de confiança
- Layout 2 colunas no desktop (conteúdo | reserva/CTA); 1 coluna no mobile com CTA fixo no rodapé

Usar identidade visual Klyon (layout `_LandingLayout.cshtml`, paleta já usada em `AddressDetail.cshtml`: fundo `#051323`, accent `#c69844`).

---

## Estado atual (não quebrar o que já funciona)

| Peça | Onde |
|------|------|
| Página do parceiro (cliente) | `KlyonHubPlatform.Api/Views/Client/AddressDetail.cshtml` + `ClientController.AddressDetail` |
| Home | `HomeController.Index` → `Views/Home/Index.cshtml` |
| Fotos | `CoworkingPhotos`; carrossel atual só troca por botões/dots, **sem swipe** (`AddressDetail.cshtml`) |
| Contratar | CTA → `CreateContractAndGoToPayment` → onboarding (`FiscalOnboarding`) → `PaymentDetails` → `PaymentSuccess` |
| Pagamento confirmado | `PaymentService.ConfirmPaymentAsync` ativa contrato (`PendingPayment` → `Active`) e enfileira `ContractDocumentsEnsureService` (gera **contrato PDF + declaração**) |
| Docs do coworking | `CoworkingDocument` / `CoworkingDocumentType`: `AddressCessionContract`, `AddressProof`, `CNPJCard`, `AddressAuthorization`, `UtilityBill`. **Não existem** Alvará de funcionamento nem Alvará dos Bombeiros |
| Storage | Oracle Object Storage (`docs/ORACLE_OBJECT_STORAGE.md`); pastas `contracts` e `coworking-photos` |
| US atuais | `docs/us/Client/001`, `002`, `003` — jornada antiga (contrato/declaração no pagamento) |

---

## 1. Página principal do parceiro (UX listing)

Reprojetar `AddressDetail` (ou extrair partials reutilizáveis) como página principal do parceiro.

### Galeria

- **Desktop:** foto principal grande + thumbnails; clique abre lightbox
- **Mobile/tablet:** uma foto em tela cheia (ou quase); **arrastar para a esquerda/direita troca a foto** (touch swipe, pointer events). Também setas e indicador `1 / N`
- Gestos: swipe com threshold, não conflitar com scroll vertical da página; suporte a teclado (`←` `→`) no lightbox
- Usar fotos de `CoworkingPhotos` (`GetPhoto`). Sem foto: placeholder elegante, sem quebrar layout
- Preferir CSS + JS próprio (ou lib já no projeto). Se adicionar lib, justificar e manter bundle leve

### Conteúdo (endereço fiscal, não hospedagem)

- Título: nome fantasia do parceiro + “Endereço fiscal”
- Local: cidade/UF, bairro, endereço formatado
- Preço anual + equivalente mensal; vagas restantes
- O que está incluso (correspondência, uso cadastral Receita/Junta, etc. — reaproveitar textos atuais da página)
- Sobre o espaço / parceiro
- Mapa ou endereço completo se já existir
- FAQ existente pode permanecer, adaptado ao layout

### CTA

- Primário: **Contratar** (endereço fiscal)
- Comercial: secundário, não duplicar o CTA principal (manter regra da US 001)
- Mobile: barra sticky inferior com preço + Contratar (não tapar conteúdo; safe-area iOS)

---

## 2. Home = página do parceiro quando houver um único endereço publicado

Parametrizar:

- Contar endereços **publicados e disponíveis** (ex.: `CoworkingAddress.IsAvailable == true` e parceiro ativo)
- **Se count == 1:** `GET /` (`HomeController.Index`) redireciona (302) para a página desse endereço (`/Client/AddressDetail?coworkingAddressId={id}`). Hoje o id conhecido é `2`, mas **não hardcodar** — resolver pelo banco
- **Se count != 1:** manter a home atual (lista/LP / `ChooseLocation` conforme o fluxo existente)
- Implementar a regra na **Application** (serviço já usado pela home ou método novo em `IAppHomeService` / similar). Controller só orquestra
- Cache curto ou `ResponseCache(NoStore)` já existente: o redirect deve refletir mudança de quantidade de parceiros sem cache stale
- Ajustar links “Voltar ao início” / logo que hoje apontam para `/` para caírem na página certa (o redirect de `/` já resolve se for 302)

---

## 3. Documentos de confiança do parceiro (modal PDF)

Menu **evidente** na página do parceiro para o cliente ver:

1. Alvará de funcionamento
2. Alvará dos bombeiros
3. Cartão CNPJ

### Comportamento

- Clique abre **modal** com o PDF (embed/`iframe` ou viewer) + botão baixar + fechar (ESC, overlay, botão)
- Mobile: modal full-screen; PDF usável (scroll, pinch se o browser permitir)
- Só exibir o item se o documento existir e estiver aprovado/ativo. Se faltar, ocultar ou “documento em breve” — não quebrar a página
- `CNPJCard` já existe em `CoworkingDocumentType`. **Adicionar** tipos para alvará de funcionamento e alvará dos bombeiros (enum + migration + upload no fluxo do parceiro/admin se ainda não houver tela; no mínimo persistência + leitura pública na página do cliente)
- Endpoint autenticado ou público somente leitura dos PDFs **do coworking daquele endereço** (não vazar docs de outro parceiro). Servir via storage (Oracle), nunca path local hardcoded
- Controllers sem regra de negócio; validação de tipo/existência na Application

---

## 4. Responsividade 100% (obrigatório)

A página do parceiro **e toda a jornada nova** devem funcionar em:

- iPhone (Safari, notch/safe-area)
- Android (Chrome)
- Tablets retrato/paisagem
- Desktop ≥ 1200px

### Checklist

- Sem overflow-x; toques ≥ 44px; tipografia legível; imagens `max-width: 100%`
- Galeria swipe no touch; CTA sticky não cobre o último conteúdo
- Modal PDF utilizável no mobile
- Upload de documentos usável no mobile (câmera + galeria, `accept` correto, labels grandes)
- Testar viewports ~375, 390, 768, 1024, 1440
- Não quebrar `_LandingLayout` (header/menu hamburger)

---

## 5. Nova jornada do cliente (mudança de regra de negócio)

### Fluxo desejado

```
Página do parceiro (home quando 1 parceiro)
  → Contratar
  → Onboarding (se anônimo / dados incompletos) — manter FiscalOnboarding
  → Pagar (PIX-first, PaymentDetails)
  → NÃO gerar contrato PDF nem declaração no pagamento
  → Redirect para upload de documentos obrigatórios:
        • RG, CPF ou CNH (documento de identidade)
        • Comprovante de residência
     Enviar ao Object Storage (Oracle)
  → Aguardar aprovação do parceiro (coworking) ou admin
  → Se aprovado: aí sim gerar contrato PDF + declaração
     (reusar ContractDocumentsEnsureService)
```

### Regras

1. **Pagamento confirmado NÃO gera** contrato PDF nem declaração. Remover/condicionar o `EnqueueContractDocumentsAsync` / `contractIdsToEnsureDocuments` em `PaymentService.ConfirmPaymentAsync` para este fluxo. Pagamento continua confirmando o **pedido/pagamento**; o contrato de serviço pode existir internamente em status que **não libere documentos** até a aprovação (ex. novo status ou flag `DocumentsApproved` — escolher o modelo mais limpo no domínio, sem gambiarra).
2. Após pagar (ou `PaymentSuccess` quando `IsPaid`): redirecionar para tela nova de upload, não para “contrato ativo / ver declaração”.
3. Tela de upload (nova action/view em Client):
   - obrigatórios: identidade (RG **ou** CPF **ou** CNH — um documento que cubra identificação; se forem arquivos separados, RG+CPF **ou** CNH) e comprovante de residência
   - validar tipo MIME (PDF, JPG, PNG), tamanho máximo, e persistir no storage
   - reenvio se rejeitado, com motivo visível
   - estado: “Aguardando aprovação”
4. Aprovação: **parceiro (Coworking) ou Admin** pode aprovar/rejeitar. Dashboard do parceiro + tela admin. Auditoria (quem aprovou, quando, motivo de rejeição).
5. **Somente após aprovação** chamar geração de contrato + declaração (`IContractDocumentsEnsureService.EnsureAsync`), reutilizando `AddressDeclarationService` e `DocumentService`.
6. Cliente só vê contrato/declaração depois de aprovado. Ajustar `Declarations`, `ContractDetails`, `PaymentSuccess` e textos (“contrato ativo” hoje está errado para este fluxo).
7. Não gerar documentos de novo em webhooks de pagamento.
8. Endereço comercial: aplicar a mesma jornada se compartilhar o checkout; se o escopo ficar grande, fiscal primeiro e deixar comercial consistente (mesma gate de documentos).

### Modelo de dados (sugerido)

Criar entidade (ex. `ClientOnboardingDocument` ou estender documentos do cliente) com:

- `ClientId` / `ContractId`
- tipo
- storage key
- status (`Pending` / `Approved` / `Rejected`)
- `ReviewedByUserId`
- `ReviewedAt`
- `RejectionReason`

Migration EF no padrão do repo:

```bash
dotnet ef migrations add <Nome> -s KlyonHubPlatform.Api -p KlyonHubPlatform.Infra.Data
```

Não usar `ApplicationDbContext` no controller.

---

## 6. Documentação

Atualizar:

| Arquivo | O que mudar |
|---------|-------------|
| `docs/us/Client/001-buscar-contratar-endereco-fiscal.md` | Entrada pela página do parceiro; home com 1 parceiro |
| `docs/us/Client/002-checkout-onboarding-pagamento.md` | Pós-pagamento = upload, não docs gerados |
| `docs/us/Client/003-declaracao-endereco-fiscal-comercial.md` | Geração **após aprovação**, não no `ConfirmPaymentAsync` |
| `docs/us/Client/004-upload-documentos-aprovacao.md` | **Nova US:** upload, storage, aprovação parceiro/admin, então contrato + declaração |
| `docs/us/index.md` | Incluir a US 004 |

---

## 7. Fora de escopo

- Marketplace de salas, candidatura de parceiro, chatbot, FocusNFe/SEFAZ
- Copiar conteúdo/fotos do Airbnb
- Hardcode `coworkingAddressId=2`

---

## 8. Critérios de aceite

- [ ] Com 1 endereço publicado, `https://localhost:5001/` abre a página do parceiro (redirect para AddressDetail correto)
- [ ] Com 0 ou 2+ endereços, a home atual permanece
- [ ] Galeria: swipe esquerda/direita troca foto no mobile; setas/dots/lightbox no desktop
- [ ] Menu visível: Alvará de funcionamento, Alvará dos Bombeiros, Cartão CNPJ → modal com PDF
- [ ] Layout ok em iOS, Android, tablet e desktop (sem corte, swipe e CTA sticky ok)
- [ ] Contratar → pagar **não** gera PDF de contrato nem declaração
- [ ] Pós-pagamento: upload RG/CPF/CNH + comprovante de residência no Object Storage
- [ ] Parceiro ou admin aprova/rejeita; rejeição permite reenvio
- [ ] Aprovado → gera contrato + declaração (mesmo renderer atual)
- [ ] Controllers magros; regras na Application; US atualizadas
- [ ] `dotnet build klyon-hub-platform.sln` ok

---

## 9. Verificação

1. Build da solution.
2. Browser: home com 1 parceiro → galeria swipe → docs em modal → Contratar → pagar (sandbox) → upload → estado “aguardando” → aprovar como admin/parceiro → contrato e declaração existem.
3. Confirmar que webhook/pagamento pago **não** chama mais `EnsureAsync` antes da aprovação.
4. Viewports 375 / 768 / 1440 na página do parceiro, modal PDF e upload.
