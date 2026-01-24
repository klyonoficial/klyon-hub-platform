-- =============================================================================
-- Script SQL para criar a tabela FocusNFeWebhookLogs
-- Integração com Focus NFe para emissão de notas fiscais
-- Execute este script via Admin > Executar Migrations
-- =============================================================================

-- Criar tabela de logs de webhooks do Focus NFe
CREATE TABLE IF NOT EXISTS FocusNFeWebhookLogs (
    Id INT NOT NULL AUTO_INCREMENT,
    Ref VARCHAR(100) NOT NULL COMMENT 'Referência única da nota no sistema',
    CnpjEmitente VARCHAR(20) NOT NULL COMMENT 'CNPJ do emitente da nota',
    DocumentType VARCHAR(20) NOT NULL DEFAULT 'NFSe' COMMENT 'Tipo do documento: NFSe, NFe, NFCe',
    Status VARCHAR(50) NOT NULL COMMENT 'Status recebido: autorizado, cancelado, erro_autorizacao',
    Evento VARCHAR(50) NULL COMMENT 'Evento do webhook',
    Numero VARCHAR(50) NULL COMMENT 'Número da nota fiscal',
    CodigoVerificacao VARCHAR(50) NULL COMMENT 'Código de verificação da nota',
    Url VARCHAR(500) NULL COMMENT 'URL para visualização da nota',
    CaminhoXml VARCHAR(500) NULL COMMENT 'Caminho do XML da nota',
    FiscalNoteId INT NULL COMMENT 'ID da nota fiscal no sistema (FK)',
    ProcessingStatus VARCHAR(50) NOT NULL DEFAULT 'Received' COMMENT 'Status do processamento: Received, Processing, Processed, Error, Ignored',
    ErrorMessage VARCHAR(2000) NULL COMMENT 'Mensagem de erro se houver',
    RawPayload TEXT NULL COMMENT 'Payload JSON completo recebido',
    SourceIp VARCHAR(50) NULL COMMENT 'IP de origem da requisição',
    ReceivedAt DATETIME(6) NOT NULL COMMENT 'Data/hora de recebimento do webhook',
    ProcessedAt DATETIME(6) NULL COMMENT 'Data/hora de processamento',
    ProcessingAttempts INT NOT NULL DEFAULT 0 COMMENT 'Número de tentativas de processamento',
    PRIMARY KEY (Id),
    CONSTRAINT FK_FocusNFeWebhookLogs_FiscalNotes FOREIGN KEY (FiscalNoteId)
        REFERENCES FiscalNotes (Id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Criar índices para otimização de consultas
CREATE INDEX IX_FocusNFeWebhookLogs_Ref ON FocusNFeWebhookLogs (Ref);
CREATE INDEX IX_FocusNFeWebhookLogs_CnpjEmitente ON FocusNFeWebhookLogs (CnpjEmitente);
CREATE INDEX IX_FocusNFeWebhookLogs_ReceivedAt ON FocusNFeWebhookLogs (ReceivedAt);
CREATE INDEX IX_FocusNFeWebhookLogs_ProcessingStatus ON FocusNFeWebhookLogs (ProcessingStatus);
CREATE INDEX IX_FocusNFeWebhookLogs_FiscalNoteId ON FocusNFeWebhookLogs (FiscalNoteId);

-- Índice composto para verificação de duplicidade
CREATE INDEX IX_FocusNFeWebhookLogs_Ref_Status_Evento ON FocusNFeWebhookLogs (Ref, Status, Evento);

-- =============================================================================
-- Verificação de execução
-- =============================================================================
SELECT 
    'FocusNFeWebhookLogs' AS Tabela,
    COUNT(*) AS TotalRegistros,
    'Tabela criada com sucesso!' AS Status
FROM FocusNFeWebhookLogs;
