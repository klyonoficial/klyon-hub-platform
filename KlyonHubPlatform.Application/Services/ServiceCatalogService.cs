using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using static KlyonHubPlatform.Domain.Core.Constants.ServiceCodes;

namespace KlyonHubPlatform.Application.Services
{
    public class ServiceCatalogService : IServiceCatalogService
    {
        private readonly ApplicationDbContext _context;
        private readonly IClientMenuService _clientMenuService;
        private readonly ISystemConfigurationService _systemConfig;

        public ServiceCatalogService(
            ApplicationDbContext context,
            IClientMenuService clientMenuService,
            ISystemConfigurationService systemConfig)
        {
            _context = context;
            _clientMenuService = clientMenuService;
            _systemConfig = systemConfig;
        }

        /// <inheritdoc />
        public async Task<List<Service>> GetActiveServicesForClientAsync(int userId, string marketplaceRoute, string chooseLocationRoute)
        {
            if (await _clientMenuService.ClientHasActiveContractAsync(userId))
                return new List<Service>();

            var services = await _context.Services
                .Where(s => s.IsActive)
                .OrderBy(s => s.DisplayOrder)
                .ToListAsync();

            var uniqueServices = services
                .GroupBy(s => s.Name)
                .Select(g => g.First())
                .OrderBy(s => s.DisplayOrder)
                .ToList();

            if (services.Count != uniqueServices.Count)
            {
                var duplicates = services.Except(uniqueServices).ToList();
                var now = DateTime.UtcNow;
                foreach (var duplicate in duplicates)
                {
                    duplicate.IsActive = false;
                    duplicate.UpdatedAt = now;
                }
                await _context.SaveChangesAsync();
                services = uniqueServices;
            }
            else
            {
                services = uniqueServices;
            }

            var needsUpdate = ApplyClientFriendlyFixes(services, marketplaceRoute);

            if (needsUpdate)
            {
                await _context.SaveChangesAsync();
                services = await _context.Services
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.DisplayOrder)
                    .ToListAsync();
                services = services
                    .GroupBy(s => s.Name)
                    .Select(g => g.First())
                    .OrderBy(s => s.DisplayOrder)
                    .ToList();
            }

            if (!services.Any())
            {
                await EnsureDefaultClientServicesAsync(chooseLocationRoute, marketplaceRoute);
                services = await _context.Services
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.DisplayOrder)
                    .ToListAsync();
            }

            var enableCoworkingMenus = await _systemConfig.GetValueAsync(FeaturesConfigKey.EnableCoworkingMenus.ToConfigKey());
            var showCoworkingMenus = StringBooleanValue.IsTrue(enableCoworkingMenus);

            if (!showCoworkingMenus)
                return services.Where(s => s.Code == FiscalAddressContracting).ToList();
            return services.Where(s => s.Code != FiscalAddressOffer).ToList();
        }

        private static bool ApplyClientFriendlyFixes(List<Service> services, string marketplaceRoute)
        {
            var roomServiceKeywords = new[] { "Sala", "Auditório", "Aluguel" };
            var needsUpdate = false;
            var partnerDescriptions = new[] { "Ofereça", "amplie", "receita", "rede", "Oferecer", "ampliar", "ganhe" };
            var clientFiscalDescription = "Contrate um endereço fiscal para sua empresa de forma rápida e segura";
            var now = DateTime.UtcNow;

            foreach (var service in services)
            {
                if (service.Name.Contains("Endereço Fiscal", StringComparison.OrdinalIgnoreCase) ||
                    service.Name.Contains("Endereco Fiscal", StringComparison.OrdinalIgnoreCase))
                {
                    if (partnerDescriptions.Any(desc => service.Description.Contains(desc, StringComparison.OrdinalIgnoreCase)) ||
                        string.IsNullOrWhiteSpace(service.Description))
                    {
                        service.Description = clientFiscalDescription;
                        service.UpdatedAt = now;
                        needsUpdate = true;
                    }
                }

                if (roomServiceKeywords.Any(k => service.Name.Contains(k, StringComparison.OrdinalIgnoreCase)) &&
                    service.Route != marketplaceRoute &&
                    !service.Route.Contains("/Home/Marketplace"))
                {
                    service.Route = marketplaceRoute;
                    service.UpdatedAt = now;
                    needsUpdate = true;
                }
                else if (service.Route == "/marketplace" ||
                    (service.Route.Contains("/marketplace") && !service.Route.Contains("/Home/Marketplace")))
                {
                    service.Route = marketplaceRoute;
                    service.UpdatedAt = now;
                    needsUpdate = true;
                }
            }

            return needsUpdate;
        }

        private async Task EnsureDefaultClientServicesAsync(string chooseLocationRoute, string marketplaceRoute)
        {
            var now = DateTime.UtcNow;
            var defaultServices = new[]
            {
                new Service
                {
                    Name = "Contratar Endereço Fiscal",
                    Code = FiscalAddressContracting,
                    Description = "Contrate um endereço fiscal para sua empresa de forma rápida e segura",
                    Route = chooseLocationRoute,
                    DisplayOrder = 1,
                    IsActive = true,
                    ImageUrl = "/images/services/endereco-fiscal.svg",
                    CreatedAt = now,
                    Category = CategoryService.Client
                },
                new Service
                {
                    Name = "Reservar Sala",
                    Code = RoomReservation,
                    Description = "Reserve salas de coworking para reuniões e trabalho",
                    Route = marketplaceRoute,
                    DisplayOrder = 2,
                    IsActive = true,
                    ImageUrl = "/images/services/sala-compartilhada.svg",
                    CreatedAt = now,
                    Category = CategoryService.ClientAndPartner
                }
            };

            foreach (var service in defaultServices)
                _context.Services.Add(service);
            await _context.SaveChangesAsync();
        }
    }
}
