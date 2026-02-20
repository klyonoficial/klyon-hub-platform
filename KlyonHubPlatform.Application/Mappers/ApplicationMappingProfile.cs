using AutoMapper;
using KlyonHubPlatform.Application.DTOs;
using KlyonHubPlatform.Application.DTOs.Asaas;
using KlyonHubPlatform.Application.DTOs.Dashboard;
using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Application.Helpers;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Application.Mappers
{
    public class ApplicationMappingProfile : Profile
    {
        public ApplicationMappingProfile()
        {
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
            CreateMap<ChatConversation, ConversationDto>()
                .ForMember(d => d.Messages, o => o.MapFrom(s => s.Messages));
            CreateMap<ChatMessage, MessageDto>();
            CreateMap<ChatFaqItem, FaqItemDto>();
            CreateMap<ChatUnansweredQuestion, UnansweredQuestionDto>()
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.ReviewedByName, o => o.MapFrom(s => s.Reviewer != null ? s.Reviewer.FullName : null));
            CreateMap<ChatMessage, ChatResponseDto>()
                .ForMember(d => d.Response, o => o.MapFrom(s => s.Content));
            CreateMap<Reservation, CoworkingReservationDetailRowDto>()
                .ForMember(d => d.RoomTypeDisplay, o => o.MapFrom(s =>
                    s.RoomType == "Shared" ? "Sala compartilhada" :
                    s.RoomType == "Private" ? "Sala privativa" :
                    s.RoomType == "Auditorium" ? "Auditório" : (s.RoomType ?? "")))
                .ForMember(d => d.ClientName, o => o.MapFrom(s =>
                    s.Client == null ? "Cliente não identificado" : (s.Client.RazaoSocial ?? s.Client.NomeFantasia ?? s.Client.ContactName ?? "N/A")))
                .ForMember(d => d.Status, o => o.MapFrom(s =>
                    s.Status == ReservationStatus.Completed ? "Concluída" : "Confirmada"))
                .ForMember(d => d.CreditsUsed, o => o.MapFrom(_ => 0));
            CreateMap<AsaasPaymentResponse, OrderResponse>()
                .ForMember(d => d.ReferenceId, o => o.MapFrom(s => s.ExternalReference ?? ""))
                .ForMember(d => d.CreatedAt, o => o.MapFrom(s => AsaasDateParser.TryParse(s.DateCreated) ?? DateTime.UtcNow))
                .ForMember(d => d.PaidAt, o => o.MapFrom(s => AsaasDateParser.TryParse(s.ConfirmedDate ?? s.ClientPaymentDate)));
        }
    }
}