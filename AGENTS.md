# Diretrizes do Repositorio

## Estrutura do Projeto e Organizacao de Modulos
- Os projetos de backend seguem arquitetura em camadas: `KlyonHubPlatform.Api` (entrypoint), `KlyonHubPlatform.Application`, `KlyonHubPlatform.Domain`, `KlyonHubPlatform.Domain.Core`, `KlyonHubPlatform.Infra.Data` e `KlyonHubPlatform.Infra.CrossCutting`.
- O frontend e a API ficam em `KlyonHubPlatform.Api/`: Controllers, Views Razor (CSHTML), `wwwroot` (CSS, JS e arquivos estaticos). As Views estao organizadas por area: `Admin`, `Auth`, `ChatbotAdmin`, `Client`, `Coworking`, `Home` e `Shared`.
- Documentacao tecnica fica em `docs/` (email, pagamento, Docker, deploy, cache, validacao de controllers, etc.).
- Arquivo de solucao: `klyon-hub-platform.sln`.

## Comandos de Build, Teste e Desenvolvimento
- Restaurar/build do backend:
  ```bash
  dotnet restore klyon-hub-platform.sln
  dotnet build klyon-hub-platform.sln
  ```
- Executar API e aplicacao web localmente:
  ```bash
  dotnet run --project KlyonHubPlatform.Api
  ```
  Acesso padrao: `https://localhost:5001`
- Banco de dados com Docker (desenvolvimento):
  ```bash
  docker-compose -f docker-compose.dev.yml up -d
  ```
  Em seguida executar a aplicacao com `dotnet run --project KlyonHubPlatform.Api`.
- Migrations do Entity Framework (na raiz do repositorio):
  ```bash
  dotnet ef migrations add <NomeDaMigration> -s KlyonHubPlatform.Api -p KlyonHubPlatform.Infra.Data
  dotnet ef database update -s KlyonHubPlatform.Api -p KlyonHubPlatform.Infra.Data
  ```

## Estilo de Codigo e Convencoes de Nomenclatura
- Projetos C# usam nullable reference types e implicit usings (`<Nullable>enable</Nullable>`).
- Manter convencoes do C#: `PascalCase` para tipos/metodos e `camelCase` para variaveis locais/parametros.
- Views Razor e codigo em CSHTML: preferir nomes orientados a funcionalidade e manter consistencia com as areas (Admin, Auth, Client, Coworking). Partials e componentes em `Views/Shared` quando reutilizaveis.
- CSS e JS em `wwwroot`: organizar por feature ou modulo quando fizer sentido (ex.: `wwwroot/css/`, `wwwroot/js/`).

## Comportamento de Arquitetura Backend e Views
- Controllers nao devem conter regras de negocio nem acessar o banco diretamente (`ApplicationDbContext`). Devem orquestrar requisicao/resposta e delegar a servicos da camada Application. Ver `docs/VALIDACAO-CONTROLLERS.md` para o estado atual e padroes.
- Regras de negocio e validacoes: manter em `KlyonHubPlatform.Application` (servicos, DTOs, FluentValidation). Domain contem entidades e interfaces de repositorios; Infra.Data implementa persistencia e migrations.
- Views Razor: manter responsabilidades de apresentacao; evitar logica complexa nas views. Dados preparados pelos controllers ou por servicos chamados pelos controllers.
- Manter baixo acoplamento: Api depende de Application e Infra; Application depende de Domain; Infra.Data depende de Domain. Nao inverter dependencias.
- Reutilizar partials, layouts (`_Layout.cshtml`, `_LandingLayout.cshtml`, `_LayoutMinimal.cshtml`) e componentes compartilhados antes de duplicar codigo.
- Para formularios e validacao de entrada, usar FluentValidation na camada Application e manter controllers enxutos.

## Diretriz de Arquitetura da Aplicacao
- A aplicacao deve seguir padrao RESTful nas APIs e MVC nas paginas web.
- Toda evolucao de codigo deve ser aderente a DDD e Clean Architecture, respeitando separacao de camadas, responsabilidades e baixo acoplamento.
- Integracoes externas (pagamento, SEFAZ, assinatura, storage): encapsular em servicos na Application ou Infra; nao expor detalhes nos controllers.

## User Stories (US)
- Se houver documentacao de US, manter em `docs/us/<Modulo>/` com o padrao `<id>-<acao>.md` (exemplo: `docs/us/Client/001-cadastro-cliente.md`).
- Manter `docs/us/index.md` atualizado com links agrupados por modulo, se o indice existir.
- Sincronizar referencias no `README.md` quando adicionar ou alterar documentacao em `docs/`.

## Diretrizes de Testes
- Nao ha testes automatizados definidos neste repositorio no momento.
- Se testes forem introduzidos, documentar o framework (ex.: xUnit, NUnit), convencoes de nomenclatura e como executa-los (ex.: `dotnet test`).

## Diretrizes de Commit e Pull Request
### Commits semanticos (PT-BR)
- Use este formato com icone e descricao em Portugues:
  - `<icone> Klyon Hub - <categoria>: <descricao>`
- Categorias aceitas:
  - `chore` (🧹), `feat` (✨), `bugfix` (🐛), `docs` (📚), `test` (🧪), `build` (⚙️), `ci` (🔧), `deps` (📦), `perf` (🚀), `remove` (🔥), `style` (🎨), `refactor` (♻️)
- Exemplos:
  - `🧹 Klyon Hub - chore: Ajustar configuracao do Docker`
  - `✨ Klyon Hub - feat: Cadastro de cliente com CNPJ`
  - `🐛 Klyon Hub - bugfix: Corrigir validacao de endereco fiscal`
  - `📚 Klyon Hub - docs: Atualizar guia de deploy`
  - `🧪 Klyon Hub - test: Testes de integracao para AuthService`
  - `⚙️ Klyon Hub - build: Ajustar pipeline de build`
  - `🔧 Klyon Hub - ci: Atualizar workflow de CI`
  - `📦 Klyon Hub - deps: Atualizar pacotes NuGet`
  - `🚀 Klyon Hub - perf: Melhorar performance da listagem de contratos`
  - `🔥 Klyon Hub - remove: Remover codigo legado de webhook`
  - `🎨 Klyon Hub - style: Padronizar formatacao do projeto`
  - `♻️ Klyon Hub - refactor: Extrair logica de AdminController para servicos`
- Pull requests devem incluir uma descricao clara da mudanca e, quando aplicavel, referencias a documentacao em `docs/`.

## Tratamento de Assets (Draw.io)
- Para arquivos `*.drawio`, manter os diagramas organizados e legiveis; agrupar entidades relacionadas e rotular relacionamentos.
- Se exportar imagens (exemplo: `*.png`), armazena-las ao lado do diagrama de origem e informar a data da exportacao na descricao do PR.

## Documentacao de Referencia
- Configuracao de email: `docs/README_EMAIL.md`
- Configuracao de pagamento: `docs/CONFIGURAR_PAGAMENTO.md`
- Docker: `docs/DOCKER.md`
- Deploy: `docs/DEPLOY.md`
- Oracle Object Storage: `docs/ORACLE_OBJECT_STORAGE.md`
- Cache: `docs/CACHE.md`
- Validacao em controllers (sem regras de negocio, sem acesso direto ao DB): `docs/VALIDACAO-CONTROLLERS.md`
