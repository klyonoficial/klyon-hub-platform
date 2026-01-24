# 🔧 KlyonBoot - Troubleshooting

## ❌ Erro 500 ao Iniciar Conversa

### **Sintomas:**
- Console mostra: `Failed to load resource: the server responded with a status of 500`
- Erro: `ERR_INCOMPLETE_CHUNKED_ENCODING`
- JavaScript: `Erro ao iniciar conversa: Error: Erro desconhecido`

### **Causas Comuns:**

#### 1. **Tabelas do Banco de Dados Não Existem** ⚠️ MAIS COMUM

**Solução:**
```bash
# Opção 1: Via terminal
dotnet ef database update

# Opção 2: Via painel admin (se já tiver migrations aplicadas)
# Acesse: /admin/chatbot/migrations
# Clique em "Executar Migrations Pendentes"
```

**Verificar se as tabelas existem:**
```sql
SHOW TABLES LIKE 'Chat%';
```

Deve retornar:
- `ChatFaqItems`
- `ChatConversations`
- `ChatMessages`
- `ChatIntentions`
- `ChatUnansweredQuestions`

#### 2. **Migrations Não Foram Criadas**

**Solução:**
```bash
# Criar migration
dotnet ef migrations add AddKlyonBootChatbotTables --project KlyonHubPlatform.Infra.Data --startup-project KlyonHubPlatform.Api

# Aplicar migration
dotnet ef database update --project KlyonHubPlatform.Infra.Data --startup-project KlyonHubPlatform.Api
```

#### 3. **FAQ Vazio (Não é crítico, mas pode causar problemas)**

**Solução:**
```bash
# Executar seed do FAQ
# Ou adicionar manualmente via SQL ou painel admin
```

#### 4. **Problema de Conexão com Banco**

**Verificar:**
- Connection string no `appsettings.json`
- Banco de dados está rodando
- Credenciais corretas

---

## 🔍 Como Diagnosticar

### **1. Verificar Logs do Servidor**

No console do servidor (onde você rodou `dotnet run`), procure por:
```
[Error] Erro ao iniciar conversa: ...
```

### **2. Verificar no Console do Navegador**

Abra DevTools (F12) → Console:
- Veja a mensagem de erro completa
- Clique no erro para ver o stack trace

### **3. Testar Endpoint Diretamente**

```bash
# Via curl ou Postman
curl -X POST https://localhost:5001/api/chatbot/start \
  -H "Content-Type: application/json" \
  -d '{"sessionId":"test123","userId":null}'
```

### **4. Verificar se Serviços Estão Registrados**

No `Program.cs`, deve ter:
```csharp
builder.Services.AddScoped<IChatbotRepository, ChatbotRepository>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddScoped<IAIIntentionService, AIIntentionService>();
```

---

## ✅ Checklist de Verificação

- [ ] Migrations foram criadas
- [ ] Migrations foram aplicadas ao banco
- [ ] Tabelas existem no banco de dados
- [ ] Serviços estão registrados no `Program.cs`
- [ ] Connection string está correta
- [ ] Banco de dados está acessível
- [ ] FAQ foi populado (opcional, mas recomendado)

---

## 🚀 Solução Rápida

Se você acabou de implementar o KlyonBoot:

1. **Criar e aplicar migrations:**
   ```bash
   dotnet ef migrations add AddKlyonBootChatbotTables --project KlyonHubPlatform.Infra.Data --startup-project KlyonHubPlatform.Api
   dotnet ef database update --project KlyonHubPlatform.Infra.Data --startup-project KlyonHubPlatform.Api
   ```

2. **Ou usar o painel admin:**
   - Acesse: `/admin/chatbot/migrations`
   - Clique em "Executar Migrations Pendentes"

3. **Popular FAQ (opcional):**
   - Execute o `ChatbotSeedService` ou
   - Adicione manualmente via SQL

4. **Reiniciar aplicação:**
   ```bash
   dotnet run
   ```

5. **Testar:**
   - Limpe cache do navegador (Ctrl+F5)
   - Abra o chat
   - Deve funcionar! ✅

---

## 📞 Se Ainda Não Funcionar

1. **Verifique os logs do servidor** - procure por exceções
2. **Verifique o console do navegador** - veja erros JavaScript
3. **Teste o endpoint diretamente** - use Postman/curl
4. **Verifique se o banco está acessível** - teste conexão

---

**Desenvolvido com ❤️ pela equipe Klyon**
