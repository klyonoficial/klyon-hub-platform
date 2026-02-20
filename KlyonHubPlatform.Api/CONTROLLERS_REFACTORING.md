# Validação e refatoração de controllers

Este documento descreve o que já foi feito (Admin) e recomendações para **CoworkingController**, **ClientController** e **AuthController** sem impactar o frontend.

---

## Já implementado (AdminController)

- **Padrão Command/Result**: `AdminCommandResult`, `IAdminApprovalService`, `IAdminCoworkingService`.
- **Handlers na Application**:
  - Aprovar/Rejeitar capacidade fiscal, max contracts e preços no cartão → `AdminApprovalService`.
  - Atualizar Coworking → `AdminCoworkingService` (comando `UpdateCoworkingCommand`).
- **Controller**: apenas validação de entrada (ex.: motivo de reprovação, CNPJ), chamada ao serviço e aplicação do resultado (TempData + redirect/JSON).

---

## CoworkingController

- **Situação**: muitas ações (~40+), uso direto de `_context` em várias delas, métodos grandes (ex.: `UpdateProfile`, `AddAddress`, `EditAddress`, `RequestCapacityChange`).
- **Recomendações**:
  1. **Manter** uso de `ICoworkingService`, `IPaymentService`, etc. O controller já delega parte da lógica.
  2. **Extrair para Application** (novos serviços ou extensão dos existentes):
     - `BuildAdministrativeAlertsAsync` → serviço tipo `ICoworkingAlertsService.GetAlertsAsync(coworkingId)`.
     - Lógica de `UpdateProfile` (validações + atualização de entidade) → comando `UpdateCoworkingProfileCommand` e handler em `ICoworkingService` ou `ICoworkingProfileService`.
     - `AddAddress` / `EditAddress`: regras de negócio (ex.: máximo de endereços, conflito de capacidade) e persistência → `IAddressDeclarationService` ou novo `ICoworkingAddressService` com comandos.
     - `RequestCapacityChange`, `EditMaxContracts`, `CardPrices`: criar comandos e handlers na Application; o controller só valida entrada e chama o serviço.
  3. **Não alterar** rotas, nomes de actions ou formato de resposta (View/JSON) para não impactar o frontend.

---

## ClientController

- **Situação**: ~100 usos de `_context`; muita lógica de relatórios, créditos, pagamentos e reservas no controller.
- **Recomendações**:
  1. **Relatórios e listagens**: mover para serviços já existentes ou novos (ex.: `IClientDashboardService`, `IReportService`). O controller chama o serviço e monta View/JSON.
  2. **Comandos sugeridos** (quando fizer sentido):
     - Compra de créditos, uso de créditos em reserva, estorno → comandos com handlers na Application.
     - Upload de comprovante, validação de pagamento → comando + handler que usa `IPaymentService`/repositórios.
  3. **Validação no controller**: manter apenas validação de entrada (campos obrigatórios, IDs, permissões). Regras de negócio (ex.: saldo de créditos) ficam nos handlers.

---

## AuthController

- **Situação**: pouco uso de `_context` (4 ocorrências); fluxo concentrado em login, registro e troca de senha.
- **Recomendações**:
  1. **Manter** uso de `IAuthService` para autenticação, token e atualização de usuário. O controller já está enxuto em relação a lógica de negócio.
  2. **Opcional**: se ainda houver consultas diretas ao `_context` (ex.: buscar usuário por email para registro), mover para `IAuthService` ou `IUserService` (ex.: `GetUserByEmailAsync`), mantendo o controller só com validação e chamada ao serviço.
  3. **Frontend**: não alterar URLs, nomes de actions ou redirecionamentos para não quebrar login/registro.

---

## Regras gerais (sem impactar o frontend)

- **Rotas e nomes de actions**: não mudar.
- **TempData / ViewBag / ViewData**: manter as mesmas chaves e formatos de mensagem.
- **Respostas JSON**: manter o mesmo contrato (`success`, `message`, `data`, etc.).
- **Redirects**: manter as mesmas actions e parâmetros (ex.: `ViewCoworking`, `EditCoworking`).
- **Validação de entrada**: permanece no controller (model binding, `string.IsNullOrWhiteSpace`, etc.); regras de negócio e persistência vão para a Application.
