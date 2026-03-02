# Comandos úteis – Klyon Hub Platform

## Gerar JWT Key
```bash
node -p "crypto.randomBytes(256).toString('base64');"
```

## Migrations (Entity Framework)

### Terminal Comum (recomendado)
- **Criar Migration**
  ```bash
  dotnet ef migrations add <NomeDaMigration> -s KlyonHubPlatform.Api -p KlyonHubPlatform.Infra.Data
  ```
- **Remover Migration**
  ```bash
  dotnet ef migrations remove -s KlyonHubPlatform.Api -p KlyonHubPlatform.Infra.Data
  ```
- **Atualizar o Banco de Dados**
  ```bash
  dotnet ef database update -s KlyonHubPlatform.Api -p KlyonHubPlatform.Infra.Data
  ```
- **Listar Migrations**
  ```bash
  dotnet ef migrations list -s KlyonHubPlatform.Api -p KlyonHubPlatform.Infra.Data
  ```

### Terminal Gerenciador de Pacotes (Package Manager Console)
- **Listar Migrations**
  ```powershell
  Get-Migration -StartUp KlyonHubPlatform.Api -Project KlyonHubPlatform.Infra.Data
  ```
- **Criar Migration**
  ```powershell
  Add-Migration <NomeDaMigration> -StartUp KlyonHubPlatform.Api -Project KlyonHubPlatform.Infra.Data
  ```
- **Remover Migration**
  ```powershell
  Remove-Migration -StartUp KlyonHubPlatform.Api -Project KlyonHubPlatform.Infra.Data
  ```
- **Atualizar o Banco de Dados**
  ```powershell
  Update-Database -StartUp KlyonHubPlatform.Api -Project KlyonHubPlatform.Infra.Data
  ```

## Build e execução
- **Restaurar e compilar**
  ```bash
  dotnet restore klyon-hub-platform.sln
  dotnet build klyon-hub-platform.sln
  ```
- **Executar API localmente**
  ```bash
  dotnet run --project KlyonHubPlatform.Api
  ```
  Acesso padrão: `https://localhost:5001`

## Docker (desenvolvimento)
- **Subir banco de dados**
  ```bash
  docker-compose -f docker-compose.dev.yml up -d
  ```
