-- Script para adicionar Status e RejectionReason à tabela CoworkingDocuments
-- Execute este script no seu banco de dados MySQL.
-- Apenas ALTER TABLE / ADD COLUMN (sem PREPARE, EXECUTE, UPDATE).
-- Documentos existentes recebem Status = 'Approved' via DEFAULT.

ALTER TABLE CoworkingDocuments ADD COLUMN Status VARCHAR(50) NOT NULL DEFAULT 'Approved';
ALTER TABLE CoworkingDocuments ADD COLUMN RejectionReason VARCHAR(2000) NULL;
