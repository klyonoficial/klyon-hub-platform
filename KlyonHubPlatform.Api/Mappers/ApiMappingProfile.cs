using AutoMapper;
using KlyonHubPlatform.Api.Dtos;
using KlyonHubPlatform.Api.Dtos.Coworking;
using KlyonHubPlatform.Api.Dtos.Dashboard;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Api.Mappers
{
    /// <summary>
    /// Profiles de mapeamento entidade -> DTO na API.
    /// </summary>
    public class ApiMappingProfile : Profile
    {
        public ApiMappingProfile()
        {
            CreateMap<CoworkingAddress, AvailableAddressDto>()
                .ForMember(d => d.CoworkingName, o => o.MapFrom(s => s.Coworking != null ? s.Coworking.Name : ""))
                .ForMember(d => d.City, o => o.MapFrom(s => s.Location != null ? s.Location.City : s.City))
                .ForMember(d => d.State, o => o.MapFrom(s => s.Location != null ? s.Location.State : s.State))
                .ForMember(d => d.Vacancies, o => o.MapFrom(s => s.MaxContracts - s.CurrentContracts))
                .ForMember(d => d.DisplayText, o => o.MapFrom(s =>
                    $"{(s.Coworking != null ? s.Coworking.Name : "")} - {s.Address}, {s.Number} ({(s.Location != null ? s.Location.City : s.City)}/{(s.Location != null ? s.Location.State : s.State)}) - R$ {s.BasePrice:F2}"));

            // Dashboard Admin
            CreateMap<User, UserDashboardItemDto>();
            CreateMap<Coworking, CoworkingDashboardItemDto>()
                .ForMember(d => d.ApprovalStatus, o => o.MapFrom(s => s.ApprovalStatus.ToString()))
                .ForMember(d => d.ApprovalStatusDisplay, o => o.MapFrom(s => s.ApprovalStatus.ToDisplayString()));
            CreateMap<Contract, ContractDashboardItemDto>()
                .ForMember(d => d.ClientName, o => o.MapFrom(s => s.Client != null ? (s.Client.RazaoSocial ?? s.Client.NomeFantasia ?? s.Client.ContactName ?? "N/A") : "N/A"))
                .ForMember(d => d.Location, o => o.MapFrom(s => s.CoworkingAddress != null && s.CoworkingAddress.Location != null
                    ? $"{s.CoworkingAddress.Location.City}/{s.CoworkingAddress.Location.State}" : "N/A"))
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.StatusDisplay, o => o.MapFrom(s => s.Status.ToDisplayString()));
            CreateMap<Location, LocationDashboardItemDto>();
            CreateMap<Payment, PaymentDashboardItemDto>()
                .ForMember(d => d.ClientName, o => o.MapFrom(s =>
                    s.Subscription != null && s.Subscription.Contract != null && s.Subscription.Contract.Client != null
                        ? (s.Subscription.Contract.Client.RazaoSocial ?? s.Subscription.Contract.Client.NomeFantasia ?? s.Subscription.Contract.Client.ContactName ?? "N/A")
                        : "N/A"))
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.StatusDisplay, o => o.MapFrom(s => s.Status.ToDisplayString()))
                .ForMember(d => d.CreatedAt, o => o.MapFrom(s => s.CreatedAt.ToString(DateFormatConstants.DisplayDateAndTime)));
            CreateMap<Reservation, ReservationDashboardItemDto>()
                .ForMember(d => d.ClientName, o => o.MapFrom(s => s.Client != null ? (s.Client.RazaoSocial ?? s.Client.NomeFantasia ?? s.Client.ContactName ?? "N/A") : "N/A"))
                .ForMember(d => d.CoworkingName, o => o.MapFrom(s => s.CoworkingAddress != null && s.CoworkingAddress.Coworking != null ? s.CoworkingAddress.Coworking.Name : "N/A"))
                .ForMember(d => d.ReservationDate, o => o.MapFrom(s => s.ReservationDate.ToString(DateFormatConstants.DisplayDateOnly)))
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.StatusDisplay, o => o.MapFrom(s =>
                    s.Status == ReservationStatus.Confirmed ? "Confirmada" :
                    s.Status == ReservationStatus.Pending ? "Pendente" :
                    s.Status == ReservationStatus.Cancelled ? "Cancelada" :
                    s.Status == ReservationStatus.Completed ? "Concluída" : s.Status.ToString()));
            CreateMap<CardPriceChangeRequest, CardPriceRequestDashboardItemDto>()
                .ForMember(d => d.CoworkingName, o => o.MapFrom(s => s.CoworkingAddress != null && s.CoworkingAddress.Coworking != null ? s.CoworkingAddress.Coworking.Name : ""))
                .ForMember(d => d.CoworkingId, o => o.MapFrom(s => s.CoworkingAddress != null ? s.CoworkingAddress.CoworkingId : 0))
                .ForMember(d => d.AddressLabel, o => o.MapFrom(s => s.CoworkingAddress != null
                    ? $"{s.CoworkingAddress.Address}, {s.CoworkingAddress.Number} — {s.CoworkingAddress.City}/{s.CoworkingAddress.State}" : ""))
                .ForMember(d => d.CreatedAt, o => o.MapFrom(s => s.CreatedAt.ToString(DateFormatConstants.DisplayDateAndTime)));

            // Coworking: Reservations e Interests para listagem unificada
            CreateMap<Reservation, ReservationOrInterestItemDto>()
                .ForMember(d => d.Type, o => o.MapFrom(_ => "Reservation"))
                .ForMember(d => d.ClientName, o => o.MapFrom(s => s.Client != null ? s.Client.User != null ? s.Client.User.FullName ?? "" : (s.Client.RazaoSocial ?? s.Client.NomeFantasia ?? s.Client.ContactName ?? "") : ""))
                .ForMember(d => d.ClientEmail, o => o.MapFrom(s => s.Client != null ? (s.Client.ContactEmail ?? (s.Client.User != null ? s.Client.User.Email : null) ?? "") : ""))
                .ForMember(d => d.ClientPhone, o => o.MapFrom(s => s.Client != null ? (s.Client.ContactPhone ?? (s.Client.User != null ? s.Client.User.Phone : null) ?? "") : ""))
                .ForMember(d => d.Address, o => o.MapFrom(s => s.CoworkingAddress))
                .ForMember(d => d.StartTime, o => o.MapFrom(s => (object?)s.StartTime ?? DBNull.Value))
                .ForMember(d => d.EndTime, o => o.MapFrom(s => (object?)s.EndTime ?? DBNull.Value))
                .ForMember(d => d.TotalValue, o => o.MapFrom(s => (decimal?)s.TotalValue))
                .ForMember(d => d.ClientConfirmedAt, o => o.MapFrom(s => s.ClientConfirmedAt))
                .ForMember(d => d.CoworkingConfirmedAt, o => o.MapFrom(s => s.CoworkingConfirmedAt));
            CreateMap<Interest, ReservationOrInterestItemDto>()
                .ForMember(d => d.Type, o => o.MapFrom(_ => "Interest"))
                .ForMember(d => d.ReservationDate, o => o.MapFrom(s => s.AllocationDate ?? DateTime.MinValue))
                .ForMember(d => d.ClientName, o => o.MapFrom(s => $"{s.Name} {s.LastName}".Trim()))
                .ForMember(d => d.ClientEmail, o => o.MapFrom(s => s.Email ?? ""))
                .ForMember(d => d.ClientPhone, o => o.MapFrom(s => s.Phone ?? ""))
                .ForMember(d => d.Address, o => o.MapFrom(s => s.CoworkingAddress))
                .ForMember(d => d.StartTime, o => o.MapFrom(s => (object?)s.StartTime ?? DBNull.Value))
                .ForMember(d => d.EndTime, o => o.MapFrom(s => (object?)s.EndTime ?? DBNull.Value))
                .ForMember(d => d.RoomType, o => o.MapFrom(_ => ""))
                .ForMember(d => d.TotalValue, o => o.MapFrom(_ => (decimal?)null))
                .ForMember(d => d.ClientConfirmedAt, o => o.MapFrom(_ => (DateTime?)null))
                .ForMember(d => d.CoworkingConfirmedAt, o => o.MapFrom(_ => (DateTime?)null));
        }
    }
}
