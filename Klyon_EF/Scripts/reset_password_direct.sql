-- Script para resetar senha diretamente no banco
-- Senha temporária: Temp123!
-- Hash BCrypt gerado: $2a$11$LaiWMGaJqfa3DoVhUoqweuQjvQcRbDOxCbgZ3Kr053t/I8jyE72zW

UPDATE Users 
SET PasswordHash = '$2a$11$LaiWMGaJqfa3DoVhUoqweuQjvQcRbDOxCbgZ3Kr053t/I8jyE72zW'
WHERE Email = 'deucleriocs@gmail.com';

-- Verificar se foi atualizado
SELECT Id, Email, FullName, IsActive, EmailVerified 
FROM Users 
WHERE Email = 'deucleriocs@gmail.com';
