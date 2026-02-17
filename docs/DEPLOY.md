# Guia de Deploy - Klyon Endereço Fiscal

## 📋 Pré-requisitos

- .NET 8.0 SDK instalado
- MariaDB 10.5+ instalado e rodando
- Servidor web (IIS, Nginx, Apache) ou hospedagem em nuvem

## 🔧 Configuração do Banco de Dados

1. Crie o banco de dados no MariaDB:
```sql
CREATE DATABASE klyon_hub_platform CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

2. Configure a string de conexão no `appsettings.json` ou variáveis de ambiente:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=seu_servidor;Database=klyon_hub_platform;User=seu_usuario;Password=sua_senha;Port=3306;"
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
    "Issuer": "KlyonHubPlatform",
    "Audience": "KlyonHubPlatform_Users",
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

## 🌐 Deploy em Produção (Oracle Cloud)

Guia para publicar a aplicação na Oracle Cloud usando **apenas recursos Always Free** (sem custo). Use a **região home** da sua tenancy (ex.: Brazil East - São Paulo). Este guia considera uma instância **Ubuntu** (usuário `ubuntu`); para Oracle Linux use o usuário `opc` e os comandos equivalentes em `dnf`.

**Importante — região home:**  
Compute, block volumes e backups Always Free **só são gratuitos na região home da tenancy**. Não crie recursos em outras regiões se quiser evitar cobrança. Referência: [Always Free Resources](https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm).

---

#### 1. Pré-requisitos no Console Oracle

- Conta Oracle Cloud criada (Free Tier).
- **Região:** confirme que está na sua **região home** (menu superior direito no Console: ex. "Brazil East (São Paulo)").
- Tenha em mãos: chave SSH (par pública/privada) para acessar a VM.

---

#### 2. Criar rede (VCN) — se ainda não existir

1. No Console: **Menu (≡)** → **Networking** → **Virtual Cloud Networks**.
2. Escolha o compartment correto e clique em **Start VCN Wizard** → **Create VCN with Internet Connectivity**.
3. Nome (ex.: `klyon-vcn`), deixe o restante padrão e clique em **Next** → **Create**.
4. Anote a VCN e a **subnet pública** criadas.

---

#### 3. Criar instância Compute (VM) Always Free
https://docs.oracle.com/pt-br/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm

1. **Menu** → **Compute** → **Instances** → **Create Instance**.
2. **Name:** ex. `klyon-hub-1`.
3. **Placement:** mantenha a **região home** e um Availability Domain.
4. **Image and shape:**
   - **Image:** Canonical Ubuntu 22.04 (Always Free-eligible) / Image build: 2025.10.31-0 / Security: Shielded instance
   - **Shape:** clique em **Change Shape**:
     - **Always Free eligible:** marque o filtro.
     - Escolha **VM.Standard.E2.1.Micro** (1 GB RAM, AMD) ou **VM.Standard.A1.Flex** (Ampere; ex. 1 OCPU, 6 GB RAM). O A1.Flex é mais confortável para .NET 8.
5. **Networking:** selecione a VCN e a **subnet pública**; marque **Assign a public IPv4 address**.
6. **Add SSH keys:** faça upload da chave pública ou cole o conteúdo. Guarde a chave privada para conectar por SSH.
7. **Boot volume:** deixe padrão (50 GB conta dos 200 GB Always Free).
8. Clique em **Create**. Aguarde o estado **Running** e anote o **IP público**.

---

#### 4. Liberar portas 80 e 443 no Security List

1. Na página da instância, em **Primary VNIC**, clique no **Subnet**.
2. Na subnet, em **Security Lists**, clique na security list padrão.
3. **Add Ingress Rules:** (adicione uma regra para cada porta)
   - **Source CIDR:** `0.0.0.0/0`
   - **IP Protocol:** TCP
   - **Destination port range:** `80`, depois `443`, e **5000** (para testar a app antes de configurar Nginx).
4. Salve.

---

#### 5. Conectar na VM por SSH (Ubuntu)

**No Windows**, se aparecer "Permissions are too open" na chave privada, no PowerShell execute (uma vez):

```powershell
icacls C:\Klyon\ssh\ssh-key-2026-02-04.key /inheritance:r
icacls C:\Klyon\ssh\ssh-key-2026-02-04.key /grant:r "$($env:USERNAME):(R)"
```

Conectar na VM (substitua o IP pelo IP público da sua instância):

```powershell
ssh -i C:\Klyon\ssh\ssh-key-2026-02-04.key ubuntu@168.75.93.94
```

**Após logar na VM (Ubuntu)**, atualize os pacotes:

```bash
sudo apt-get update
sudo apt-get upgrade -y
```

---

#### 6. Instalar .NET 8 Runtime e MariaDB na VM (Ubuntu)

Na VM, já conectado como `ubuntu`:

```bash
# .NET 8
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-runtime-8.0 aspnetcore-runtime-8.0

# MariaDB (compatível com MySQL)
sudo apt-get install -y mariadb-server
sudo systemctl start mariadb
sudo systemctl enable mariadb
sudo mysql_secure_installation   # defina senha root e opções de segurança
```

**Se o serviço falhar com "No frameworks were found" / "You must install or update .NET to run this application":** o runtime .NET 8 não está instalado ou não foi encontrado. Na VM, execute:

```bash
# Verificar se algo do .NET está instalado
dotnet --list-runtimes

# Instalar repositório Microsoft e .NET 8 Runtime (Ubuntu 22.04)
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-runtime-8.0 aspnetcore-runtime-8.0

# Conferir
dotnet --list-runtimes
```

Deve aparecer **ambos**: `Microsoft.NETCore.App 8.0.x` e `Microsoft.AspNetCore.App 8.0.x`. Se só aparecer AspNetCore, instale o runtime base: `sudo apt-get install -y dotnet-runtime-8.0`. Depois reinicie o serviço:

```bash
sudo systemctl restart klyon-app
sudo systemctl status klyon-app
```

Criar banco e usuário para a aplicação:

```bash
sudo mysql -u root -p
```

No MySQL:

```sql
CREATE DATABASE klyon_hub CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'gestorhub'@'localhost' IDENTIFIED BY 'F@z3rd@rC3rt0';
GRANT ALL PRIVILEGES ON klyon_hub.* TO 'gestorhub'@'localhost';
FLUSH PRIVILEGES;
EXIT;
```

**Se aparecer erro de migration ("Table already exists" ou "Unknown column 'Category'"):** a aplicação tenta recuperar sozinha (marca a migration como aplicada e aplica as seguintes). Se ainda falhar, recrie o banco e faça o deploy de novo:

```bash
sudo mariadb -e "
DROP DATABASE IF EXISTS klyon_hub;
CREATE DATABASE klyon_hub CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
GRANT ALL PRIVILEGES ON klyon_hub.* TO 'gestorhub'@'%';
FLUSH PRIVILEGES;
"
```

Depois execute o script de deploy (`.\deploy.ps1`) novamente.

**Se aparecer "Unknown column 'p.InstallmentCount'" (ou outras colunas faltando):** o banco na VM está desatualizado em relação às migrations. Aplicar as migrations pendentes resolve. Se não tiver como rodar `dotnet ef database update` na VM, você pode aplicar o SQL da migration manualmente. Exemplo para as colunas de parcelamento em `Payments`:

Na VM (substitua pela senha do root do MariaDB quando pedido):

```bash
sudo mariadb -u root -p klyon_hub
```

No prompt do MariaDB:

```sql
ALTER TABLE Payments ADD COLUMN InstallmentCount INT NULL;
ALTER TABLE Payments ADD COLUMN InstallmentValue DECIMAL(18,2) NULL;
EXIT;
```

Se aparecer "Duplicate column name", a coluna já existe — pode ignorar e seguir para a próxima. Depois reinicie o app: `sudo systemctl restart klyon-app`.

Para garantir que **todas** as migrations estejam aplicadas, rode na **sua máquina (Windows)**, onde está o .NET SDK. Na VM só há o Runtime, então `dotnet ef` não está disponível lá. Use a connection string do banco da VM no `appsettings` (temporariamente) e execute na raiz do repositório:

```powershell
dotnet ef database update --project KlyonHubPlatform.Infra.Data --startup-project KlyonHubPlatform.Api
```

---

#### 7. Publicar a aplicação no seu computador

**Opção rápida (script):** na raiz do repositório há `deploy.bat` e `deploy.ps1`. Duplo clique em **deploy.bat** (ou execute `.\deploy.ps1` no PowerShell) para: publicar, enviar os arquivos para a VM, apagar a pasta `publish` local e reiniciar o serviço `klyon-app` na VM. Exige a chave em `ssh\ssh-key-2026-02-04.key` e o IP da VM em `deploy.ps1` (variável `$VmIp`).

**Manual:** na raiz do repositório (Windows):

```powershell
dotnet publish KlyonHubPlatform.Api\KlyonHubPlatform.Api.csproj -c Release -o .\publish
```

Crie `appsettings.Production.json` na pasta `publish` (ou edite depois na VM) com a connection string e JWT de produção. Exemplo:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=klyon_hub;User=klyonapp;Password=SuaSenhaSeguraAqui;Port=3306;"
  },
  "JwtSettings": {
    "SecretKey": "SUA_CHAVE_JWT_SEGURA_PRODUCAO",
    "Issuer": "KlyonHubPlatform",
    "Audience": "KlyonHubPlatform_Users",
    "ExpirationMinutes": 1440
  }
}
```

---

#### 8. Enviar os arquivos para a VM

**Na VM (Ubuntu)**, crie a pasta antes (uma vez):

```bash
mkdir -p /home/ubuntu/app
mkdir -p /home/ubuntu/app/wwwroot/documents
```

**No seu computador Windows** (PowerShell, na pasta que contém `publish`), envie os arquivos (substitua o IP pelo da sua instância):

```powershell
scp -i C:\Klyon\ssh\ssh-key-2026-02-04.key -r publish/* ubuntu@168.75.93.94:/home/ubuntu/app/
```

Ou, a partir da raiz do repositório onde está a pasta `publish`:

```powershell
scp -i C:\Klyon\ssh\ssh-key-2026-02-04.key -r .\publish\* ubuntu@168.75.93.94:/home/ubuntu/app/
```

Na VM, ajuste permissões se necessário: `chmod -R 755 /home/ubuntu/app`

Se preferir, compacte `publish` em ZIP, envie o ZIP por `scp` e descompacte na VM com `unzip`.

---

#### 9. Rodar a aplicação na VM (Ubuntu)

Na VM:

```bash
cd /home/ubuntu/app
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS=http://0.0.0.0:5000
dotnet KlyonHubPlatform.Api.dll
```

Teste no navegador: `http://168.75.93.94:5000` (use o IP público da sua instância).

**Para servir na porta 80 (http://IP/):** use Nginx como proxy reverso para a app na 5000.

1. Instalar Nginx (se ainda não instalou): `sudo apt install -y nginx`

2. Criar configuração do site (proxy para a app .NET):

```bash
sudo nano /etc/nginx/sites-available/klyon-app
```

Cole o conteúdo abaixo (ajuste `server_name` se tiver domínio; senão o `_` atende por IP):

```
server {
    listen 80;
    server_name _;
    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

Salve (Ctrl+O, Enter, Ctrl+X).

3. Ativar o site e recarregar o Nginx:

```bash
sudo ln -sf /etc/nginx/sites-available/klyon-app /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t && sudo systemctl reload nginx
```

4. Confirme que a aplicação está rodando na 5000 (systemd ou manualmente). Acesse no navegador: `http://168.75.93.94/`

5. Se ainda não abrir, verifique: **Oracle Cloud** → Security List da subnet → Ingress Rules com porta **80** (Source `0.0.0.0/0`). Na VM: `sudo ss -tlnp | grep -E ':80|:5000'`

Cerificar se retorna 200:
curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:5000

liberar porta 80 no ufw:
sudo ufw allow 80/tcp
sudo ufw status
sudo ufw reload




**Executar migrações (uma vez):** na sua máquina, com a connection string de produção em `appsettings.json` ou variável de ambiente:

```bash
dotnet ef database update --project KlyonHubPlatform.Api
```

Ou use a área **Database Manager** da própria aplicação após o primeiro acesso.

---

#### 10. Manter a aplicação rodando (systemd) — opcional (Ubuntu)

Na VM:

```bash
sudo nano /etc/systemd/system/klyon-app.service
```

Conteúdo (usuário e caminhos para Ubuntu):

```ini
[Unit]
Description=Klyon Hub Platform .NET 8 App
After=network.target mariadb.service

[Service]
WorkingDirectory=/home/ubuntu/app
ExecStart=/usr/bin/dotnet /home/ubuntu/app/KlyonHubPlatform.Api.dll
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000
# Connection string (ajuste User/Password se necessário); evita "Access denied for user ''"
Environment=ConnectionStrings__DefaultConnection=Server=localhost;Database=klyon_hub;Uid=gestorhub;Pwd=SUA_SENHA_MARIADB;Port=3306;
User=ubuntu

[Install]
WantedBy=multi-user.target
```

Ativar e iniciar:

```bash
sudo systemctl daemon-reload
sudo systemctl enable klyon-app
sudo systemctl start klyon-app
sudo systemctl status klyon-app
```

---

#### 11. Resumo rápido — Oracle Cloud Always Free

| Etapa | Ação |
|-------|------|
| 1 | Criar VCN com internet (se não existir) |
| 2 | Criar instância Always Free na **região home** (E2.1.Micro ou A1.Flex) |
| 3 | Abrir portas 80 e 443 na Security List da subnet |
| 4 | Conectar por SSH e instalar .NET 8 + MariaDB; criar banco e usuário |
| 5 | Publicar localmente: `dotnet publish ... -o .\publish` e configurar `appsettings.Production.json` |
| 6 | Enviar conteúdo de `publish` para `/home/ubuntu/app` (scp ou ZIP) |
| 7 | Criar `wwwroot/documents` e rodar migrações |
| 8 | Executar `dotnet KlyonHubPlatform.Api.dll` (ou configurar systemd) |

**Nota:** Se o MySQL HeatWave estiver disponível na sua região no Console (Database → MySQL), você pode criar um sistema Always Free e usar o host de conexão dele na connection string em vez de `localhost` + MariaDB na VM.

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


COMANDOS DENTRO DA VM:
Comando para ver logs:
sudo journalctl -u klyon-app --since "1 minute ago"
sudo journalctl -u klyon-app --since "2 hours ago"

Comando para acessar o banco Mariadb:
sudo mariadb

