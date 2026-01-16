// Script para gerar hash BCrypt de uma senha
// Execute: dotnet script GeneratePasswordHash.cs

using BCrypt.Net;

string password = "Temp123!";
string hash = BCrypt.Net.BCrypt.HashPassword(password);
Console.WriteLine($"Senha: {password}");
Console.WriteLine($"Hash: {hash}");

// Verificar
bool isValid = BCrypt.Net.BCrypt.Verify(password, hash);
Console.WriteLine($"Validação: {(isValid ? "OK" : "ERRO")}");
