# Klyon Hub Platform

Sistema SaaS + Marketplace para comercialização automatizada de endereço fiscal, conectando clientes finais, coworkings próprios e parceiros. Backend em .NET 8 e frontend em Razor (CSHTML) com Bootstrap.

## Contexto do Projeto

A solução foi estruturada para atender fluxos de cadastro de clientes e empresas, contratação de endereço fiscal, gestão de contratos, pagamentos, comissões e repasses. Inclui integrações com gateways de pagamento (Asaas, PagBank), emissão fiscal (Focus NFe, SEFAZ), assinatura eletrônica (Clicksign) e armazenamento de documentos.

A arquitetura segue separação em camadas com Domain, Application e Infra, mantendo responsabilidades bem definidas entre projetos.

## Arquitetura

- Backend: .NET 8 com arquitetura em camadas
- Frontend: ASP.NET Core MVC com Views Razor (CSHTML), Bootstrap 5 e JavaScript
- API: padrão RESTful + controllers MVC
- Banco de dados: MariaDB com Entity Framework Core
- Solução: `klyon-hub-platform.sln`

### Camadas do Backend

- `KlyonHubPlatform.Api`: entrypoint da API e da aplicação web (Controllers, Views, wwwroot)
- `KlyonHubPlatform.Application`: casos de uso, serviços de aplicação e orquestração
- `KlyonHubPlatform.Domain`: entidades e interfaces de repositórios
- `KlyonHubPlatform.Domain.Core`: enums, constantes e compartilhados de domínio
- `KlyonHubPlatform.Infra.Data`: persistência (DbContext, repositórios, migrations)
- `KlyonHubPlatform.Infra.CrossCutting`: middlewares, helpers e utilitários transversais

### Frontend

- `KlyonHubPlatform.Api/Views`: Views Razor organizadas por área (Admin, Auth, Client, Coworking, Shared)
- `KlyonHubPlatform.Api/wwwroot`: CSS, JS e arquivos estáticos

## Como Executar

### Backend (e aplicação web)

```bash
dotnet restore klyon-hub-platform.sln
dotnet build klyon-hub-platform.sln
dotnet run --project KlyonHubPlatform.Api
```

Acesse: `https://localhost:5001`

### Usando Docker (recomendado para desenvolvimento)

1. Subir apenas o MariaDB:

```bash
docker-compose -f docker-compose.dev.yml up -d
```

2. Executar a aplicação:

```bash
dotnet run --project KlyonHubPlatform.Api
```

### Credenciais padrão

- **Email**: admin@klyon.com  
- **Senha**: Admin@123

## Banco de Dados (EF Core)

Na raiz do repositório:

```bash
dotnet ef migrations add <NomeDaMigration> -s KlyonHubPlatform.Api -p KlyonHubPlatform.Infra.Data
dotnet ef database update -s KlyonHubPlatform.Api -p KlyonHubPlatform.Infra.Data
```

## Documentação

- Configuração de email: [docs/README_EMAIL.md](docs/README_EMAIL.md)
- Configuração de pagamento: [docs/CONFIGURAR_PAGAMENTO.md](docs/CONFIGURAR_PAGAMENTO.md)
- Docker: [docs/DOCKER.md](docs/DOCKER.md)
- Deploy: [docs/DEPLOY.md](docs/DEPLOY.md)
- Oracle Object Storage: [docs/ORACLE_OBJECT_STORAGE.md](docs/ORACLE_OBJECT_STORAGE.md)
- Cache: [docs/CACHE.md](docs/CACHE.md)
- Validação em controllers: [docs/VALIDACAO-CONTROLLERS.md](docs/VALIDACAO-CONTROLLERS.md)

## Funcionalidades principais

### Clientes

- Cadastro de empresa/profissional
- Escolha de localidade
- Contratação de endereço fiscal
- Gestão de contratos e documentos
- Histórico de pagamentos

### Coworkings

- Cadastro do coworking e endereços fiscais
- Definição de preços
- Contratos ativos, comissões e solicitação de repasses

### Administração

- Gestão de usuários, coworkings e localidades
- Relatórios financeiros e logs de auditoria

## Estrutura de Pastas (resumo)

```text
.
├── docs/
│   ├── CACHE.md
│   ├── CONFIGURAR_PAGAMENTO.md
│   ├── DEPLOY.md
│   ├── DOCKER.md
│   ├── ORACLE_OBJECT_STORAGE.md
│   ├── README_EMAIL.md
│   └── VALIDACAO-CONTROLLERS.md
├── KlyonHubPlatform.Api/
│   ├── Controllers/
│   ├── Views/
│   ├── wwwroot/
│   └── ...
├── KlyonHubPlatform.Application/
├── KlyonHubPlatform.Domain/
├── KlyonHubPlatform.Domain.Core/
├── KlyonHubPlatform.Infra.CrossCutting/
├── KlyonHubPlatform.Infra.Data/
├── docker-compose.dev.yml
├── docker-compose.yml
├── Dockerfile
├── init.sql
└── klyon-hub-platform.sln
```
