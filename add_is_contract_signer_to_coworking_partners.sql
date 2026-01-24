-- Script para adicionar campo IsContractSigner à tabela CoworkingPartners
-- Execute este script no seu banco de dados MySQL
-- Este script verifica se a coluna já existe antes de tentar adicioná-la

SET @dbname = DATABASE();
SET @tablename = 'CoworkingPartners';
SET @columnname = 'IsContractSigner';

SET @preparedStatement = (SELECT IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
     WHERE TABLE_SCHEMA = @dbname 
     AND TABLE_NAME = @tablename 
     AND COLUMN_NAME = @columnname) = 0,
    CONCAT('ALTER TABLE `', @tablename, '` ADD COLUMN `', @columnname, '` TINYINT(1) NOT NULL DEFAULT 0'),
    'SELECT 1'
));
PREPARE stmt FROM @preparedStatement;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
