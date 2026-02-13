# Validação: Controllers sem regras de negócio e sem acesso direto ao banco

Regra: **a camada de controller não pode ter regras de negócio nem acessar o banco de dados**. O controller deve ser simples e apenas orquestrar requisição/resposta e chamar serviços.

## Resumo da validação

| Controller | Acesso a DB (_context) | Regras de negócio no controller | Status |
|------------|------------------------|----------------------------------|--------|
| **ClientController** | Sim (muitos usos) | Sim (validações, filtros, deduplicação) | **Parcial** – método `Services()` refatorado; demais ações ainda usam _context |
| **AdminController** | Sim (~270 usos) | Provável | **Violação** – deve usar serviços/repositórios |
| **CoworkingController** | Sim (~236 usos) | Provável | **Violação** – deve usar serviços/repositórios |
| **AuthController** | Sim (Users, SaveChanges) | Pouco | **Violação** – mover para IAuthService |
| **FocusNFeWebhookController** | Sim | Sim | **Violação** – extrair para serviço de webhook |
| **QuickClientApiController** | Sim | Sim | **Violação** – extrair para serviço de registro rápido |
| **ChatbotController** | Sim (GetRequiredService&lt;DbContext&gt;) | Possível | **Violação** – evitar service locator e acesso a DB no controller |
| **DatabaseAdminController** | A verificar | A verificar | A validar |
| **HomeController** | Não (usa _systemConfig) | Não | **OK** |
| **PaymentWebhookController** | A verificar | A verificar | A validar |
| **PagBankWebhookController** | A verificar | A verificar | A validar |
| **AsaasWebhookController** | A verificar | A verificar | A validar |
| **ChatbotAdminController** | A verificar | A verificar | A validar |
| **LocalizationController** | A verificar | A verificar | A validar |

## O que foi feito (exemplo no ClientController)

1. **Validação de cadastro do cliente**  
   Antes: controller lia `_context.Clients`, checava CPF/CNPJ, telefone, nome, estado e redirecionava.  
   Depois: `IClientMenuService.GetRegistrationStatusAsync(userId)` retorna `ClientRegistrationStatus`; o controller só aplica TempData e redireciona.

2. **Listagem e regras de serviços**  
   Antes: controller acessava `_context.Services`, deduplicava, corrigia descrições/rotas, gravava com `SaveChangesAsync`, criava serviços padrão e filtrava por feature.  
   Depois: `IServiceCatalogService.GetActiveServicesForClientAsync(userId, marketplaceRoute, chooseLocationRoute)` concentra toda essa lógica; o controller só chama o serviço e devolve a view.

3. **Controller permanece simples**  
   - Obtém `userId` e rotas (URL).  
   - Chama serviços.  
   - Define ViewBag/TempData e retorna View/Redirect.

## Próximos passos recomendados

- **ClientController**: remover `ApplicationDbContext` do controller e, ação a ação, passar a usar apenas serviços (ex.: `IClientService` para cliente por userId/id, `IContractService` para contratos, `IPaymentService` para pagamentos, etc.). Criar interfaces na Application quando não existirem.
- **AdminController** e **CoworkingController**: mesmo padrão – identificar blocos que usam `_context` e regras de negócio e extrair para serviços/repositórios na camada Application/Infra.
- **AuthController**: mover leitura/atualização de `Users` e `SaveChangesAsync` para `IAuthService` (ou serviço específico de usuário) e manter o controller só com orquestração e respostas HTTP.
- **Webhooks (FocusNFe, QuickClient, PagBank, Payment, Asaas)**: cada um em um serviço dedicado (ex.: `IFocusNFeWebhookService`), com o controller apenas recebendo a requisição, chamando o serviço e retornando o status HTTP.
- **ChatbotController**: deixar de resolver `ApplicationDbContext` por service locator; injetar um serviço que encapsule o que hoje é feito com o context.

## Referência de padrão (controller simples)

```csharp
[HttpGet]
public async Task<IActionResult> Services(CancellationToken cancellationToken = default)
{
    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    if (User.IsInRole(RoleNames.Client) && !User.IsInRole(Administrator))
    {
        var status = await _clientMenuService.GetRegistrationStatusAsync(userId);
        if (!status.IsComplete)
        {
            TempData[TempDataKeys.Info] = status.TempDataMessageKey == "CompleteRegistrationWithFields"
                ? InfoMessages.CompleteRegistrationWithFields
                : InfoMessages.CompleteRegistration;
            return RedirectToAction(status.RedirectActionName ?? ActionNames.RegisterClient);
        }
    }

    var hasActiveContract = await _clientMenuService.ClientHasActiveContractAsync(userId);
    var marketplaceRoute = Url.Action(ActionNames.Marketplace, ControllerNames.Home) ?? "/Home/Marketplace";
    var chooseLocationRoute = Url.Action(ActionNames.ChooseLocation, ControllerNames.Client) ?? "/Client/ChooseLocation";

    var services = await _serviceCatalogService.GetActiveServicesForClientAsync(userId, marketplaceRoute, chooseLocationRoute);

    ViewBag.HasActiveContract = hasActiveContract;
    return View(services);
}
```

Nenhuma regra de negócio e nenhum acesso a banco no controller; apenas chamadas a serviços e decisões de apresentação (TempData, Redirect, View).
