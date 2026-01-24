# ⚙️ Configuração do KlyonBoot - Guia Rápido

## 📝 Passo a Passo de Instalação

### 1. Adicionar no Program.cs ou Startup.cs

```csharp
// No arquivo Program.cs (ou ConfigureServices no Startup.cs)

using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.Services;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Infra.Data.Repositories;

// ========================================
// REGISTRAR SERVIÇOS DO KLYONBOOT
// ========================================

// Repositórios
builder.Services.AddScoped<IChatbotRepository, ChatbotRepository>();

// Serviços
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddScoped<IAIIntentionService, AIIntentionService>();
builder.Services.AddScoped<ChatbotSeedService>();

// ========================================
// CORS (se necessário para API)
// ========================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowChatbot", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
```

### 2. Aplicar Migrations

```bash
# No diretório do projeto
cd c:\Source\klyon-hub-platform\klyon-hub-platform

# Aplicar migration
dotnet ef database update --project KlyonHubPlatform.Infra.Data --startup-project KlyonHubPlatform.Api
```

### 3. Popular FAQ (Primeira Execução)

**Opção A: Criar endpoint temporário no ChatbotController.cs**

```csharp
[HttpPost("admin/seed-faq")]
[Authorize(Roles = "Admin")] // Ou sem autorização para primeira execução
public async Task<IActionResult> SeedFaq([FromServices] ChatbotSeedService seedService)
{
    await seedService.SeedFaqDataAsync();
    return Ok(new { message = "FAQ populado com sucesso! Total: 16 itens" });
}
```

Depois, chamar via Postman/Browser:
```
POST http://localhost:5000/api/chatbot/admin/seed-faq
```

**Opção B: Executar no startup (apenas primeira vez)**

```csharp
// No Program.cs, após app.Build()
using (var scope = app.Services.CreateScope())
{
    var seedService = scope.ServiceProvider.GetRequiredService<ChatbotSeedService>();
    var chatbotRepo = scope.ServiceProvider.GetRequiredService<IChatbotRepository>();
    
    // Verificar se já existe FAQ
    var existingFaqs = await chatbotRepo.GetAllActiveFaqItemsAsync();
    if (!existingFaqs.Any())
    {
        await seedService.SeedFaqDataAsync();
        Console.WriteLine("✅ Base de conhecimento do KlyonBoot populada!");
    }
}
```

### 4. Adicionar Widget nas Views

**No arquivo `_Layout.cshtml` ou em páginas específicas:**

```html
<!DOCTYPE html>
<html>
<head>
    <!-- ... outros links ... -->
    
    <!-- KlyonBoot CSS -->
    <link rel="stylesheet" href="~/css/klyonboot.css" />
</head>
<body>
    <!-- Seu conteúdo aqui -->
    
    <!-- KlyonBoot JavaScript (antes de fechar </body>) -->
    <script src="~/js/klyonboot.js"></script>
    
    <!-- Opcional: Configuração personalizada -->
    <script>
        // O widget se inicializa automaticamente
        // Para customizar, descomentar e ajustar:
        
        // window.klyonboot = new KlyonBoot({
        //     apiBaseUrl: '/api/chatbot',
        //     userId: @(User.Identity?.IsAuthenticated == true ? User.FindFirst("UserId")?.Value : "null")
        // });
    </script>
</body>
</html>
```

### 5. Configurar Rotas do Painel Admin

**No Program.cs, garantir que as rotas MVC estejam configuradas:**

```csharp
// Antes de app.Run()
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "chatbot-admin",
    pattern: "admin/chatbot/{action=Dashboard}/{id?}",
    defaults: new { controller = "ChatbotAdmin" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers(); // Para API Controllers
```

---

## 🧪 Testar a Instalação

### 1. Verificar Banco de Dados

```sql
-- No MySQL/SQL Server, verificar se as tabelas foram criadas:
SHOW TABLES LIKE 'Chat%';

-- Deve retornar:
-- ChatFaqItems
-- ChatConversations
-- ChatMessages
-- ChatIntentions
-- ChatUnansweredQuestions
```

### 2. Testar API

**a) Iniciar conversa:**
```bash
curl -X POST http://localhost:5000/api/chatbot/start \
  -H "Content-Type: application/json" \
  -d '{"sessionId":"test_123","userId":null}'
```

**b) Enviar mensagem:**
```bash
curl -X POST http://localhost:5000/api/chatbot/message \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId":"test_123",
    "message":"Como me cadastrar como parceiro?"
  }'
```

**c) Ver estatísticas:**
```bash
curl http://localhost:5000/api/chatbot/admin/statistics
```

### 3. Testar Widget

1. Abrir qualquer página do site que tenha o widget
2. Clicar no botão flutuante roxo no canto inferior direito
3. Digitar: "Olá"
4. Digitar: "Como me cadastrar como parceiro?"
5. Verificar se a resposta está correta

### 4. Testar Dashboard Admin

1. Fazer login como Admin
2. Acessar: `http://localhost:5000/admin/chatbot/dashboard`
3. Verificar se as estatísticas carregam
4. Acessar: `http://localhost:5000/admin/chatbot/unanswered-questions`

---

## 🐛 Troubleshooting

### Erro: "Tabelas não encontradas"

**Solução:** Executar migrations:
```bash
dotnet ef database update --project KlyonHubPlatform.Infra.Data --startup-project KlyonHubPlatform.Api
```

### Erro: "IChatbotService não registrado"

**Solução:** Verificar se adicionou no Program.cs:
```csharp
builder.Services.AddScoped<IChatbotService, ChatbotService>();
```

### Widget não aparece

**Soluções:**
1. Verificar se o CSS foi incluído: `<link rel="stylesheet" href="~/css/klyonboot.css" />`
2. Verificar se o JS foi incluído: `<script src="~/js/klyonboot.js"></script>`
3. Ver console do navegador (F12) para erros JavaScript
4. Verificar se os arquivos estão em `wwwroot/css/` e `wwwroot/js/`

### API retorna 404

**Soluções:**
1. Verificar se `app.MapControllers()` está no Program.cs
2. Verificar se o Controller tem `[ApiController]` e `[Route("api/[controller]")]`
3. Testar rota: `http://localhost:5000/api/chatbot/start`

### Dashboard Admin retorna 403 (Forbidden)

**Soluções:**
1. Remover temporariamente `[Authorize(Roles = "Admin")]` para testar
2. Verificar se o usuário logado tem role "Admin"
3. Verificar autenticação JWT/Cookie

---

## 📊 Verificar se Está Funcionando

### Checklist Final

- [ ] Migrations aplicadas (tabelas criadas no banco)
- [ ] FAQ populado (16 itens iniciais)
- [ ] Serviços registrados no DI
- [ ] Widget aparece na página
- [ ] Possível enviar mensagens
- [ ] Bot responde corretamente
- [ ] Dashboard carrega estatísticas
- [ ] Perguntas não respondidas são registradas

---

## 🔧 Configurações Avançadas

### Alterar Confiança Mínima

```csharp
// Em ChatbotService.cs
private const decimal MINIMUM_CONFIDENCE = 0.6m; // 60% - Ajustar conforme necessário
```

### Adicionar Novas Palavras-Chave

```csharp
// Em AIIntentionService.cs, no dicionário _intentionKeywords:
{ "minha_nova_intencao", new List<string> { "palavra1", "palavra2", "palavra3" } }
```

### Customizar Cores do Widget

```css
/* Em klyonboot.css */
:root {
    --klyonboot-primary: #6C5CE7;      /* Roxo */
    --klyonboot-secondary: #A29BFE;    /* Roxo claro */
    --klyonboot-success: #00B894;      /* Verde */
    --klyonboot-danger: #FF7675;       /* Vermelho */
}
```

---

## 📞 Precisa de Ajuda?

Se encontrar problemas:

1. Verificar este guia novamente
2. Verificar o `KLYONBOOT_README.md` para documentação completa
3. Verificar logs da aplicação
4. Verificar console do navegador (F12)
5. Entrar em contato: dev@klyon.com.br

---

**✅ Setup Completo!** O KlyonBoot está pronto para uso. 🎉

---

**Última atualização:** 24/01/2026
