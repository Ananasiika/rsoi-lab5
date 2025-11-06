using GatewayService.Dto;
using GatewayService.Models;

namespace GatewayService.Services;

public interface IGatewayService
{
    Task<ServiceResponse<PaginationResponse<FlightDto>>> GetFlightsAsync(int page, int size);
    Task<ServiceResponse<UserInfoResponse>> GetUserInfoAsync();
    Task<ServiceResponse<List<TicketResponse>>> GetUserTicketsAsync();
    Task<ServiceResponse<TicketResponse?>> GetTicketAsync(Guid ticketUid);
    Task<ServiceResponse<TicketPurchaseResponse?>> PurchaseTicketAsync(TicketPurchaseRequest request);
    Task<ServiceResponse<bool>> CancelTicketAsync(Guid ticketUid);
    Task<ServiceResponse<PrivilegeInfoResponse?>> GetPrivilegeInfoAsync();
}