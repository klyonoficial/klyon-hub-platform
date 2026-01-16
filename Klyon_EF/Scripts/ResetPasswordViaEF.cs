// Script para resetar senha usando Entity Framework
// Execute: dotnet run --project . -- reset-password-ef

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using BCrypt.Net;
using KlyonEF.Data;
using KlyonEF.Models;

// Este script deve ser executado como parte da aplicação
// Por enquanto, vamos criar um endpoint temporário no AdminController
