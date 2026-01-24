-- Script para adicionar tabela de logs de webhooks do PagBank
-- Execute este script via a tela de Admin > Migrations (MIGRATIONS_VIA_WEB.md)

CREATE TABLE IF NOT EXISTS `PagBankWebhookLogs` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `EventId` VARCHAR(100) CHARACTER SET utf8mb4 NOT NULL COMMENT 'ID único do evento enviado pelo PagBank (para idempotência)',
    `EventType` VARCHAR(50) CHARACTER SET utf8mb4 NOT NULL COMMENT 'Tipo do evento (CHARGE_PAID, CHARGE_CANCELED, etc.)',
    `ResourceId` VARCHAR(100) CHARACTER SET utf8mb4 NOT NULL COMMENT 'ID do recurso (charge ou order)',
    `ReferenceId` VARCHAR(100) CHARACTER SET utf8mb4 NULL COMMENT 'Reference ID do pedido (nosso ExternalPaymentId original)',
    `ResourceStatus` VARCHAR(50) CHARACTER SET utf8mb4 NULL COMMENT 'Status do recurso no momento da notificação',
    `PaymentId` INT NULL COMMENT 'ID do Payment associado (se encontrado)',
    `ProcessingStatus` VARCHAR(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Received' COMMENT 'Status do processamento do webhook',
    `ErrorMessage` VARCHAR(2000) CHARACTER SET utf8mb4 NULL COMMENT 'Mensagem de erro se o processamento falhou',
    `RawPayload` TEXT CHARACTER SET utf8mb4 NULL COMMENT 'Payload JSON completo recebido (para debug)',
    `ReceivedSignature` VARCHAR(255) CHARACTER SET utf8mb4 NULL COMMENT 'Assinatura recebida no header',
    `SignatureValid` TINYINT(1) NOT NULL DEFAULT 0 COMMENT 'Se a assinatura foi validada com sucesso',
    `SourceIp` VARCHAR(50) CHARACTER SET utf8mb4 NULL COMMENT 'IP de origem da requisição',
    `ReceivedAt` DATETIME(6) NOT NULL COMMENT 'Data/hora de recebimento',
    `ProcessedAt` DATETIME(6) NULL COMMENT 'Data/hora de processamento',
    `ProcessingAttempts` INT NOT NULL DEFAULT 0 COMMENT 'Número de tentativas de processamento',
    PRIMARY KEY (`Id`),
    UNIQUE KEY `IX_PagBankWebhookLogs_EventId` (`EventId`),
    INDEX `IX_PagBankWebhookLogs_ResourceId` (`ResourceId`),
    INDEX `IX_PagBankWebhookLogs_ReferenceId` (`ReferenceId`),
    INDEX `IX_PagBankWebhookLogs_ReceivedAt` (`ReceivedAt`),
    INDEX `IX_PagBankWebhookLogs_ProcessingStatus` (`ProcessingStatus`),
    CONSTRAINT `FK_PagBankWebhookLogs_Payments_PaymentId`
        FOREIGN KEY (`PaymentId`) REFERENCES `Payments` (`Id`) ON DELETE SET NULL
) CHARACTER SET utf8mb4 COMMENT 'Registro de webhooks recebidos do PagBank para garantir idempotência';
