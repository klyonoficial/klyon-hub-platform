# 🗄️ Executar Migrations via Web (Painel Admin)

## ✅ **IMPLEMENTADO!**

Agora você pode executar migrations diretamente do painel administrativo! 🎉

---

## 🚀 Como Usar

### 1. **Registrar o Serviço (Program.cs)**

Adicione no `Program.cs`:

```csharp
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.Services;

// Registrar serviço de migrations
builder.Services.AddScoped<IDatabaseMigrationService, DatabaseMigrationService>();

// Também adicionar ILogger se não estiver registrado
builder.Services.AddLogging();
```

### 2. **Acessar o Painel**

1. Fazer login como Admin
2. Acessar: `http://localhost:5000/admin/chatbot/dashboard`
3. Clicar em **"🗄️ Gerenciar Migrations"**
4. Ou acessar diretamente: `http://localhost:5000/admin/chatbot/migrations`

---

## 📊 Funcionalidades Disponíveis

### ✅ **Status do Banco de Dados**
- Verifica conexão com o banco
- Mostra total de migrations
- Exibe migrations aplicadas
- Lista migrations pendentes
- Mostra última migration executada

### ✅ **Visualizar Migrations**
- **Pendentes:** Migrations que ainda não foram aplicadas (amarelo)
- **Aplicadas:** Migrations já executadas (verde)

### ✅ **Executar Migrations**
1. Clique em **"🚀 Executar Migrations Pendentes"**
2. Confirme a operação (modal de confirmação)
3. Aguarde a execução
4. Veja o resultado (sucesso ou erro)

### ✅ **Atualizar Status**
- Botão **"🔄 Atualizar Status"** para recarregar informações

---

## ⚠️ **IMPORTANTE: Segurança**

### Proteções Implementadas

1. ✅ **Apenas Admin:** Rota protegida com `[Authorize(Roles = "Admin")]`
2. ✅ **Confirmação Obrigatória:** Modal de confirmação antes de executar
3. ✅ **Logs Completos:** Todas as ações são logadas
4. ✅ **Feedback Visual:** Mostra progresso e resultados
5. ✅ **Validação:** Verifica conexão antes de executar

### ⚠️ Recomendações

**✅ USAR EM:**
- Ambiente de Desenvolvimento
- Ambiente de Staging/Homologação
- Testes locais

**❌ CUIDADO EM:**
- Produção (sempre fazer backup antes!)
- Bancos compartilhados
- Sem confirmação da equipe

---

## 🔧 Endpoints da API

### 1. **GET /api/admin/database/status**
Retorna status completo do banco

**Response:**
```json
{
  "canConnect": true,
  "totalMigrations": 15,
  "appliedMigrations": 12,
  "pendingMigrations": 3,
  "pendingMigrationsList": [
    "20260124000000_AddKlyonBootChatbotTables",
    "20260125000000_AnotherMigration"
  ],
  "appliedMigrationsList": [...],
  "lastMigration": "20260123000000_LastMigration",
  "checkedAt": "2026-01-24T10:00:00"
}
```

### 2. **GET /api/admin/database/migrations/pending**
Lista apenas migrations pendentes

### 3. **GET /api/admin/database/migrations/applied**
Lista apenas migrations aplicadas

### 4. **POST /api/admin/database/migrations/execute**
Executa todas as migrations pendentes

**Request:**
```json
{
  "confirmed": true
}
```

**Response:**
```json
{
  "success": true,
  "message": "✅ 3 migration(s) executada(s) com sucesso!",
  "appliedMigrations": [
    "20260124000000_AddKlyonBootChatbotTables"
  ],
  "errors": [],
  "executedAt": "2026-01-24T10:05:00",
  "duration": {
    "totalSeconds": 3.5
  }
}
```

### 5. **GET /api/admin/database/migrations/has-pending**
Verifica se há migrations pendentes

**Response:**
```json
{
  "hasPending": true
}
```

---

## 🎨 Interface Visual

### Tela Principal

```
┌────────────────────────────────────────────────┐
│  🗄️ Gerenciamento de Migrations               │
├────────────────────────────────────────────────┤
│  ⚠️ ATENÇÃO: Operação Crítica                 │
│  • Executar migrations modifica o banco        │
│  • Recomendado apenas em dev/staging           │
│  • Sempre faça backup antes                    │
├────────────────────────────────────────────────┤
│  📊 Status do Banco de Dados                   │
│  ┌──────────┬──────────┬──────────┬──────────┐│
│  │ Conexão  │ Total    │ Aplicadas│ Pendentes││
│  │ ✅       │    15    │    12    │     3    ││
│  └──────────┴──────────┴──────────┴──────────┘│
├────────────────────────────────────────────────┤
│  ⏳ Migrations Pendentes                       │
│  • 20260124000000_AddKlyonBootChatbotTables   │
│  • 20260125000000_AnotherMigration            │
├────────────────────────────────────────────────┤
│  ✅ Migrations Aplicadas                       │
│  • 20260123000000_PreviousMigration           │
│  • ...                                         │
├────────────────────────────────────────────────┤
│  [🚀 Executar Migrations]  [🔄 Atualizar]     │
└────────────────────────────────────────────────┘
```

---

## 📝 Exemplo de Uso

### Cenário: Aplicar Migrations do KlyonBoot

```
1. Acessar /admin/chatbot/migrations

2. Ver status:
   ✅ Conexão OK
   ⏳ 3 migrations pendentes
   
3. Clicar em "🚀 Executar Migrations Pendentes"

4. Confirmar no modal:
   "⚠️ Você está prestes a executar 3 migration(s)"
   [Cancelar] [Sim, Executar!]

5. Aguardar execução (spinner)

6. Ver resultado:
   ✅ 3 migration(s) executadas com sucesso!
   
   Migrations aplicadas:
   • 20260124000000_AddKlyonBootChatbotTables
   • 20260125000000_AnotherMigration
   • 20260126000000_YetAnotherMigration
   
   Duração: 3.5s

7. Status atualizado automaticamente:
   ✅ 0 migrations pendentes
```

---

## 🐛 Troubleshooting

### Erro: "Não foi possível conectar ao banco"

**Solução:**
1. Verificar connection string no `appsettings.json`
2. Verificar se o banco está rodando
3. Verificar credenciais

### Erro: "Endpoint não encontrado"

**Solução:**
1. Verificar se `app.MapControllers()` está no Program.cs
2. Verificar se o serviço foi registrado no DI
3. Verificar se está autenticado como Admin

### Migrations não aparecem

**Solução:**
1. Verificar se as migrations existem na pasta `Migrations/`
2. Executar `dotnet ef migrations list` para ver migrations
3. Recompilar o projeto

---

## 🔒 Segurança em Produção

### ⚠️ **ANTES de usar em PRODUÇÃO:**

1. ✅ **Fazer backup completo do banco**
   ```bash
   # MySQL
   mysqldump -u user -p database > backup.sql
   
   # SQL Server
   BACKUP DATABASE [YourDB] TO DISK = 'C:\Backup\db.bak'
   ```

2. ✅ **Testar em ambiente de staging primeiro**

3. ✅ **Agendar manutenção** (informar usuários)

4. ✅ **Ter plano de rollback**

5. ✅ **Monitorar logs** durante e após execução

6. ✅ **Considerar desabilitar em produção:**
   ```csharp
   // No Program.cs
   if (!app.Environment.IsProduction())
   {
       builder.Services.AddScoped<IDatabaseMigrationService, DatabaseMigrationService>();
   }
   ```

---

## 📊 Logs

Todas as operações são logadas:

```csharp
// Exemplo de logs gerados:
[2026-01-24 10:05:00] INFO: Iniciando execução de migrations...
[2026-01-24 10:05:00] INFO: Encontradas 3 migrations pendentes
[2026-01-24 10:05:03] INFO: Migrations executadas com sucesso: 20260124000000_AddKlyonBootChatbotTables, ...
[2026-01-24 10:05:03] WARN: Usuário admin@klyon.com iniciou execução de migrations
```

---

## ✅ Checklist de Implementação

- [x] Criar interface `IDatabaseMigrationService`
- [x] Implementar `DatabaseMigrationService`
- [x] Criar controller `DatabaseAdminController`
- [x] Criar view `DatabaseMigrations.cshtml`
- [x] Adicionar link no dashboard
- [x] Implementar confirmação de segurança
- [x] Adicionar logs
- [x] Criar documentação

---

## 🎉 Pronto!

Agora você pode executar migrations via web de forma segura e profissional!

**Lembre-se:** Com grandes poderes vêm grandes responsabilidades. Use com cuidado! 🦸‍♂️

---

**Desenvolvido com ❤️ pela equipe Klyon**
