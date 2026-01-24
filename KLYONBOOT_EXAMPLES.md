# 💡 KlyonBoot - Exemplos de Uso

## 📝 Exemplos de Perguntas e Respostas

### ✅ Perguntas que o KlyonBoot Responde (com FAQ atual)

#### Cadastro
```
Usuário: "Como me cadastrar como parceiro?"
Bot: "Para se cadastrar como parceiro na Klyon:
      1. Acesse nossa plataforma
      2. Clique em 'Cadastrar Coworking'
      3. Preencha seus dados: CNPJ, razão social, endereço..."
```

```
Usuário: "Preciso de quais documentos para cadastrar?"
Bot: "Documentos necessários para cadastro de parceiros:
      ✅ CNPJ da empresa
      ✅ Contrato Social ou CNPJ completo..."
```

#### Reservas
```
Usuário: "Como faço uma reserva?"
Bot: "Para fazer uma reserva:
      1. Faça login na plataforma
      2. Busque coworkings pela localização desejada..."
```

```
Usuário: "Posso cancelar minha reserva?"
Bot: "Sim! Você pode cancelar sua reserva:
      • Até 24h antes: reembolso de 100%..."
```

#### Pagamentos
```
Usuário: "Quais formas de pagamento vocês aceitam?"
Bot: "Aceitamos as seguintes formas de pagamento:
      💳 Cartão de crédito (Visa, Mastercard, Elo)
      💰 PIX (aprovação instantânea)..."
```

```
Usuário: "Qual a taxa da Klyon?"
Bot: "A Klyon cobra uma taxa de intermediação de:
      • 15% sobre o valor de cada reserva..."
```

---

## ❌ Exemplos de Perguntas NÃO Respondidas (Logar para melhorias)

```
Usuário: "Como exportar relatórios financeiros?"
Bot: "No momento não tenho essa informação. 
      Posso te direcionar para o atendimento humano se quiser."
      
→ Sistema registra em ChatUnansweredQuestions
→ Intenção detectada: "unknown"
→ Categoria sugerida: "Geral"
→ Frequência: 1
```

```
Usuário: "Posso integrar com meu sistema ERP?"
Bot: "No momento não tenho essa informação..."
      
→ Sistema registra como pergunta não respondida
→ Admin pode revisar e adicionar ao FAQ
```

---

## 🔍 Exemplos de Detecção de Intenções

### Cadastro de Parceiro
```javascript
Mensagem: "quero virar parceiro"
Intenção: "cadastro_parceiro"
Confiança: 0.85
Categoria: "Cadastro"
```

### Consultar Reserva
```javascript
Mensagem: "onde vejo minhas reservas?"
Intenção: "consultar_reserva"
Confiança: 0.75
Categoria: "Reservas"
Entidades: {}
```

### Pagamento
```javascript
Mensagem: "aceita pix?"
Intenção: "pagamento"
Confiança: 0.70
Categoria: "Pagamentos"
```

### Cancelamento
```javascript
Mensagem: "preciso cancelar minha reserva do dia 25/01"
Intenção: "cancelamento"
Confiança: 0.90
Categoria: "Suporte"
Entidades: { "date": "25/01" }
```

---

## 🎯 Casos de Uso Práticos

### Caso 1: Novo Parceiro Querendo se Cadastrar

**Fluxo:**
```
1. Usuário: "Olá"
   Bot: "Olá! 😊 Sou o KlyonBoot, assistente oficial..."

2. Usuário: "Como me cadastro como parceiro?"
   Bot: [Resposta completa do FAQ sobre cadastro]
   Sistema: 
   - Registra intenção "cadastro_parceiro"
   - Marca mensagem como "respondida"
   - Incrementa contador de uso do FAQ

3. Usuário: "Quais documentos preciso?"
   Bot: [Resposta do FAQ sobre documentos]

4. Sistema pergunta: "Consegui ajudar você?"
   Usuário: 👍 "Sim, ajudou!"
   Sistema:
   - Marca conversa como "resolvida"
   - Encerra conversa
```

### Caso 2: Cliente com Dúvida Não Cadastrada no FAQ

**Fluxo:**
```
1. Usuário: "Posso pagar em dinheiro?"
   Sistema:
   - Busca no FAQ: não encontra match
   - Detecta intenção: "pagamento" (0.55 confiança)
   - Não atinge confiança mínima (60%)
   
   Bot: "No momento não tenho essa informação.
         Posso te direcionar para o atendimento humano..."
   
   Sistema:
   - Registra em ChatUnansweredQuestions
   - Question: "Posso pagar em dinheiro?"
   - DetectedIntention: "pagamento"
   - SuggestedCategory: "Pagamentos"
   - Status: "pending"
   - Frequency: 1

2. Usuário: "Sim, preciso falar com alguém"
   Bot: "Entendo. Você pode entrar em contato pelo email
         suporte@klyon.com.br ou WhatsApp..."
   
   Sistema:
   - Marca WasRedirectedToHuman = true
```

### Caso 3: Admin Revisando Perguntas Não Respondidas

**Dashboard:**
```
Admin acessa: /admin/chatbot/unanswered-questions

Vê:
┌──────────────────────────────────────────────────────────────┐
│ Pergunta                         │ Freq │ Categoria          │
├──────────────────────────────────────────────────────────────┤
│ "Posso pagar em dinheiro?"       │ 15x  │ Pagamentos        │
│ "Como exportar relatórios?"      │ 12x  │ Geral             │
│ "Tem desconto para anual?"       │ 8x   │ Informações       │
└──────────────────────────────────────────────────────────────┘

Admin clica em "Revisar":
1. Analisa a pergunta
2. Adiciona resposta ao FAQ (ou não)
3. Muda status para "resolved" ou "added_to_faq"
4. Adiciona notas: "Resposta adicionada ao FAQ de Pagamentos"
```

---

## 📊 Exemplos de Queries de Análise

### Análise 1: Quais categorias têm mais dúvidas?

```sql
SELECT 
    SuggestedCategory,
    COUNT(*) AS TotalDuvidas,
    SUM(Frequency) AS FrequenciaTotal
FROM ChatUnansweredQuestions
WHERE Status = 'pending'
GROUP BY SuggestedCategory
ORDER BY FrequenciaTotal DESC;
```

**Resultado:**
```
┌────────────────┬──────────────┬─────────────────┐
│ Categoria      │ Total Dúvidas│ Freq. Total     │
├────────────────┼──────────────┼─────────────────┤
│ Pagamentos     │ 23           │ 145             │
│ Geral          │ 18           │ 98              │
│ Reservas       │ 12           │ 67              │
└────────────────┴──────────────┴─────────────────┘
```

**Ação:** Priorizar expansão do FAQ de Pagamentos

### Análise 2: Taxa de resolução está melhorando?

```sql
SELECT 
    DATE(StartedAt) AS Data,
    COUNT(*) AS TotalConversas,
    ROUND((SUM(CASE WHEN WasResolved = 1 THEN 1 ELSE 0 END) * 100.0) / COUNT(*), 2) AS TaxaResolucao
FROM ChatConversations
WHERE StartedAt >= DATE_SUB(CURDATE(), INTERVAL 30 DAY)
GROUP BY DATE(StartedAt)
ORDER BY Data;
```

**Resultado:**
```
┌────────────┬─────────────────┬────────────────┐
│ Data       │ Total Conversas │ Taxa Resolução │
├────────────┼─────────────────┼────────────────┤
│ 2026-01-01 │ 45              │ 62.22%         │
│ 2026-01-02 │ 52              │ 65.38%         │
│ 2026-01-03 │ 48              │ 68.75%         │
│ ...        │ ...             │ ...            │
│ 2026-01-24 │ 67              │ 79.10%         │ ← Melhorando!
└────────────┴─────────────────┴────────────────┘
```

---

## 🧪 Exemplos de Testes

### Teste 1: Conversa Completa (Postman/cURL)

```bash
# 1. Iniciar conversa
curl -X POST http://localhost:5000/api/chatbot/start \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "test_session_001",
    "userId": null
  }'

# Response:
# {
#   "id": 1,
#   "sessionId": "test_session_001",
#   "startedAt": "2026-01-24T10:00:00",
#   "isActive": true
# }

# 2. Enviar mensagem
curl -X POST http://localhost:5000/api/chatbot/message \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "test_session_001",
    "message": "Como me cadastrar como parceiro?"
  }'

# Response:
# {
#   "conversationId": 1,
#   "messageId": 2,
#   "response": "Para se cadastrar como parceiro...",
#   "wasAnswered": true,
#   "usedAI": true,
#   "confidenceScore": 0.85,
#   "intentionDetected": "cadastro_parceiro",
#   "suggestHumanSupport": false
# }

# 3. Marcar como resolvido
curl -X POST http://localhost:5000/api/chatbot/conversation/1/resolve \
  -H "Content-Type: application/json" \
  -d '{
    "wasResolved": true
  }'
```

### Teste 2: Verificar Estatísticas

```bash
curl http://localhost:5000/api/chatbot/admin/statistics

# Response:
# {
#   "totalConversations": 1250,
#   "resolvedConversations": 987,
#   "redirectedToHuman": 123,
#   "totalMessages": 3456,
#   "answeredMessages": 3210,
#   "unansweredQuestions": 45,
#   "averageMessagesPerConversation": 2.76,
#   "resolutionRate": 78.96
# }
```

---

## 🎨 Exemplos de Customização

### Customizar Mensagem de Boas-Vindas

```csharp
// Em ChatbotService.cs
private const string WELCOME_MESSAGE = 
    "Olá! 👋 Sou o KlyonBoot, seu assistente pessoal na Klyon Coworking!\n\n" +
    "Posso ajudar com:\n" +
    "• Cadastro de parceiros e clientes\n" +
    "• Reservas de espaços\n" +
    "• Pagamentos e taxas\n" +
    "• Endereço fiscal\n\n" +
    "Como posso ajudar você hoje? 😊";
```

### Adicionar Nova Intenção

```csharp
// Em AIIntentionService.cs
private readonly Dictionary<string, List<string>> _intentionKeywords = new()
{
    // ... intenções existentes ...
    
    { "solicitar_orcamento", new List<string> { 
        "orçamento", "cotação", "preço", "valor", "quanto custa", "proposta" 
    }},
};

private readonly Dictionary<string, string> _intentionToCategory = new()
{
    // ... mapeamentos existentes ...
    
    { "solicitar_orcamento", "Comercial" },
};
```

### Adicionar Novo Item ao FAQ

```csharp
// Em ChatbotSeedService.cs
new ChatFaqItem
{
    Category = "Comercial",
    Question = "Como solicitar um orçamento personalizado?",
    Answer = "Para solicitar um orçamento:\n\n" +
             "1. Acesse 'Contato' no menu\n" +
             "2. Preencha o formulário com suas necessidades\n" +
             "3. Nossa equipe comercial responderá em até 24h\n\n" +
             "Ou envie e-mail para: comercial@klyon.com.br 📧",
    Keywords = "orçamento,cotação,preço,proposta,solicitar,comercial",
    Intentions = "solicitar_orcamento,informacao_preco",
    DisplayOrder = 17,
    IsActive = true,
    CreatedAt = DateTime.Now
}
```

---

## 🚀 Integração com Outras Plataformas

### Exemplo: Webhook para Slack (Notificar perguntas não respondidas)

```csharp
// Criar um serviço de notificação
public class SlackNotificationService
{
    public async Task NotifyUnansweredQuestionAsync(string question, int frequency)
    {
        var slackWebhookUrl = "https://hooks.slack.com/services/YOUR/WEBHOOK/URL";
        
        var payload = new
        {
            text = $"⚠️ *Nova pergunta não respondida*",
            attachments = new[]
            {
                new
                {
                    color = "warning",
                    fields = new[]
                    {
                        new { title = "Pergunta", value = question, @short = false },
                        new { title = "Frequência", value = $"{frequency}x", @short = true }
                    }
                }
            }
        };
        
        using var client = new HttpClient();
        var json = JsonSerializer.Serialize(payload);
        await client.PostAsync(slackWebhookUrl, new StringContent(json, Encoding.UTF8, "application/json"));
    }
}

// No ChatbotService, após registrar pergunta não respondida:
if (unansweredQuestion.Frequency >= 5)
{
    await _slackService.NotifyUnansweredQuestionAsync(
        unansweredQuestion.Question, 
        unansweredQuestion.Frequency
    );
}
```

---

**🎉 Esses exemplos cobrem os principais casos de uso do KlyonBoot!**

Para mais informações, consulte:
- `KLYONBOOT_README.md` - Documentação completa
- `KLYONBOOT_SETUP.md` - Guia de instalação
- `klyonboot_schema.sql` - Schema do banco de dados

---

**Desenvolvido com ❤️ pela equipe Klyon**
