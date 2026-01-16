# Configuração de Email

Este documento explica como configurar o serviço de email do sistema Klyon Endereço Fiscal.

## Configuração no appsettings.json

As configurações de email devem ser adicionadas na seção `EmailSettings` do arquivo `appsettings.json`:

```json
{
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": "587",
    "SmtpUsername": "seu-email@gmail.com",
    "SmtpPassword": "sua-senha-de-app",
    "FromEmail": "noreply@klyon.com.br",
    "FromName": "Klyon Endereço Fiscal",
    "EnableSsl": "true"
  },
  "AppSettings": {
    "BaseUrl": "https://localhost:5001"
  }
}
```

## Configuração para Gmail

### 1. Criar Senha de App

1. Acesse sua conta Google: https://myaccount.google.com/
2. Vá em **Segurança**
3. Ative a **Verificação em duas etapas** (obrigatório)
4. Role até **Senhas de app**
5. Selecione **App** e **Outro (nome personalizado)**
6. Digite "Klyon EF" e clique em **Gerar**
7. Copie a senha gerada (16 caracteres)

### 2. Configurar appsettings.json

```json
{
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": "587",
    "SmtpUsername": "seu-email@gmail.com",
    "SmtpPassword": "senha-de-app-gerada",
    "FromEmail": "seu-email@gmail.com",
    "FromName": "Klyon Endereço Fiscal",
    "EnableSsl": "true"
  }
}
```

## Configuração para Outlook/Hotmail

```json
{
  "EmailSettings": {
    "SmtpServer": "smtp-mail.outlook.com",
    "SmtpPort": "587",
    "SmtpUsername": "seu-email@outlook.com",
    "SmtpPassword": "sua-senha",
    "FromEmail": "seu-email@outlook.com",
    "FromName": "Klyon Endereço Fiscal",
    "EnableSsl": "true"
  }
}
```

## Configuração para SendGrid

```json
{
  "EmailSettings": {
    "SmtpServer": "smtp.sendgrid.net",
    "SmtpPort": "587",
    "SmtpUsername": "apikey",
    "SmtpPassword": "SUA_API_KEY_DO_SENDGRID",
    "FromEmail": "noreply@klyon.com.br",
    "FromName": "Klyon Endereço Fiscal",
    "EnableSsl": "true"
  }
}
```

## Configuração para Amazon SES

```json
{
  "EmailSettings": {
    "SmtpServer": "email-smtp.us-east-1.amazonaws.com",
    "SmtpPort": "587",
    "SmtpUsername": "SUA_ACCESS_KEY",
    "SmtpPassword": "SUA_SECRET_KEY",
    "FromEmail": "noreply@klyon.com.br",
    "FromName": "Klyon Endereço Fiscal",
    "EnableSsl": "true"
  }
}
```

## Configuração BaseUrl

O `BaseUrl` em `AppSettings` deve apontar para a URL base da sua aplicação:

- **Desenvolvimento**: `https://localhost:5001` ou `http://localhost:5000`
- **Produção**: `https://seu-dominio.com.br`

## Funcionalidades Implementadas

O serviço de email está configurado para enviar:

1. **Email de Verificação**: Enviado automaticamente após o cadastro
2. **Reenvio de Verificação**: Disponível na página `/Auth/ResendVerification`
3. **Redefinição de Senha**: Enviado quando o usuário solicita recuperação de senha

## Testando o Envio de Email

1. Configure as credenciais no `appsettings.json`
2. Reinicie a aplicação
3. Faça um cadastro de novo usuário
4. Verifique a caixa de entrada do email informado
5. Se não receber, verifique a pasta de spam

## Troubleshooting

### Erro: "Falha ao enviar email"

- Verifique se as credenciais estão corretas
- Para Gmail, certifique-se de usar uma **Senha de App**, não a senha normal
- Verifique se a porta e o servidor SMTP estão corretos
- Verifique se o firewall não está bloqueando a conexão

### Email não chega

- Verifique a pasta de spam
- Verifique se o email de destino está correto
- Verifique os logs da aplicação para erros
- Teste com outro provedor de email

### Gmail bloqueando acesso

- Ative a verificação em duas etapas
- Use uma Senha de App específica
- Não use a senha normal da conta Google

## Segurança

⚠️ **IMPORTANTE**: Nunca commite o arquivo `appsettings.json` com credenciais reais no Git. Use variáveis de ambiente ou `appsettings.Development.json` (que deve estar no `.gitignore`).
