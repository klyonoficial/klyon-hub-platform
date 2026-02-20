using AutoMapper;
using KlyonHubPlatform.Application.DTOs;
using KlyonHubPlatform.Application.DTOs.Asaas;
using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Application.Helpers;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Application.Mappers
{
    /// <summary>
    /// Profiles de mapeamento entidade -> DTO na camada Application.
    /// </summary>
    public class ApplicationMappingProfile : Profile
    {
        public ApplicationMappingProfile()
        {
            // Chatbot: Conversation
            CreateMap<ChatConversation, ConversationDto>()
                .ForMember(d => d.Messages, o => o.MapFrom(s => s.Messages));

            // Chatbot: Message
            CreateMap<ChatMessage, MessageDto>();

            // Chatbot: FAQ
            CreateMap<ChatFaqItem, FaqItemDto>();

            // Chatbot: UnansweredQuestion (Status como string, Reviewer name)
            CreateMap<ChatUnansweredQuestion, UnansweredQuestionDto>()
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.ReviewedByName, o => o.MapFrom(s => s.Reviewer != null ? s.Reviewer.FullName : null));

            // Chatbot: ChatMessage -> ChatResponseDto (resposta do bot)
            CreateMap<ChatMessage, ChatResponseDto>()
                .ForMember(d => d.Response, o => o.MapFrom(s => s.Content));

            // Report: Reservation -> CoworkingReservationDetailRowDto
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

            // Asaas: AsaasPaymentResponse -> OrderResponse (datas via AsaasDateParser)
            CreateMap<AsaasPaymentResponse, OrderResponse>()
                .ForMember(d => d.ReferenceId, o => o.MapFrom(s => s.ExternalReference ?? ""))
                .ForMember(d => d.CreatedAt, o => o.MapFrom(s => AsaasDateParser.TryParse(s.DateCreated) ?? DateTime.UtcNow))
                .ForMember(d => d.PaidAt, o => o.MapFrom(s => AsaasDateParser.TryParse(s.ConfirmedDate ?? s.ClientPaymentDate)));
        }
    }
}
