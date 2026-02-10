using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Application.Handlers;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.Services;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Infra.Data.Repositories;
using KlyonHubPlatform.Infra.CrossCutting.Middleware;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Em produção atrás de proxy (Nginx): usar X-Forwarded-Proto e X-Forwarded-Host para links corretos
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configurar Kestrel para desabilitar HTTP/2 em desenvolvimento (corrige ERR_HTTP2_PROTOCOL_ERROR)
if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<KestrelServerOptions>(options =>
    {
        options.ConfigureEndpointDefaults(listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1;
        });
    });
}

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// Antiforgery: aceitar token no header para requisições AJAX (JSON)
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

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
    options.UseMySql(connectionString, ServerVersion.Parse("10.5.29-mariadb"), 
        mySqlOptions => 
        {
            mySqlOptions.EnableRetryOnFailure();
            // Configurar para não converter timezone - salvar valores DateTime exatamente como estão
            mySqlOptions.EnableStringComparisonTranslations();
        }));

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
builder.Services.AddScoped<IPagBankWebhookLogRepository, PagBankWebhookLogRepository>();

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
builder.Services.AddScoped<IFocusNFeService, FocusNFeService>();
builder.Services.AddScoped<IMonthlySettlementService, MonthlySettlementService>();
builder.Services.AddScoped<ILocalizationService, LocalizationService>();
builder.Services.AddScoped<ISystemConfigurationService, SystemConfigurationService>();
builder.Services.AddScoped<IAppBaseUrlProvider, AppBaseUrlProvider>();
builder.Services.AddScoped<IReservationTransferService, ReservationTransferService>();

// Chatbot Services
builder.Services.AddScoped<IChatbotRepository, ChatbotRepository>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddScoped<IAIIntentionService, AIIntentionService>();
builder.Services.AddScoped<IDatabaseMigrationService, DatabaseMigrationService>();

// Asaas Service (cobrança e nota fiscal - provedor fixo)
builder.Services.AddHttpClient<IAsaasService, AsaasService>();
builder.Services.AddHttpClient<IAsaasInvoiceService, AsaasInvoiceService>();

// PagBank (legado - apenas webhook; fluxo principal usa Asaas)
builder.Services.AddHttpClient<PagBankService>();
builder.Services.AddScoped<IPagBankService, PagBankService>();

// D4Sign - Assinatura eletrônica de contratos (endereço fiscal)
builder.Services.AddHttpClient<ID4SignService, D4SignService>();

// Clicksign - Assinatura eletrônica (v3 Envelope) com tratamento de rate limit (429)
builder.Services.AddTransient<ClicksignRateLimitHandler>();
builder.Services.AddHttpClient<IClicksignService, ClicksignService>()
    .AddHttpMessageHandler<ClicksignRateLimitHandler>();

// Address Declaration Service
builder.Services.AddScoped<IAddressDeclarationService, AddressDeclarationService>();

// Client Menu (ex.: ocultar "Localidades Disponíveis" quando cliente tem endereço ativo)
builder.Services.AddScoped<IClientMenuService, ClientMenuService>();

// Storage Services
builder.Services.AddScoped<AWSStorageService>();
builder.Services.AddScoped<FTPStorageService>();
builder.Services.AddScoped<StorageServiceFactory>();

// Contract Expiry Notification Service
builder.Services.AddScoped<ContractExpiryNotificationService>();

// Background Services
builder.Services.AddHostedService<ContractExpiryBackgroundService>();
builder.Services.AddHostedService<AddressDeclarationBackgroundService>();
builder.Services.AddHostedService<MonthlySettlementBackgroundService>();

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// HttpContextAccessor para acesso ao contexto HTTP nos serviços
builder.Services.AddHttpContextAccessor();

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// ForwardedHeaders deve vir primeiro para Request.Scheme/Host refletirem o domínio público (Nginx)
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Cache Control Middleware - configura headers de cache do navegador
app.UseMiddleware<CacheControlMiddleware>();

// Localization - DEVE vir antes de UseRouting mas depois de UseStaticFiles
var localizationOptions = app.Services.GetService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(localizationOptions!.Value);

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();
app.UseMiddleware<AuditMiddleware>();

// Rota de alias para Parceiro -> Coworking (precisa vir antes da default para /Parceiro/* não ser interpretado como controller=Parceiro)
app.MapControllerRoute(
    name: "parceiro",
    pattern: "Parceiro/{action=Dashboard}/{id?}",
    defaults: new { controller = "Coworking" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    try
    {
        // Usar sempre Migrate quando o banco conecta. NÃO usar EnsureCreated aqui:
        // EnsureCreated cria as tabelas mas NÃO grava em __EFMigrationsHistory; depois
        // MigrateAsync tenta aplicar a primeira migration e falha com "tabela já existe".
        if (dbContext.Database.CanConnect())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            // Se o banco foi criado com EnsureCreated ou está desatualizado, algumas migrations
            // podem falhar com "table already exists". Marcamos como aplicada e tentamos a próxima.
            while (true)
            {
                var pending = await dbContext.Database.GetPendingMigrationsAsync();
                if (pending.Count() == 0)
                    break;
                var firstPending = pending.First();
                try
                {
                    await dbContext.Database.MigrateAsync();
                    break;
                }
                catch (Exception ex) when (ex.InnerException?.Message?.Contains("already exists", StringComparison.OrdinalIgnoreCase) == true
                    || ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning(ex, "MigrateAsync: tabela já existe. Marcando migration {Migration} como aplicada e tentando novamente.", firstPending);
                    await dbContext.Database.ExecuteSqlRawAsync(
                        "INSERT IGNORE INTO `__EFMigrationsHistory` (MigrationId, ProductVersion) VALUES ({0}, {1})",
                        firstPending,
                        "8.0.0");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "MigrateAsync: {Message}", ex.Message);
                    break;
                }
            }
        }
        else
        {
            // Só cria o banco/schema quando não consegue conectar (ex.: banco não existe).
            // Atenção: isso não popula __EFMigrationsHistory; use Migrate após o banco existir.
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
