// Script temporário para gerar hash BCrypt válido
// Execute: dotnet run --project . -- fix-password

using BCrypt.Net;

string password = "password";
string hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
Console.WriteLine($"Senha: {password}");
Console.WriteLine($"Hash: {hash}");

// Verificar se funciona
bool isValid = BCrypt.Net.BCrypt.Verify(password, hash);
Console.WriteLine($"Validação: {(isValid ? "OK" : "ERRO")}");
