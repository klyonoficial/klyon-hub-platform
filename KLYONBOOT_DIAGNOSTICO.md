# 🔍 KlyonBoot - Diagnóstico de Erros

## ❌ Erro 500 Persistente

Se você está vendo erro 500 mesmo após executar migrations, siga este guia de diagnóstico:

---

## 🔧 **Passo 1: Verificar se a Migration Foi Aplicada**

### Via SQL:
```sql
-- Verificar se a migration foi aplicada
SELECT * FROM `__EFMigrationsHistory` 
WHERE MigrationId LIKE '%KlyonBoot%' OR MigrationId LIKE '%Chatbot%' OR MigrationId LIKE '%Chat%';
```

**Resultado esperado:**
- Deve retornar: `20260124000000_AddKlyonBootChatbotTables`

### Via Código:
Acesse: `https://localhost:5001/api/chatbot/diagnostics/tables`

**Resultado esperado:**
```json
{
  "allTablesExist": true,
  "tables": {
    "ChatFaqItems": true,
    "ChatConversations": true,
    "ChatMessages": true,
    "ChatIntentions": true,
    "ChatUnansweredQuestions": true
  },
  "message": "Todas as tabelas do KlyonBoot existem"
}
```

---

## 🔧 **Passo 2: Verificar se as Tabelas Existem**

### Via SQL:
```sql
-- Verificar se as tabelas existem
SHOW TABLES LIKE 'Chat%';
```

**Resultado esperado:**
```
ChatConversations
ChatFaqItems
ChatIntentions
ChatMessages
ChatUnansweredQuestions
```

### Se as tabelas NÃO existem:

**Solução:**
1. Acesse: `https://localhost:5001/Admin/DatabaseManager`
2. Verifique se a migration `20260124000000_AddKlyonBootChatbotTables` aparece na lista
3. Se aparecer, clique em **"Executar Migrations Pendentes"**
4. Se NÃO aparecer, a migration pode já ter sido marcada como aplicada mas as tabelas não foram criadas

---

## 🔧 **Passo 3: Forçar Criação das Tabelas (Se Migration Já Foi Aplicada)**

Se a migration já está marcada como aplicada mas as tabelas não existem:

### Opção 1: Remover Migration do Histórico e Reaplicar

```sql
-- Remover do histórico
DELETE FROM `__EFMigrationsHistory` 
WHERE MigrationId = '20260124000000_AddKlyonBootChatbotTables';

-- Depois, acesse /Admin/DatabaseManager e execute novamente
```

### Opção 2: Criar Tabelas Manualmente

Execute o SQL da migration diretamente. O arquivo está em:
`KlyonHubPlatform.Infra.Data/Migrations/20260124000000_AddKlyonBootChatbotTables.cs`

---

## 🔧 **Passo 4: Verificar Logs do Servidor**

No console onde você rodou `dotnet run`, procure por:

```
[Error] Erro ao iniciar conversa: ...
```

A mensagem de erro completa vai indicar o problema exato.

---

## 🔧 **Passo 5: Testar Endpoint de Diagnóstico**

Acesse no navegador ou Postman:
```
GET https://localhost:5001/api/chatbot/diagnostics/tables
```

Isso vai mostrar exatamente quais tabelas existem e quais não existem.

---

## ✅ **Checklist Completo**

- [ ] Migration `20260124000000_AddKlyonBootChatbotTables` existe no projeto
- [ ] Migration aparece em `/Admin/DatabaseManager` como pendente OU já aplicada
- [ ] Tabelas existem no banco (verificar via SQL ou endpoint de diagnóstico)
- [ ] Serviços estão registrados no `Program.cs`
- [ ] Connection string está correta
- [ ] Banco de dados está acessível

---

## 🚨 **Se Nada Funcionar**

1. **Verifique os logs do servidor** - procure por exceções completas
2. **Teste o endpoint de diagnóstico**: `/api/chatbot/diagnostics/tables`
3. **Verifique se há outras migrations pendentes** que podem estar bloqueando
4. **Tente criar as tabelas manualmente** usando o SQL da migration

---

**Desenvolvido com ❤️ pela equipe Klyon**
