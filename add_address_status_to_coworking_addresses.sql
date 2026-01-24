-- Script para adicionar AddressStatus à tabela CoworkingAddresses
-- Status do endereço (0=Active, 1=Suspended, 2=Removed). Separado do status do parceiro.
-- Parceiro suspenso → não cria novos contratos. Endereço suspenso/removido →
-- contratos ativos continuam válidos; apenas novas vendas são bloqueadas.

SET @dbname = DATABASE();
SET @tablename = 'CoworkingAddresses';
SET @columnname = 'AddressStatus';

SET @preparedStatement = (SELECT IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
     WHERE TABLE_SCHEMA = @dbname
     AND TABLE_NAME = @tablename
     AND COLUMN_NAME = @columnname) = 0,
    CONCAT('ALTER TABLE `', @tablename, '` ADD COLUMN `', @columnname, '` INT NOT NULL DEFAULT 0 COMMENT ''0=Active, 1=Suspended, 2=Removed'''),
    'SELECT 1'
));
PREPARE stmt FROM @preparedStatement;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
