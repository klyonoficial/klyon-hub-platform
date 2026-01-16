-- Script de inicialização do banco de dados
-- Este script é executado automaticamente quando o container é criado pela primeira vez

-- Garantir que o banco existe
CREATE DATABASE IF NOT EXISTS klyon_hub_platform CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- Usar o banco
USE klyon_hub_platform;

-- Garantir que o usuário tem todas as permissões necessárias
GRANT ALL PRIVILEGES ON klyon_hub_platform.* TO 'klyon_user'@'%';
FLUSH PRIVILEGES;

-- O Entity Framework Core criará as tabelas automaticamente
-- Este script pode ser usado para dados iniciais ou configurações específicas
