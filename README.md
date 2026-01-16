# Klyon Endereço Fiscal - Sistema SaaS + Marketplace

Sistema completo para comercialização automatizada de endereço fiscal, conectando clientes finais, coworkings próprios e parceiros.

## 🚀 Tecnologias

- **Backend**: ASP.NET Core 8.0 (C#)
- **Frontend**: Razor Pages (CSHTML) + Bootstrap 5 + JavaScript
- **Banco de Dados**: MariaDB
- **ORM**: Entity Framework Core
- **Autenticação**: JWT + Cookies
- **Geração de PDF**: iTextSharp

## 📋 Pré-requisitos

- .NET 8.0 SDK
- MariaDB 10.5+
- Visual Studio 2022 ou VS Code

## 🔧 Configuração

### Opção 1: Usando Docker (Recomendado)

1. **Clone o repositório**

2. **Inicie apenas o banco de dados MariaDB:**
```bash
docker-compose -f docker-compose.dev.yml up -d
```

3. **Execute o projeto:**
```bash
dotnet run
```

4. **Acesse:** `https://localhost:5001`

### Opção 2: Instalação Manual

1. **Clone o repositório**

2. **Instale o MariaDB** e configure a string de conexão no `appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=klyon_hub_platform;User=root;Password=sua_senha;Port=3306;"
}
```

3. **Execute o projeto:**
```bash
dotnet run
```

4. **Acesse:** `https://localhost:5001`

> 📖 **Nota:** O sistema cria o banco de dados automaticamente na primeira execução usando Entity Framework Core.

> 🐳 **Dica:** Para mais informações sobre Docker, consulte [DOCKER.md](DOCKER.md)

## 👤 Credenciais Padrão

- **Email**: admin@klyon.com
- **Senha**: Admin@123

## 📦 Estrutura do Projeto

```
klyon-hub-platform/
├── Controllers/          # Controllers MVC
├── Models/              # Modelos de dados
├── Services/            # Serviços de negócio
├── Repositories/        # Repositórios (acesso a dados)
├── Data/                # DbContext e configurações
├── Middleware/          # Middlewares customizados
├── Views/               # Views Razor (CSHTML)
└── wwwroot/             # Arquivos estáticos

```

## 🎯 Funcionalidades

### Para Clientes
- Cadastro de empresa/profissional
- Escolha de localidade
- Contratação de endereço fiscal
- Gestão de contratos
- Download de documentos
- Histórico de pagamentos

### Para Coworkings
- Cadastro do coworking
- Cadastro de endereços fiscais
- Definição de preços
- Visualização de contratos ativos
- Gestão de comissões
- Solicitação de repasses

### Para Administradores
- Gestão completa de usuários
- Gestão de coworkings
- Gestão de localidades e comissões
- Relatórios financeiros
- Logs de auditoria

## 🔐 Segurança

- Autenticação JWT + Cookies
- Autorização baseada em roles (RBAC)
- Criptografia de senhas (BCrypt)
- Logs de auditoria
- Validação de dados

## 📄 Documentos Gerados

- Contrato de endereço fiscal
- Recibos de pagamento
- Declaração para Receita Federal
- Termos de uso

## 💳 Pagamentos

O sistema está preparado para integração com gateways de pagamento (Stripe, PagSeguro, etc.). Atualmente, o processamento é simulado.

## 🔄 Processos Automatizados

- Criação de contratos
- Geração de assinaturas mensais
- Processamento de pagamentos recorrentes
- Cálculo e criação de comissões
- Repasses automáticos
- Geração de documentos

## 📝 Licença

Este projeto é proprietário.

## 👥 Suporte

Para suporte, entre em contato através do email: suporte@klyon.com
