# 🤖 KlyonBoot - Assistente Oficial da Klyon Coworking

## 📋 Sobre

O **KlyonBoot** é um chatbot híbrido (regras + IA) desenvolvido para a plataforma Klyon Coworking. Ele auxilia clientes e parceiros com dúvidas sobre cadastro, reservas, pagamentos, endereço fiscal e uso da plataforma.

### 🎯 Características Principais

- ✅ **Base de conhecimento estruturada (FAQ)**
- ✅ **Interpretação de intenções via IA**
- ✅ **Logs de perguntas não respondidas**
- ✅ **Dashboard administrativo com métricas**
- ✅ **Feedback do usuário (resolveu/não resolveu)**
- ✅ **Sugestões automáticas de melhorias para FAQ**
- ✅ **Widget de chat responsivo**
- ✅ **Controle total das respostas**

---

## 🏗️ Arquitetura

### Camadas do Projeto

```
KlyonHubPlatform.Domain/
├── Entities/
│   ├── ChatFaqItem.cs          # Base de conhecimento (FAQ)
│   ├── ChatConversation.cs     # Conversas
│   ├── ChatMessage.cs          # Mensagens
│   ├── ChatIntention.cs        # Intenções detectadas
│   └── ChatUnansweredQuestion.cs # Perguntas não respondidas
└── Interfaces/
    └── IChatbotRepository.cs

KlyonHubPlatform.Infra.Data/
├── Context/
│   └── ApplicationDbContext.cs # DbContext com DbSets do KlyonBoot
├── Repositories/
│   └── ChatbotRepository.cs
└── Migrations/
    └── 20260124000000_AddKlyonBootChatbotTables.cs

KlyonHubPlatform.Application/
├── Interfaces/
│   ├── IChatbotService.cs
│   └── IAIIntentionService.cs
├── Services/
│   ├── ChatbotService.cs       # Lógica principal do chatbot
│   ├── AIIntentionService.cs   # Detecção de intenções (IA)
│   └── ChatbotSeedService.cs   # Seed data (FAQ inicial)
└── DTOs/
    └── ChatbotDtos.cs

KlyonHubPlatform.Api/
├── Controllers/
│   ├── ChatbotController.cs      # API pública do chatbot
│   └── ChatbotAdminController.cs # Painel administrativo
├── Views/
│   └── ChatbotAdmin/
│       ├── Dashboard.cshtml
│       └── UnansweredQuestions.cshtml
└── wwwroot/
    ├── css/
    │   └── klyonboot.css
    └── js/
        └── klyonboot.js        # Widget de chat
```

---

## 🚀 Instalação e Configuração

### 1. Executar Migrations

```bash
cd KlyonHubPlatform.Infra.Data
dotnet ef migrations add AddKlyonBootChatbotTables --startup-project ../KlyonHubPlatform.Api
dotnet ef database update --startup-project ../KlyonHubPlatform.Api
```

### 2. Registrar Dependências (Startup.cs / Program.cs)

```csharp
// No Program.cs ou Startup.cs, adicionar:

using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.Services;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Infra.Data.Repositories;

// Registrar serviços do KlyonBoot
builder.Services.AddScoped<IChatbotRepository, ChatbotRepository>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddScoped<IAIIntentionService, AIIntentionService>();
builder.Services.AddScoped<ChatbotSeedService>();
```

### 3. Popular Base de Conhecimento (FAQ)

Criar um endpoint ou executar uma vez no startup:

```csharp
// Exemplo de endpoint para seed (remover após primeira execução)
[HttpPost("api/chatbot/admin/seed")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> SeedFaq([FromServices] ChatbotSeedService seedService)
{
    await seedService.SeedFaqDataAsync();
    return Ok(new { message = "FAQ populado com sucesso!" });
}
```

### 4. Adicionar Widget ao Layout

No arquivo `_Layout.cshtml` ou em qualquer página:

```html
<!-- CSS do KlyonBoot -->
<link rel="stylesheet" href="~/css/klyonboot.css" />

<!-- No final do body, antes de fechar </body> -->
<script src="~/js/klyonboot.js"></script>

<!-- Opcional: Configuração customizada -->
<script>
    // O widget se auto-inicializa, mas você pode customizar:
    // window.klyonboot = new KlyonBoot({
    //     apiBaseUrl: '/api/chatbot',
    //     userId: null // ou ID do usuário logado
    // });
</script>
```

---

## 📡 Endpoints da API

### Endpoints Públicos

#### `POST /api/chatbot/start`
Inicia uma nova conversa

**Request:**
```json
{
  "sessionId": "session_123456",
  "userId": 1 // opcional
}
```

**Response:**
```json
{
  "id": 1,
  "sessionId": "session_123456",
  "startedAt": "2026-01-24T10:00:00",
  "isActive": true,
  "messages": []
}
```

#### `POST /api/chatbot/message`
Envia uma mensagem e recebe resposta

**Request:**
```json
{
  "sessionId": "session_123456",
  "message": "Como me cadastrar como parceiro?",
  "userId": 1 // opcional
}
```

**Response:**
```json
{
  "conversationId": 1,
  "messageId": 2,
  "response": "Para se cadastrar como parceiro...",
  "wasAnswered": true,
  "usedAI": true,
  "confidenceScore": 0.85,
  "intentionDetected": "cadastro_parceiro",
  "suggestHumanSupport": false,
  "sentAt": "2026-01-24T10:01:00"
}
```

#### `POST /api/chatbot/conversation/{id}/resolve`
Marca se a conversa resolveu o problema do usuário

**Request:**
```json
{
  "wasResolved": true
}
```

### Endpoints Administrativos (Requerem autenticação)

#### `GET /api/chatbot/admin/statistics`
Retorna estatísticas do dashboard

**Query Params:**
- `from` (opcional): Data inicial (formato: YYYY-MM-DD)
- `to` (opcional): Data final (formato: YYYY-MM-DD)

**Response:**
```json
{
  "totalConversations": 1250,
  "resolvedConversations": 987,
  "redirectedToHuman": 123,
  "totalMessages": 3456,
  "answeredMessages": 3210,
  "unansweredQuestions": 45,
  "averageMessagesPerConversation": 2.76,
  "resolutionRate": 78.96
}
```

#### `GET /api/chatbot/admin/unanswered-questions`
Lista perguntas não respondidas

**Query Params:**
- `status` (opcional): pending, under_review, resolved, added_to_faq
- `minFrequency` (opcional): Frequência mínima

**Response:**
```json
[
  {
    "id": 1,
    "question": "Como exportar relatórios?",
    "detectedIntention": "unknown",
    "suggestedCategory": "Geral",
    "status": "pending",
    "frequency": 15,
    "createdAt": "2026-01-20T14:30:00",
    "reviewedByName": null,
    "reviewedAt": null,
    "reviewNotes": null
  }
]
```

#### `PUT /api/chatbot/admin/unanswered-questions/{id}`
Atualiza status de uma pergunta não respondida

**Request:**
```json
{
  "status": "resolved",
  "reviewedBy": 1,
  "notes": "Resposta adicionada ao FAQ"
}
```

#### `GET /api/chatbot/admin/top-intentions`
Retorna top intenções detectadas

**Query Params:**
- `from` (opcional): Data inicial
- `to` (opcional): Data final
- `top` (opcional): Número de resultados (padrão: 10)

**Response:**
```json
{
  "cadastro_parceiro": 250,
  "consultar_reserva": 180,
  "pagamento": 150,
  "endereco_fiscal": 120,
  "cancelamento": 90
}
```

---

## 🎨 Interface Administrativa

### Dashboard Principal
**Rota:** `/admin/chatbot/dashboard`

Exibe:
- ✅ Estatísticas gerais (total de conversas, taxa de resolução, etc.)
- ✅ Top 10 perguntas não respondidas
- ✅ Gráfico de intenções mais detectadas
- ✅ Filtros por período

### Perguntas Não Respondidas
**Rota:** `/admin/chatbot/unanswered-questions`

Permite:
- ✅ Listar todas as perguntas não respondidas
- ✅ Filtrar por status e frequência
- ✅ Revisar perguntas (mudar status, adicionar notas)
- ✅ Identificar lacunas no FAQ

---

## 🧠 Como Funciona a IA

### Detecção de Intenções

O **AIIntentionService** usa uma abordagem híbrida:

1. **Pattern Matching:** Busca palavras-chave na mensagem
2. **Normalização:** Remove acentos, converte para minúsculas
3. **Scoring:** Calcula confiança baseada em correspondências
4. **Extração de Entidades:** Identifica CPF, CNPJ, e-mail, telefone, datas

### Matching de FAQ

O **ChatbotService** busca respostas:

1. Busca textual no banco de dados
2. Calcula similaridade (Levenshtein Distance)
3. Verifica se a intenção detectada corresponde às intenções do FAQ
4. Retorna a melhor resposta (confiança mínima: 60%)

### Fluxo de Processamento

```
Usuário envia mensagem
     ↓
Detectar intenção (IA)
     ↓
Buscar no FAQ
     ↓
Encontrou? 
     ├─ SIM → Retornar resposta
     └─ NÃO → Registrar pergunta não respondida
              ↓
         Sugerir atendimento humano
```

---

## 📊 Métricas e Logs

### Dados Registrados

Para cada interação, o KlyonBoot registra:

✅ **Pergunta do usuário**  
✅ **Intenção detectada**  
✅ **Confiança da detecção**  
✅ **Resposta fornecida (ou não)**  
✅ **FAQ usado (se aplicável)**  
✅ **Feedback do usuário (resolveu?)**  
✅ **Redirecionamento para humano**

### Dica de Ouro (Arquitetura)

> **"Nunca deixe a IA responder direto sem:**
> - Logar a pergunta
> - Marcar se respondeu ou não
> - Guardar intenção detectada"

**Isso vira:**
- ✅ **Métrica:** Taxa de resolução, satisfação
- ✅ **Produto:** Identificar lacunas e melhorar FAQ
- ✅ **Diferencial competitivo:** Chatbot que aprende e evolui

---

## 🎯 Roadmap de Evolução

### Fase 1: ✅ Implementado
- Base de conhecimento estruturada
- Detecção de intenções rule-based
- Widget de chat
- Dashboard administrativo
- Logs e métricas

### Fase 2: Próximas melhorias
- [ ] Integração com NLP mais avançado (OpenAI, Gemini)
- [ ] Machine Learning para ranking de respostas
- [ ] Análise de sentimento aprimorada
- [ ] Chatbot multilíngue
- [ ] Integração com WhatsApp/Telegram

### Fase 3: Futuro
- [ ] Chatbot por voz
- [ ] Recomendações personalizadas
- [ ] A/B testing de respostas
- [ ] Auto-treinamento com feedback

---

## 🛠️ Customização

### Adicionar Novas Intenções

1. Editar `AIIntentionService.cs`
2. Adicionar palavras-chave no dicionário `_intentionKeywords`
3. Adicionar mapeamento em `_intentionToCategory`

### Adicionar Novos Itens ao FAQ

1. Opção 1: Via banco de dados (INSERT manual)
2. Opção 2: Adicionar em `ChatbotSeedService.cs`
3. Opção 3: Criar interface administrativa para CRUD de FAQ

### Mudar Mensagens Padrão

Editar constantes em `ChatbotService.cs`:

```csharp
private const string WELCOME_MESSAGE = "Olá! 😊 ...";
private const string NO_ANSWER_MESSAGE = "No momento não tenho...";
private const decimal MINIMUM_CONFIDENCE = 0.6m;
```

### Customizar Visual do Widget

Editar `klyonboot.css`:

```css
:root {
    --klyonboot-primary: #6C5CE7; /* Cor principal */
    --klyonboot-secondary: #A29BFE;
    /* ... */
}
```

---

## 📞 Suporte

Para dúvidas ou problemas com o KlyonBoot:

📧 **E-mail:** dev@klyon.com.br  
💬 **Chat:** Disponível na plataforma  
📱 **WhatsApp:** (11) 9xxxx-xxxx

---

## 📝 Licença

© 2026 Klyon Coworking. Todos os direitos reservados.

---

**Desenvolvido com ❤️ pela equipe Klyon**
