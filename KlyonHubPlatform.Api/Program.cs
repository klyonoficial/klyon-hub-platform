using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.Services;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Infra.Data.Repositories;
using KlyonHubPlatform.Infra.CrossCutting.Middleware;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources/Shared");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("pt-BR"),
        new CultureInfo("en-US"),
        new CultureInfo("es-ES")
    };

    options.DefaultRequestCulture = new RequestCulture("pt-BR");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    
    // Limpar providers e usar apenas o padrão pt-BR
    options.RequestCultureProviders.Clear();
});

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.Parse("10.5.29-mariadb")));

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/Login";
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

builder.Services.AddAuthorization();

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICoworkingRepository, CoworkingRepository>();
builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<ICommissionRepository, CommissionRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// Services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICoworkingService, CoworkingService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<ICommissionService, CommissionService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ISefazService, SefazService>();
builder.Services.AddScoped<ILocalizationService, LocalizationService>();
builder.Services.AddScoped<ISystemConfigurationService, SystemConfigurationService>();
builder.Services.AddScoped<IReservationTransferService, ReservationTransferService>();

// PagBank Service
builder.Services.AddHttpClient<PagBankService>();
builder.Services.AddScoped<IPagBankService, PagBankService>();

// Address Declaration Service
builder.Services.AddScoped<IAddressDeclarationService, AddressDeclarationService>();

// Storage Services
builder.Services.AddScoped<AWSStorageService>();
builder.Services.AddScoped<FTPStorageService>();
builder.Services.AddScoped<StorageServiceFactory>();

// Contract Expiry Notification Service
builder.Services.AddScoped<ContractExpiryNotificationService>();

// Background Services
builder.Services.AddHostedService<ContractExpiryBackgroundService>();
builder.Services.AddHostedService<AddressDeclarationBackgroundService>();

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Localization - DEVE vir antes de UseRouting mas depois de UseStaticFiles
var localizationOptions = app.Services.GetService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(localizationOptions!.Value);

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();
app.UseMiddleware<AuditMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Rota de alias para Parceiro -> Coworking
app.MapControllerRoute(
    name: "parceiro",
    pattern: "Parceiro/{action=Dashboard}/{id?}",
    defaults: new { controller = "Coworking" });

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    try
    {
        // Verificar se o banco existe e pode conectar
        if (dbContext.Database.CanConnect())
        {
            // Verificar se a tabela Roles existe (indicador de que as tabelas foram criadas)
            var rolesTableExists = false;
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync("SELECT 1 FROM Roles LIMIT 1");
                rolesTableExists = true;
            }
            catch
            {
                // Tabela não existe
                rolesTableExists = false;
            }
            
            if (!rolesTableExists)
            {
                // Se o banco existe mas não tem tabelas, criar todas as tabelas
                await dbContext.Database.EnsureCreatedAsync();
            }
            else
            {
                // Se as tabelas existem, aplicar migrações pendentes
                try
                {
                    await dbContext.Database.MigrateAsync();
                }
                catch
                {
                    // Se não houver migrações, apenas continuar
                }
            }
        }
        else
        {
            // Se não conseguir conectar, criar o banco e as tabelas
            await dbContext.Database.EnsureCreatedAsync();
        }
        
        // Seed initial data
        var seedService = new SeedService(dbContext);
        await seedService.SeedInitialDataAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Erro ao inicializar banco de dados. Tentando criar tabelas...");
        
        // Tentar criar as tabelas mesmo em caso de erro
        try
        {
            await dbContext.Database.EnsureCreatedAsync();
            var seedService = new SeedService(dbContext);
            await seedService.SeedInitialDataAsync();
        }
        catch (Exception ex2)
        {
            logger.LogError(ex2, "Erro crítico ao criar tabelas do banco de dados");
            throw;
        }
    }
}

app.Run();
