using GatewayService.Dto;
using GatewayService.Models;

namespace GatewayService.HttpClients;

public interface IBonusClient
{
    Task<ServiceResponse<PrivilegeInfoResponse?>> GetPrivilegeInfoAsync();
    Task<ServiceResponse<PrivilegeShortInfo?>> GetPrivilegeShortInfoAsync();
    Task<ServiceResponse<bool>> UpdatePrivilegeAfterPurchase(TicketPurchaseRequest request, Guid ticketUid, int paidByBonuses, int paidByMoney, int bonusToAdd = 0);
    Task<ServiceResponse<bool>> UpdatePrivilegeAfterCancel(Guid ticketUid);
}