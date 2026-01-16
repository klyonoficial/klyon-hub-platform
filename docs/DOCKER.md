# 🐳 Guia Docker - Klyon Endereço Fiscal

Este guia explica como executar o sistema usando Docker.

## 📋 Pré-requisitos

- Docker Desktop instalado (Windows/Mac) ou Docker Engine (Linux)
- Docker Compose instalado (geralmente vem com Docker Desktop)

## 🚀 Opção 1: Executar Apenas o Banco de Dados (Recomendado para Desenvolvimento)

Esta opção executa apenas o MariaDB no Docker, enquanto você roda a aplicação localmente.

### Passos:

1. **Inicie o banco de dados:**
```bash
docker-compose -f docker-compose.dev.yml up -d
```

2. **Verifique se o container está rodando:**
```bash
docker ps
```

3. **Atualize o `appsettings.json` com a string de conexão:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=klyon_hub_platform;User=klyon_user;Password=klyon_pass;Port=3306;"
  }
}
```

4. **Execute a aplicação localmente:**
```bash
dotnet run
```

### Parar o banco de dados:
```bash
docker-compose -f docker-compose.dev.yml down
```

### Remover volumes (apaga os dados):
```bash
docker-compose -f docker-compose.dev.yml down -v
```

## 🐳 Opção 2: Executar Tudo no Docker (Aplicação + Banco)

Esta opção executa tanto a aplicação quanto o banco de dados em containers Docker.

### Passos:

1. **Construa e inicie todos os serviços:**
```bash
docker-compose up -d --build
```

2. **Verifique os logs:**
```bash
docker-compose logs -f
```

3. **Acesse a aplicação:**
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001

### Comandos úteis:

**Parar todos os serviços:**
```bash
docker-compose down
```

**Parar e remover volumes:**
```bash
docker-compose down -v
```

**Ver logs:**
```bash
docker-compose logs -f app
docker-compose logs -f mariadb
```

**Reiniciar um serviço específico:**
```bash
docker-compose restart app
docker-compose restart mariadb
```

## 🔧 Configurações do Banco de Dados

### Credenciais Padrão (Docker):

- **Host:** localhost (ou `mariadb` se dentro da rede Docker)
- **Porta:** 3306
- **Database:** klyon_hub_platform
- **Usuário:** klyon_user
- **Senha:** klyon_pass
- **Root Password:** root123

### Alterar Credenciais:

Edite o arquivo `docker-compose.yml` ou `docker-compose.dev.yml`:

```yaml
environment:
  MYSQL_ROOT_PASSWORD: sua_senha_root
  MYSQL_DATABASE: klyon_hub_platform
  MYSQL_USER: seu_usuario
  MYSQL_PASSWORD: sua_senha
```

E atualize o `appsettings.json` correspondente.

## 📊 Acessar o Banco de Dados

### Via Docker Exec:

```bash
docker exec -it klyon-mariadb mysql -u klyon_user -pklyon_pass klyon_hub_platform
```

### Via Cliente MySQL:

Use qualquer cliente MySQL (MySQL Workbench, DBeaver, etc.) com:
- Host: localhost
- Port: 3306
- User: klyon_user
- Password: klyon_pass
- Database: klyon_hub_platform

## 🗄️ Gerenciar Volumes

### Listar volumes:
```bash
docker volume ls
```

### Inspecionar volume:
```bash
docker volume inspect klyon_ef_mariadb_data
```

### Backup do banco:
```bash
docker exec klyon-mariadb mysqldump -u klyon_user -pklyon_pass klyon_hub_platform > backup.sql
```

### Restaurar backup:
```bash
docker exec -i klyon-mariadb mysql -u klyon_user -pklyon_pass klyon_hub_platform < backup.sql
```

## 🔍 Troubleshooting

### Container não inicia:

1. Verifique os logs:
```bash
docker-compose logs mariadb
```

2. Verifique se a porta 3306 está livre:
```bash
# Windows
netstat -ano | findstr :3306

# Linux/Mac
lsof -i :3306
```

### Erro de conexão:

1. Verifique se o container está rodando:
```bash
docker ps
```

2. Verifique a saúde do container:
```bash
docker inspect klyon-mariadb | grep Health
```

3. Teste a conexão:
```bash
docker exec -it klyon-mariadb mysql -u klyon_user -pklyon_pass -e "SELECT 1;"
```

### Resetar tudo:

```bash
# Parar e remover tudo
docker-compose down -v

# Remover imagens
docker rmi klyon-ef-app

# Iniciar novamente
docker-compose up -d --build
```

## 📝 Notas Importantes

- Os dados do banco são persistidos no volume Docker `mariadb_data`
- O script `init.sql` é executado automaticamente na primeira inicialização
- A aplicação aguarda o banco estar saudável antes de iniciar (healthcheck)
- Documentos gerados são salvos em `wwwroot/documents` (montado como volume)

## 🚀 Produção

Para produção, considere:

1. Alterar senhas padrão
2. Usar secrets do Docker
3. Configurar SSL/TLS para o banco
4. Configurar backups automáticos
5. Usar um servidor de aplicação (Nginx, IIS) como proxy reverso
6. Configurar variáveis de ambiente seguras
