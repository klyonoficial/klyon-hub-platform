using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
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
using KlyonHubPlatform.Api.Constants;
using KlyonHubPlatform.Domain.Core.Constants;
using Microsoft.Extensions.Options;
using KlyonHubPlatform.Application.Options;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
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
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources/Shared");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo(CultureNames.PtBr),
        new CultureInfo(CultureNames.EnUs),
        new CultureInfo(CultureNames.EsEs)
    };
    options.DefaultRequestCulture = new RequestCulture(CultureNames.Default);
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders.Clear();
});
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.Parse("10.5.29-mariadb"), 
        mySqlOptions => 
        {
            mySqlOptions.EnableRetryOnFailure();
            mySqlOptions.EnableStringComparisonTranslations();
            mySqlOptions.CommandTimeout(120); // evita "Command Timeout expired" em consultas pesadas (ex.: DashboardDetails)
        }));
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JwtSettings:SecretKey is required");
var dataProtectionKeyPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrEmpty(dataProtectionKeyPath))
{
    var keyDir = new DirectoryInfo(dataProtectionKeyPath);
    if (keyDir.Exists)
    {
        builder.Services.AddDataProtection()
            .SetApplicationName("KlyonHubPlatform")
            .PersistKeysToFileSystem(keyDir);
    }
}
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(AuthConstants.CookieScheme, options =>
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
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICoworkingRepository, CoworkingRepository>();
builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<ICorrespondenceRepository, CorrespondenceRepository>();
builder.Services.AddScoped<IOperatingHoursRepository, OperatingHoursRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<ICommissionRepository, CommissionRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IPagBankWebhookLogRepository, PagBankWebhookLogRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICoworkingService, CoworkingService>();
builder.Services.AddScoped<ICoworkingAppService, CoworkingAppService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<ICorrespondenceService, CorrespondenceService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<ICommissionService, CommissionService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IQuickClientRegistrationService, QuickClientRegistrationService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
            builder.Services.AddScoped<IAdminApprovalService, AdminApprovalService>();
            builder.Services.AddScoped<IAdminCoworkingService, AdminCoworkingService>();
            builder.Services.AddScoped<IAdminDataService, AdminDataService>();
            builder.Services.AddScoped<IAdminDbService, AdminDbService>();
            builder.Services.AddScoped<ISefazService, SefazService>();
builder.Services.AddScoped<IFocusNFeService, FocusNFeService>();
builder.Services.AddScoped<IMonthlySettlementService, MonthlySettlementService>();
builder.Services.AddScoped<ILocalizationService, LocalizationService>();
builder.Services.AddScoped<ISystemConfigurationService, SystemConfigurationService>();
builder.Services.AddScoped<IAppBaseUrlProvider, AppBaseUrlProvider>();
builder.Services.AddScoped<IReservationTransferService, ReservationTransferService>();
builder.Services.AddScoped<IChatbotRepository, ChatbotRepository>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddScoped<IAIIntentionService, AIIntentionService>();
builder.Services.Configure<FiscalAddressChatOptions>(builder.Configuration.GetSection(FiscalAddressChatOptions.SectionName));
builder.Services.AddScoped<IFiscalAddressChatService, FiscalAddressChatService>();
builder.Services.AddScoped<IDatabaseMigrationService, DatabaseMigrationService>();
builder.Services.AddHttpClient<IAsaasService, AsaasService>();
builder.Services.AddHttpClient<IAsaasInvoiceService, AsaasInvoiceService>();
builder.Services.AddHttpClient<PagBankService>();
builder.Services.AddScoped<IPagBankService, PagBankService>();
builder.Services.AddHttpClient<ID4SignService, D4SignService>();
builder.Services.AddTransient<ClicksignRateLimitHandler>();
builder.Services.AddHttpClient<IClicksignService, ClicksignService>()
    .AddHttpMessageHandler<ClicksignRateLimitHandler>();
builder.Services.AddScoped<IAddressDeclarationService, AddressDeclarationService>();
builder.Services.AddScoped<IClientMenuService, ClientMenuService>();
builder.Services.AddScoped<IClientDashboardService, ClientDashboardService>();
builder.Services.AddScoped<IClientAppService, ClientAppService>();
builder.Services.AddScoped<IAppHomeService, AppHomeService>();
builder.Services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
builder.Services.AddScoped<AWSStorageService>();
builder.Services.AddScoped<FTPStorageService>();
builder.Services.AddScoped<OracleStorageService>();
builder.Services.AddScoped<StorageServiceFactory>();
builder.Services.AddScoped<ContractExpiryNotificationService>();
builder.Services.AddHostedService<ContractExpiryBackgroundService>();
builder.Services.AddHostedService<AddressDeclarationBackgroundService>();
builder.Services.AddHostedService<MonthlySettlementBackgroundService>();
builder.Services.AddAutoMapper(typeof(Program), typeof(KlyonHubPlatform.Application.Mappers.ApplicationMappingProfile));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
var app = builder.Build();
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseMiddleware<CacheControlMiddleware>();
var localizationOptions = app.Services.GetService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(localizationOptions!.Value);
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.UseMiddleware<AuditMiddleware>();
app.MapControllerRoute(
    name: "parceiro",
    pattern: "Parceiro/{action=Dashboard}/{id?}",
    defaults: new { controller = "Coworking" });
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<KlyonHubPlatform.Api.Hubs.PaymentHub>("/hubs/payment");
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        if (dbContext.Database.CanConnect())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
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
            await dbContext.Database.EnsureCreatedAsync();
        }
        var seedService = new SeedService(dbContext);
        await seedService.SeedInitialDataAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Erro ao inicializar banco de dados. Tentando criar tabelas...");
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