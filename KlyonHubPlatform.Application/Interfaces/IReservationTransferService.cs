using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Executa o repasse de créditos ao coworking quando a reserva recebe dupla confirmação
    /// (cliente + coworking), aplicando a comissão parametrizada e debitando o saldo da plataforma.
    /// </summary>
    public interface IReservationTransferService
    {
        /// <summary>
        /// Se a reserva tiver ClientConfirmedAt e CoworkingConfirmedAt preenchidos e Status != Completed,
        /// cria ReservationCommission, Transfer para o coworking e marca a reserva como Completed.
        /// Idempotente: se já Completed, não faz nada.
        /// </summary>
        Task ExecuteTransferIfBothConfirmedAsync(Reservation reservation);
    }
}
