-- =====================================================
-- KLYONBOOT - SCHEMA DE BANCO DE DADOS
-- Plataforma: MySQL / SQL Server
-- Data: 24/01/2026
-- =====================================================

-- Tabela: ChatFaqItems
-- Base de conhecimento (FAQ) do KlyonBoot
CREATE TABLE ChatFaqItems (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    Category VARCHAR(100) NOT NULL,
    Question VARCHAR(500) NOT NULL,
    Answer VARCHAR(5000) NOT NULL,
    Keywords VARCHAR(1000),
    Intentions VARCHAR(500),
    DisplayOrder INT NOT NULL DEFAULT 0,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    UsageCount INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL,
    UpdatedAt DATETIME NULL,
    INDEX IX_ChatFaqItems_Category (Category),
    INDEX IX_ChatFaqItems_IsActive (IsActive)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Tabela: ChatConversations
-- Conversas completas com o KlyonBoot
CREATE TABLE ChatConversations (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    UserId INT NULL,
    SessionId VARCHAR(100) NOT NULL,
    IpAddress VARCHAR(50) NULL,
    UserAgent VARCHAR(500) NULL,
    StartedAt DATETIME NOT NULL,
    EndedAt DATETIME NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    WasResolved TINYINT(1) NULL,
    WasRedirectedToHuman TINYINT(1) NOT NULL DEFAULT 0,
    INDEX IX_ChatConversations_SessionId (SessionId),
    INDEX IX_ChatConversations_UserId (UserId),
    INDEX IX_ChatConversations_StartedAt (StartedAt),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Tabela: ChatMessages
-- Mensagens individuais nas conversas
CREATE TABLE ChatMessages (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    ConversationId INT NOT NULL,
    MessageType VARCHAR(20) NOT NULL,
    Content VARCHAR(5000) NOT NULL,
    FaqItemId INT NULL,
    WasAnswered TINYINT(1) NOT NULL DEFAULT 0,
    UsedAI TINYINT(1) NOT NULL DEFAULT 0,
    ConfidenceScore DECIMAL(5,4) NULL,
    SentAt DATETIME NOT NULL,
    Metadata VARCHAR(2000) NULL,
    INDEX IX_ChatMessages_ConversationId (ConversationId),
    INDEX IX_ChatMessages_SentAt (SentAt),
    INDEX IX_ChatMessages_FaqItemId (FaqItemId),
    FOREIGN KEY (ConversationId) REFERENCES ChatConversations(Id) ON DELETE CASCADE,
    FOREIGN KEY (FaqItemId) REFERENCES ChatFaqItems(Id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Tabela: ChatIntentions
-- Intenções detectadas pela IA
CREATE TABLE ChatIntentions (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    ConversationId INT NOT NULL,
    MessageId INT NOT NULL,
    IntentionName VARCHAR(100) NOT NULL,
    Category VARCHAR(100) NOT NULL,
    Confidence DECIMAL(5,4) NOT NULL,
    Entities VARCHAR(2000) NULL,
    DetectedAt DATETIME NOT NULL,
    INDEX IX_ChatIntentions_IntentionName (IntentionName),
    INDEX IX_ChatIntentions_Category (Category),
    INDEX IX_ChatIntentions_DetectedAt (DetectedAt),
    INDEX IX_ChatIntentions_ConversationId (ConversationId),
    INDEX IX_ChatIntentions_MessageId (MessageId),
    FOREIGN KEY (ConversationId) REFERENCES ChatConversations(Id) ON DELETE CASCADE,
    FOREIGN KEY (MessageId) REFERENCES ChatMessages(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Tabela: ChatUnansweredQuestions
-- Perguntas não respondidas - crucial para melhorias
CREATE TABLE ChatUnansweredQuestions (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    ConversationId INT NOT NULL,
    MessageId INT NOT NULL,
    Question VARCHAR(1000) NOT NULL,
    DetectedIntention VARCHAR(100) NULL,
    SuggestedCategory VARCHAR(100) NULL,
    Status VARCHAR(50) NOT NULL DEFAULT 'pending',
    ReviewedBy INT NULL,
    ReviewedAt DATETIME NULL,
    ReviewNotes VARCHAR(2000) NULL,
    Frequency INT NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL,
    INDEX IX_ChatUnansweredQuestions_Status (Status),
    INDEX IX_ChatUnansweredQuestions_Frequency (Frequency),
    INDEX IX_ChatUnansweredQuestions_CreatedAt (CreatedAt),
    INDEX IX_ChatUnansweredQuestions_ConversationId (ConversationId),
    INDEX IX_ChatUnansweredQuestions_MessageId (MessageId),
    INDEX IX_ChatUnansweredQuestions_ReviewedBy (ReviewedBy),
    FOREIGN KEY (ConversationId) REFERENCES ChatConversations(Id) ON DELETE CASCADE,
    FOREIGN KEY (MessageId) REFERENCES ChatMessages(Id) ON DELETE CASCADE,
    FOREIGN KEY (ReviewedBy) REFERENCES Users(Id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- =====================================================
-- QUERIES ÚTEIS
-- =====================================================

-- 1. Ver total de conversas por dia
SELECT 
    DATE(StartedAt) AS Data,
    COUNT(*) AS TotalConversas,
    SUM(CASE WHEN WasResolved = 1 THEN 1 ELSE 0 END) AS Resolvidas,
    SUM(CASE WHEN WasRedirectedToHuman = 1 THEN 1 ELSE 0 END) AS RedirecionadasHumano
FROM ChatConversations
WHERE StartedAt >= DATE_SUB(CURDATE(), INTERVAL 30 DAY)
GROUP BY DATE(StartedAt)
ORDER BY Data DESC;

-- 2. Top 10 perguntas não respondidas
SELECT 
    Question,
    Frequency,
    DetectedIntention,
    SuggestedCategory,
    Status
FROM ChatUnansweredQuestions
WHERE Status = 'pending'
ORDER BY Frequency DESC, CreatedAt DESC
LIMIT 10;

-- 3. Top 10 intenções mais detectadas
SELECT 
    IntentionName,
    Category,
    COUNT(*) AS Total,
    AVG(Confidence) AS ConfidenceMedia
FROM ChatIntentions
WHERE DetectedAt >= DATE_SUB(CURDATE(), INTERVAL 30 DAY)
GROUP BY IntentionName, Category
ORDER BY Total DESC
LIMIT 10;

-- 4. Taxa de resolução por período
SELECT 
    DATE(StartedAt) AS Data,
    COUNT(*) AS TotalConversas,
    SUM(CASE WHEN WasResolved = 1 THEN 1 ELSE 0 END) AS Resolvidas,
    ROUND((SUM(CASE WHEN WasResolved = 1 THEN 1 ELSE 0 END) * 100.0) / COUNT(*), 2) AS TaxaResolucao
FROM ChatConversations
WHERE StartedAt >= DATE_SUB(CURDATE(), INTERVAL 30 DAY)
GROUP BY DATE(StartedAt)
ORDER BY Data DESC;

-- 5. FAQs mais usados
SELECT 
    f.Category,
    f.Question,
    f.UsageCount,
    COUNT(m.Id) AS TotalUsos
FROM ChatFaqItems f
LEFT JOIN ChatMessages m ON m.FaqItemId = f.Id
WHERE f.IsActive = 1
GROUP BY f.Id, f.Category, f.Question, f.UsageCount
ORDER BY f.UsageCount DESC
LIMIT 10;

-- 6. Conversas sem resposta
SELECT 
    c.Id,
    c.SessionId,
    c.StartedAt,
    GROUP_CONCAT(m.Content SEPARATOR ' | ') AS Mensagens
FROM ChatConversations c
INNER JOIN ChatMessages m ON m.ConversationId = c.Id
WHERE NOT EXISTS (
    SELECT 1 FROM ChatMessages m2 
    WHERE m2.ConversationId = c.Id 
    AND m2.WasAnswered = 1
)
AND c.StartedAt >= DATE_SUB(CURDATE(), INTERVAL 7 DAY)
GROUP BY c.Id, c.SessionId, c.StartedAt
ORDER BY c.StartedAt DESC;

-- 7. Média de mensagens por conversa
SELECT 
    AVG(MessageCount) AS MediaMensagensPorConversa
FROM (
    SELECT ConversationId, COUNT(*) AS MessageCount
    FROM ChatMessages
    GROUP BY ConversationId
) AS SubQuery;

-- 8. Distribuição de intenções por categoria
SELECT 
    Category,
    COUNT(*) AS Total,
    ROUND((COUNT(*) * 100.0) / (SELECT COUNT(*) FROM ChatIntentions), 2) AS Percentual
FROM ChatIntentions
WHERE DetectedAt >= DATE_SUB(CURDATE(), INTERVAL 30 DAY)
GROUP BY Category
ORDER BY Total DESC;

-- =====================================================
-- LIMPEZA (CUIDADO!)
-- =====================================================

-- Deletar conversas antigas (mais de 6 meses)
-- DELETE FROM ChatConversations WHERE StartedAt < DATE_SUB(CURDATE(), INTERVAL 6 MONTH);

-- Deletar perguntas não respondidas resolvidas (mais de 3 meses)
-- DELETE FROM ChatUnansweredQuestions 
-- WHERE Status IN ('resolved', 'added_to_faq') 
-- AND ReviewedAt < DATE_SUB(CURDATE(), INTERVAL 3 MONTH);
