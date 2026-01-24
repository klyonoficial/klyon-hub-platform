-- Script para adicionar campos de Venda Assistida à tabela Contracts
-- Execute no banco MySQL. Verifica se as colunas já existem antes de adicionar.

SET @dbname = DATABASE();
SET @tablename = 'Contracts';

-- IsAssistedSale
SET @col = 'IsAssistedSale';
SET @prep = (SELECT IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
     WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND COLUMN_NAME = @col) = 0,
    CONCAT('ALTER TABLE `', @tablename, '` ADD COLUMN `', @col, '` TINYINT(1) NOT NULL DEFAULT 0'),
    'SELECT 1'
));
PREPARE stmt FROM @prep;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- AssistedSaleAdminId
SET @col = 'AssistedSaleAdminId';
SET @prep = (SELECT IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
     WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND COLUMN_NAME = @col) = 0,
    CONCAT('ALTER TABLE `', @tablename, '` ADD COLUMN `', @col, '` INT NULL'),
    'SELECT 1'
));
PREPARE stmt FROM @prep;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Índice e FK (apenas se não existir)
SET @idx = 'IX_Contracts_AssistedSaleAdminId';
SET @prep = (SELECT IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
     WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND INDEX_NAME = @idx) = 0,
    CONCAT('ALTER TABLE `', @tablename, '` ADD INDEX `', @idx, '` (`AssistedSaleAdminId`)'),
    'SELECT 1'
));
PREPARE stmt FROM @prep;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @fk = 'FK_Contracts_Users_AssistedSaleAdminId';
SET @prep = (SELECT IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
     WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND CONSTRAINT_NAME = @fk) = 0,
    'ALTER TABLE `Contracts` ADD CONSTRAINT `FK_Contracts_Users_AssistedSaleAdminId` FOREIGN KEY (`AssistedSaleAdminId`) REFERENCES `Users` (`Id`) ON DELETE SET NULL',
    'SELECT 1'
));
PREPARE stmt FROM @prep;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
