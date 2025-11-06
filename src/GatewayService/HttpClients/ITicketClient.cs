using GatewayService.Dto;
using GatewayService.Models;

namespace GatewayService.HttpClients;

public interface ITicketClient
{
    Task<ServiceResponse<List<TicketResponse>>> GetUserTicketsAsync();
    Task<ServiceResponse<TicketResponse?>> GetTicketAsync(Guid ticketUid);
    Task<ServiceResponse<TicketPurchaseResponse?>> PurchaseTicketAsync(TicketPurchaseRequest request);
    Task<ServiceResponse<bool>> CancelTicketAsync(Guid ticketUid);
}