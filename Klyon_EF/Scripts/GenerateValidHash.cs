// Script para gerar hash BCrypt válido
using BCrypt.Net;

string password = "password";
string hash = BCrypt.Net.BCrypt.HashPassword(password);
Console.WriteLine($"Hash gerado: {hash}");

// Verificar
bool valid = BCrypt.Net.BCrypt.Verify(password, hash);
Console.WriteLine($"Validação: {(valid ? "OK" : "ERRO")}");
