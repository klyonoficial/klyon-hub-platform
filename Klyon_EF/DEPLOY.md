# Guia de Deploy - Klyon Endereço Fiscal

## 📋 Pré-requisitos

- .NET 8.0 SDK instalado
- MariaDB 10.5+ instalado e rodando
- Servidor web (IIS, Nginx, Apache) ou hospedagem em nuvem

## 🔧 Configuração do Banco de Dados

1. Crie o banco de dados no MariaDB:
```sql
CREATE DATABASE KlyonEF CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

2. Configure a string de conexão no `appsettings.json` ou variáveis de ambiente:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=seu_servidor;Database=KlyonEF;User=seu_usuario;Password=sua_senha;Port=3306;"
  }
}
```

## 🔐 Configurações de Segurança

### JWT Settings
Altere a chave secreta em produção:
```json
{
  "JwtSettings": {
    "SecretKey": "SUA_CHAVE_SECRETA_SUPER_SEGURA_AQUI",
    "Issuer": "KlyonEF",
    "Audience": "KlyonEF_Users",
    "ExpirationMinutes": 1440
  }
}
```

### Payment Settings
Configure as credenciais do gateway de pagamento:
```json
{
  "PaymentSettings": {
    "Provider": "Stripe",
    "ApiKey": "sk_live_sua_chave_aqui",
    "WebhookSecret": "whsec_seu_webhook_secret"
  }
}
```

## 🚀 Deploy Local

1. Restaure as dependências:
```bash
dotnet restore
```

2. Execute as migrações (o sistema cria automaticamente na primeira execução):
```bash
dotnet ef database update
```

3. Execute o projeto:
```bash
dotnet run
```

## 🌐 Deploy em Produção

### Opção 1: Publicar e Executar

1. Publique o projeto:
```bash
dotnet publish -c Release -o ./publish
```

2. Execute o executável publicado:
```bash
cd publish
dotnet KlyonEF.dll
```

### Opção 2: Docker (Recomendado)

1. Crie um Dockerfile:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["KlyonEF.csproj", "./"]
RUN dotnet restore "KlyonEF.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "KlyonEF.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "KlyonEF.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "KlyonEF.dll"]
```

2. Construa e execute:
```bash
docker build -t klyon-ef .
docker run -d -p 80:80 -p 443:443 klyon-ef
```

### Opção 3: IIS (Windows)

1. Instale o ASP.NET Core Hosting Bundle
2. Publique o projeto:
```bash
dotnet publish -c Release
```

3. Configure o IIS para apontar para a pasta de publicação
4. Configure o Application Pool para usar "No Managed Code"

## 📁 Estrutura de Pastas Necessárias

Certifique-se de que as seguintes pastas existam e tenham permissões de escrita:
- `wwwroot/documents/` - Para armazenar documentos gerados

## 🔄 Tarefas Agendadas

Configure um job agendado (Cron, Task Scheduler) para executar:

1. **Processamento de Assinaturas Vencidas** (diariamente):
```bash
# Criar um endpoint ou console app que chama:
# ISubscriptionService.ProcessDueSubscriptionsAsync()
```

2. **Processamento de Repasses** (semanalmente):
```bash
# Processar transferências pendentes
```

## 📊 Monitoramento

- Configure logs em produção
- Monitore o banco de dados
- Configure alertas para erros críticos
- Monitore o uso de recursos

## 🔒 Checklist de Segurança

- [ ] Alterar chave JWT secreta
- [ ] Configurar HTTPS
- [ ] Configurar firewall
- [ ] Configurar backup automático do banco
- [ ] Configurar logs de auditoria
- [ ] Revisar permissões de arquivos
- [ ] Configurar rate limiting
- [ ] Validar inputs do usuário
- [ ] Configurar CORS adequadamente

## 📞 Suporte

Para problemas durante o deploy, consulte:
- Logs da aplicação
- Logs do banco de dados
- Logs do servidor web
