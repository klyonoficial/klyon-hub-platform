using AutoMapper;
using KlyonHubPlatform.Api.Dtos;
using KlyonHubPlatform.Api.Dtos.Admin;
using KlyonHubPlatform.Api.Dtos.Coworking;
using KlyonHubPlatform.Api.Dtos.Dashboard;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Constants;
namespace KlyonHubPlatform.Api.Mappers
{
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
            CreateMap<CardPriceChangeRequest, CardPriceRequestDashboardItemDto>()
                .ForMember(d => d.CoworkingName, o => o.MapFrom(s => s.CoworkingAddress != null && s.CoworkingAddress.Coworking != null ? s.CoworkingAddress.Coworking.Name : ""))
                .ForMember(d => d.CoworkingId, o => o.MapFrom(s => s.CoworkingAddress != null ? s.CoworkingAddress.CoworkingId : 0))
                .ForMember(d => d.AddressLabel, o => o.MapFrom(s => s.CoworkingAddress != null
                    ? $"{s.CoworkingAddress.Address}, {s.CoworkingAddress.Number} — {s.CoworkingAddress.City}/{s.CoworkingAddress.State}" : ""))
                .ForMember(d => d.CreatedAt, o => o.MapFrom(s => s.CreatedAt.ToString(DateFormatConstants.DisplayDateAndTime)));
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
            CreateMap<RequestRejectionUpdateDto, CapacityChangeRequest>();
            CreateMap<RequestRejectionUpdateDto, MaxContractsChangeRequest>();
            CreateMap<RequestRejectionUpdateDto, CardPriceChangeRequest>();
            CreateMap<AddressRejectionUpdateDto, CoworkingAddress>()
                .ForAllMembers(o => o.Condition((src, dest, srcVal) => srcVal != null));
            CreateMap<KlyonHubPlatform.Api.Dtos.Admin.UpdateCoworkingDto, Coworking>();
            CreateMap<RequestApprovalUpdateDto, CapacityChangeRequest>();
            CreateMap<RequestApprovalUpdateDto, MaxContractsChangeRequest>();
            CreateMap<RequestApprovalUpdateDto, CardPriceChangeRequest>();
            CreateMap<AddressApprovalUpdateDto, CoworkingAddress>()
                .ForAllMembers(o => o.Condition((src, dest, srcVal) => srcVal != null));
            CreateMap<EditUserFormDto, User>();
            CreateMap<PaymentHistory, PaymentHistoryItemDto>()
                .ForMember(d => d.ActionBy, o => o.MapFrom(s => s.ActionByUser != null ? s.ActionByUser.FullName : "Sistema"))
                .ForMember(d => d.ActionAt, o => o.MapFrom(s => s.ActionAt.ToString(DateFormatConstants.DisplayDateAndTime)));
            CreateMap<Commission, CommissionListItemDto>()
                .ForMember(d => d.CoworkingName, o => o.MapFrom(s => s.Coworking != null ? s.Coworking.Name : DefaultDisplayStrings.EmptyPlaceholder));
            CreateMap<Transfer, TransferListItemDto>()
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
            CreateMap<Client, ClientAutocompleteItemDto>()
                .ForMember(d => d.Text, o => o.MapFrom(s => $"{(s.RazaoSocial ?? s.NomeFantasia ?? s.ContactName ?? "Sem nome")} - {(s.CNPJ ?? s.CPF ?? "Sem documento")}"));
        }
    }
}