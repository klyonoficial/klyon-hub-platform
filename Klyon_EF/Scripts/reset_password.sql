-- Script para resetar senha de usuário
-- Use BCrypt para gerar o hash da nova senha
-- Exemplo: senha "Temp123!" gera hash: $2a$11$...

-- Para resetar a senha do usuário deucleriocs@gmail.com para "Temp123!"
-- Execute este comando no MySQL/MariaDB:

UPDATE Users 
SET PasswordHash = '$2a$11$LaiWMGaJqfa3DoVhUoqweuQjvQcRbDOxCbgZ3Kr053t/I8jyE72zW'
WHERE Email = 'deucleriocs@gmail.com';

-- OU use uma nova senha (exemplo: "Senha123!")
-- Gere o hash usando BCrypt e substitua acima
