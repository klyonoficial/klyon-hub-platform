-- Script para adicionar campos de solicitação de remoção à tabela PartnerServices
-- Execute este script no seu banco de dados MySQL
-- Este script verifica se cada coluna já existe antes de tentar adicioná-la

SET @dbname = DATABASE();

-- Adicionar RemovalRequestStatus se não existir
SET @tablename = 'PartnerServices';
SET @columnname = 'RemovalRequestStatus';
SET @preparedStatement = (SELECT IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
     WHERE TABLE_SCHEMA = @dbname 
     AND TABLE_NAME = @tablename 
     AND COLUMN_NAME = @columnname) = 0,
    CONCAT('ALTER TABLE `', @tablename, '` ADD COLUMN `', @columnname, '` VARCHAR(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''None'''),
    'SELECT 1'
));
PREPARE stmt FROM @preparedStatement;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Adicionar RemovalRequestedAt se não existir
SET @columnname = 'RemovalRequestedAt';
SET @preparedStatement = (SELECT IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
     WHERE TABLE_SCHEMA = @dbname 
     AND TABLE_NAME = @tablename 
     AND COLUMN_NAME = @columnname) = 0,
    CONCAT('ALTER TABLE `', @tablename, '` ADD COLUMN `', @columnname, '` DATETIME(6) NULL'),
    'SELECT 1'
));
PREPARE stmt FROM @preparedStatement;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Adicionar RemovalReviewedAt se não existir
SET @columnname = 'RemovalReviewedAt';
SET @preparedStatement = (SELECT IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
     WHERE TABLE_SCHEMA = @dbname 
     AND TABLE_NAME = @tablename 
     AND COLUMN_NAME = @columnname) = 0,
    CONCAT('ALTER TABLE `', @tablename, '` ADD COLUMN `', @columnname, '` DATETIME(6) NULL'),
    'SELECT 1'
));
PREPARE stmt FROM @preparedStatement;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Adicionar RemovalReviewedBy se não existir
SET @columnname = 'RemovalReviewedBy';
SET @preparedStatement = (SELECT IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
     WHERE TABLE_SCHEMA = @dbname 
     AND TABLE_NAME = @tablename 
     AND COLUMN_NAME = @columnname) = 0,
    CONCAT('ALTER TABLE `', @tablename, '` ADD COLUMN `', @columnname, '` INT NULL'),
    'SELECT 1'
));
PREPARE stmt FROM @preparedStatement;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Adicionar RemovalRejectionReason se não existir
SET @columnname = 'RemovalRejectionReason';
SET @preparedStatement = (SELECT IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
     WHERE TABLE_SCHEMA = @dbname 
     AND TABLE_NAME = @tablename 
     AND COLUMN_NAME = @columnname) = 0,
    CONCAT('ALTER TABLE `', @tablename, '` ADD COLUMN `', @columnname, '` LONGTEXT CHARACTER SET utf8mb4 NULL'),
    'SELECT 1'
));
PREPARE stmt FROM @preparedStatement;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
