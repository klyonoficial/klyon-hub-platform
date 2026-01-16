# 💳 Guia de Configuração do PagBank (PagSeguro)

Este guia explica como configurar o PagBank para receber pagamentos via cartão de crédito, boleto e PIX.

## 📋 Pré-requisitos

1. Conta no PagBank (PagSeguro)
2. Acesso ao painel do desenvolvedor do PagBank
3. Permissões de administrador no sistema

## 🔑 Passo 1: Obter o Access Token do PagBank

### Para Ambiente Sandbox (Testes):

1. Acesse: https://sandbox.pagseguro.uol.com.br/
2. Faça login com sua conta
3. Vá em **"Integrações"** → **"APIs"**
4. Clique em **"Criar Token"** ou **"Gerar Token"**
5. Copie o token gerado (ele começa com algo como `APP_USR-...`)

### Para Ambiente de Produção:

1. Acesse: https://pagseguro.uol.com.br/
2. Faça login com sua conta
3. Vá em **"Integrações"** → **"APIs"**
4. Clique em **"Criar Token"** ou **"Gerar Token"**
5. Copie o token gerado

⚠️ **Importante**: O token de produção é diferente do token de sandbox!

## ⚙️ Passo 2: Configurar no Sistema

1. Acesse a página **"Configurações do Sistema"** como administrador
2. Localize as configurações da categoria **"Payment"** (Pagamento)
3. Edite as seguintes configurações:

### 2.1. PagBank.AccessToken

- **O que é**: Token de acesso da API PagBank (Bearer Token)
- **Como configurar**:
  1. Clique no botão **"Editar"** ao lado de `PagBank.AccessToken`
  2. Cole o token que você copiou do PagBank
  3. Clique em **"Salvar Alterações"**

⚠️ **Atenção**: Este campo é sensível e será exibido como asteriscos após salvar.

### 2.2. PagBank.UseSandbox

- **O que é**: Define se o sistema usa o ambiente de testes (sandbox) ou produção
- **Valores possíveis**:
  - `true` = Ambiente de testes (sandbox) - **Recomendado para começar**
  - `false` = Ambiente de produção (pagamentos reais)
- **Como configurar**:
  1. Clique no botão **"Editar"** ao lado de `PagBank.UseSandbox`
  2. Digite `true` para testes ou `false` para produção
  3. Clique em **"Salvar Alterações"**

### 2.3. PagBank.WebhookSecret (Opcional)

- **O que é**: Secret para validação de webhooks do PagBank
- **Quando usar**: Se você configurar webhooks para notificações automáticas de pagamento
- **Como obter**: No painel do PagBank, em **"Integrações"** → **"Webhooks"**

### 2.4. Payment.Provider

- **O que é**: Provedor de pagamento ativo
- **Valor padrão**: `PagBank`
- **Não altere** a menos que você queira usar outro provedor

### 2.5. Payment.DefaultCurrency

- **O que é**: Moeda padrão para pagamentos
- **Valor padrão**: `BRL` (Real Brasileiro)
- **Não altere** a menos que necessário

## ✅ Passo 3: Verificar a Configuração

Após configurar, você pode testar:

1. Crie um contrato de teste
2. Tente gerar um pagamento (PIX, Boleto ou Cartão)
3. Verifique se os códigos são gerados corretamente

## 🔍 Troubleshooting

### Erro: "Token de acesso PagBank não configurado"

**Solução**: Verifique se você salvou o `PagBank.AccessToken` corretamente.

### Erro: "Invalid token" ou "Unauthorized"

**Soluções**:
- Verifique se o token está correto (sem espaços extras)
- Verifique se está usando o token correto (sandbox vs produção)
- Verifique se o token não expirou (gere um novo se necessário)

### Pagamentos não estão sendo processados

**Verificações**:
1. Confirme que `PagBank.UseSandbox` está configurado corretamente
2. Verifique se o `PagBank.AccessToken` está válido
3. Verifique os logs do sistema para mais detalhes

## 📚 Recursos Adicionais

- **Documentação PagBank**: https://dev.pagseguro.uol.com.br/
- **API Reference**: https://dev.pagseguro.uol.com.br/reference
- **Suporte PagBank**: https://pagseguro.uol.com.br/atendimento

## 🔒 Segurança

- ⚠️ **Nunca compartilhe** seu Access Token
- ⚠️ **Use Sandbox** para testes antes de ir para produção
- ⚠️ **Mantenha os tokens atualizados** se expirarem
- ⚠️ **Monitore** os pagamentos regularmente
