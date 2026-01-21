using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Api.ViewModels
{
    public class MarketplaceSearchVm
    {
        public string? State { get; set; }
        public string? City { get; set; }
        public string? Neighborhood { get; set; }
        public int? MinPeople { get; set; }
        public int? MaxPeople { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 12;
    }

    public class MarketplaceCardVm
    {
        public int CoworkingAddressId { get; set; }
        public string Title { get; set; } = string.Empty; // Ex: "Endereço em Manaus"
        public string Subtitle { get; set; } = string.Empty; // Ex: "Aleixo"
        public string CityState { get; set; } = string.Empty; // Ex: "Manaus/AM"
        public decimal BasePrice { get; set; }
        public bool IsAvailable { get; set; }
        public int CurrentContracts { get; set; }
        public int MaxContracts { get; set; }
        public int MaxPeople { get; set; } // Capacidade máxima de pessoas
        public int? MainPhotoId { get; set; }
        
        // Serviços disponíveis
        public bool OffersSharedRoom { get; set; }
        public int? SharedRoomCreditsPerHour { get; set; } // Créditos por hora para sala compartilhada
        public bool OffersPrivateRoom { get; set; }
        public int? PrivateRoomCreditsPerHour { get; set; } // Créditos por hora para sala privativa
    }

    public class MarketplaceListVm
    {
        public MarketplaceSearchVm Search { get; set; } = new();

        public List<string> AvailableStates { get; set; } = new();
        public List<string> AvailableCities { get; set; } = new();
        public List<MarketplaceCardVm> Items { get; set; } = new();

        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }

    public class MarketplaceDetailsVm
    {
        public CoworkingAddress Address { get; set; } = null!;
        public Coworking Coworking { get; set; } = null!;
        public int? MainPhotoId { get; set; }
        public List<int> PhotoIds { get; set; } = new();
        public string? ReturnUrl { get; set; }
    }

    public class InterestFormVm
    {
        public int CoworkingAddressId { get; set; }
        public DateTime? AllocationDate { get; set; }
        public string? StartTime { get; set; } // Formato HH:mm
        public string? EndTime { get; set; } // Formato HH:mm
        public string Name { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }
}

