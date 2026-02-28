using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace KlyonHubPlatform.Application.Services
{
    public class CorrespondenceService : ICorrespondenceService
    {
        private readonly ICorrespondenceRepository _correspondenceRepository;
        private readonly IContractRepository _contractRepository;
        private readonly IOperatingHoursRepository _operatingHoursRepository;
        private readonly IEmailService _emailService;
        private readonly IAppBaseUrlProvider _appBaseUrlProvider;
        private readonly ILogger<CorrespondenceService> _logger;

        public CorrespondenceService(
            ICorrespondenceRepository correspondenceRepository,
            IContractRepository contractRepository,
            IOperatingHoursRepository operatingHoursRepository,
            IEmailService emailService,
            IAppBaseUrlProvider appBaseUrlProvider,
            ILogger<CorrespondenceService> logger)
        {
            _correspondenceRepository = correspondenceRepository;
            _contractRepository = contractRepository;
            _operatingHoursRepository = operatingHoursRepository;
            _emailService = emailService;
            _appBaseUrlProvider = appBaseUrlProvider;
            _logger = logger;
        }

        public async Task<Correspondence?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _correspondenceRepository.GetByIdAsync(id, cancellationToken);
        }

        public async Task<Correspondence> RegisterArrivalAsync(
            int contractId,
            DateTime arrivalDate,
            string? notes,
            int createdByUserId,
            IReadOnlyList<(string FilePath, string FileName)> photos,
            CancellationToken cancellationToken = default)
        {
            var contract = await _contractRepository.GetByIdAsync(contractId, cancellationToken);
            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado.");

            var correspondence = new Correspondence
            {
                ContractId = contractId,
                ArrivalDate = arrivalDate,
                Notes = notes,
                CreatedByUserId = createdByUserId,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var (path, fileName) in photos)
            {
                correspondence.Photos.Add(new CorrespondencePhoto
                {
                    FilePath = path,
                    FileName = fileName,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var created = await _correspondenceRepository.CreateAsync(correspondence, cancellationToken);

            var clientEmail = contract.Client?.User?.Email ?? contract.Client?.ContactEmail;
            if (!string.IsNullOrEmpty(clientEmail))
            {
                try
                {
                    var baseUrl = await _appBaseUrlProvider.GetBaseUrlAsync();
                    var schedulePickupUrl = $"{baseUrl}/Client/SchedulePickup/{created.Id}";
                    var clientName = contract.Client?.ContactName ?? contract.Client?.RazaoSocial ?? contract.Client?.NomeFantasia ?? "Cliente";
                    var addressInfo = contract.CoworkingAddress != null
                        ? $"{contract.CoworkingAddress.Address}, {contract.CoworkingAddress.Number}" + (string.IsNullOrEmpty(contract.CoworkingAddress.Complement) ? "" : $" - {contract.CoworkingAddress.Complement}")
                        : "Endereço fiscal";
                    await _emailService.SendCorrespondenceArrivalNotificationAsync(clientEmail, clientName, addressInfo, arrivalDate, schedulePickupUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar email de notificação de correspondência para {Email}", clientEmail);
                }
            }
            else
            {
                _logger.LogWarning("Cliente do contrato {ContractId} não possui email para notificação de correspondência.", contractId);
            }

            return created;
        }

        public async Task<List<Correspondence>> GetByClientIdAsync(int clientId, CancellationToken cancellationToken = default)
        {
            return await _correspondenceRepository.GetByClientIdAsync(clientId, cancellationToken);
        }

        public async Task<List<Correspondence>> GetByCoworkingIdAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _correspondenceRepository.GetByCoworkingIdAsync(coworkingId, cancellationToken);
        }

        public async Task<Correspondence> SchedulePickupAsync(int correspondenceId, DateTime scheduledDate, TimeSpan scheduledTime, int clientId, CancellationToken cancellationToken = default)
        {
            var correspondence = await _correspondenceRepository.GetByIdAsync(correspondenceId, cancellationToken);
            if (correspondence == null)
                throw new InvalidOperationException("Correspondência não encontrada.");
            if (correspondence.Contract?.CoworkingAddress == null)
                throw new InvalidOperationException("Endereço de retirada não encontrado.");
            if (correspondence.Contract.ClientId != clientId)
                throw new UnauthorizedAccessException("Esta correspondência não pertence a você.");
            if (correspondence.PickupStatus != CorrespondencePickupStatus.AwaitingSchedule)
                throw new InvalidOperationException("Esta correspondência já possui retirada agendada ou já foi retirada.");

            var coworkingId = correspondence.Contract.CoworkingAddress.CoworkingId;
            var dayOfWeek = scheduledDate.DayOfWeek.ToString().ToUpperInvariant();
            var operatingHours = await _operatingHoursRepository.GetByCoworkingAndDayAsync(coworkingId, dayOfWeek, cancellationToken);
            if (operatingHours == null || !operatingHours.IsActive)
                throw new InvalidOperationException("Não é possível agendar retirada neste dia: o espaço não possui horário de funcionamento cadastrado.");
            if (!operatingHours.OpeningTime.HasValue || !operatingHours.ClosingTime.HasValue)
                throw new InvalidOperationException("O horário de funcionamento deste dia não está configurado. Entre em contato com o parceiro.");
            if (scheduledTime < operatingHours.OpeningTime.Value || scheduledTime > operatingHours.ClosingTime.Value)
            {
                var openStr = operatingHours.OpeningTime.Value.ToString(@"hh\:mm");
                var closeStr = operatingHours.ClosingTime.Value.ToString(@"hh\:mm");
                throw new InvalidOperationException($"O horário deve estar dentro do horário comercial: das {openStr} às {closeStr}.");
            }

            var scheduledAt = scheduledDate.Date.Add(scheduledTime);
            correspondence.PickupStatus = CorrespondencePickupStatus.Scheduled;
            correspondence.PickupScheduledAt = scheduledAt;
            return await _correspondenceRepository.UpdateAsync(correspondence, cancellationToken);
        }

        public async Task ResendArrivalNotificationAsync(int correspondenceId, int coworkingId, CancellationToken cancellationToken = default)
        {
            var correspondence = await _correspondenceRepository.GetByIdAsync(correspondenceId, cancellationToken);
            if (correspondence == null)
                throw new InvalidOperationException("Correspondência não encontrada.");
            if (correspondence.Contract.CoworkingAddress.CoworkingId != coworkingId)
                throw new UnauthorizedAccessException("Esta correspondência não pertence ao seu coworking.");

            var contract = await _contractRepository.GetByIdAsync(correspondence.ContractId, cancellationToken);
            if (contract?.Client == null) return;

            var clientEmail = contract.Client.User?.Email ?? contract.Client.ContactEmail;
            if (string.IsNullOrEmpty(clientEmail))
            {
                _logger.LogWarning("Cliente da correspondência {Id} não possui email.", correspondenceId);
                return;
            }

            var baseUrl = await _appBaseUrlProvider.GetBaseUrlAsync();
            var schedulePickupUrl = $"{baseUrl}/Client/SchedulePickup/{correspondence.Id}";
            var clientName = contract.Client.ContactName ?? contract.Client.RazaoSocial ?? contract.Client.NomeFantasia ?? "Cliente";
            var addressInfo = contract.CoworkingAddress != null
                ? $"{contract.CoworkingAddress.Address}, {contract.CoworkingAddress.Number}" + (string.IsNullOrEmpty(contract.CoworkingAddress.Complement) ? "" : $" - {contract.CoworkingAddress.Complement}")
                : "Endereço fiscal";

            await _emailService.SendCorrespondenceArrivalNotificationAsync(clientEmail, clientName, addressInfo, correspondence.ArrivalDate, schedulePickupUrl);
        }

        public async Task<Correspondence> MarkAsPickedUpAsync(int correspondenceId, int coworkingId, CancellationToken cancellationToken = default)
        {
            var correspondence = await _correspondenceRepository.GetByIdAsync(correspondenceId, cancellationToken);
            if (correspondence == null)
                throw new InvalidOperationException("Correspondência não encontrada.");
            if (correspondence.Contract.CoworkingAddress.CoworkingId != coworkingId)
                throw new UnauthorizedAccessException("Esta correspondência não pertence ao seu coworking.");

            correspondence.PickupStatus = CorrespondencePickupStatus.PickedUp;
            return await _correspondenceRepository.UpdateAsync(correspondence, cancellationToken);
        }

        public async Task<List<Correspondence>> GetByCoworkingIdFilteredAsync(int coworkingId, DateTime? pickupDateFrom, DateTime? pickupDateTo, CorrespondencePickupStatus? status, CancellationToken cancellationToken = default)
        {
            return await _correspondenceRepository.GetByCoworkingIdFilteredAsync(coworkingId, pickupDateFrom, pickupDateTo, status, cancellationToken);
        }

        public async Task<List<Correspondence>> GetAllAsync(int? coworkingIdFilter, CancellationToken cancellationToken = default)
        {
            return await _correspondenceRepository.GetAllAsync(coworkingIdFilter, cancellationToken);
        }
    }
}
