using GatewayService.Dto;
using GatewayService.HttpClients;
using GatewayService.Models;

namespace GatewayService.Services;

public class GatewayService : IGatewayService
{
    private readonly IFlightClient _flightClient;
    private readonly IBonusClient _bonusClient;
    private readonly ITicketClient _ticketClient;
    private readonly ILogger<GatewayService> _logger;
    private readonly IRetryQueue _retryQueue;
    private readonly IUserContext _userContext;

    public GatewayService(
        IFlightClient flightClient,
        IBonusClient bonusClient,
        ITicketClient ticketClient,
        IRetryQueue retryQueue,
        IUserContext userContext,
        ILogger<GatewayService> logger)
    {
        _flightClient = flightClient;
        _bonusClient = bonusClient;
        _ticketClient = ticketClient;
        _retryQueue = retryQueue;
        _userContext = userContext;
        _logger = logger;
    }

    public Task<ServiceResponse<PaginationResponse<FlightDto>>> GetFlightsAsync(int page, int size)
    {
        return _flightClient.GetFlightsAsync(page, size);
    }

    public async Task<ServiceResponse<UserInfoResponse>> GetUserInfoAsync()
    {
        var username = _userContext.GetUsername();
        if (string.IsNullOrEmpty(username))
        {
            return ServiceResponse<UserInfoResponse>.ErrorResponse("User not authenticated", 401);
        }

        // 1. Tickets - критичный сервис
        var ticketsResponse = await _ticketClient.GetUserTicketsAsync();
        if (!ticketsResponse.IsSuccess)
        {
            return ServiceResponse<UserInfoResponse>.ErrorResponse(
                ticketsResponse.Error?.Message ?? "Ticket service error", 
                ticketsResponse.StatusCode);
        }

        var result = new List<TicketResponse>();

        foreach (var ticket in ticketsResponse.Response ?? new List<TicketResponse>())
        {
            // 2. Flight info - с fallback
            var flightResponse = await _flightClient.GetFlightByNumberAsync(ticket.FlightNumber);
            if (flightResponse.IsSuccess && flightResponse.Response != null)
            {
                result.Add(CreateTicketResponse(ticket, flightResponse.Response));
            }
            else
            {
                result.Add(CreateFallbackTicketResponse(ticket));
            }
        }

        // 3. Privilege - всегда fallback при ошибках
        var privilegeResponse = await _bonusClient.GetPrivilegeShortInfoAsync();
        PrivilegeShortInfo privilege;
        if (privilegeResponse.IsFallback || !privilegeResponse.IsSuccess)
        {
            // BonusService недоступен - возвращаем пустой privilege
            privilege = new PrivilegeShortInfo { Balance = 0, Status = "BRONZE" };
        }
        else
        {
            privilege = privilegeResponse.Response ?? new PrivilegeShortInfo { Balance = 0, Status = "BRONZE" };
        }

        return ServiceResponse<UserInfoResponse>.Success(new UserInfoResponse
        {
            Tickets = result,
            Privilege = privilege
        });
    }

    public async Task<ServiceResponse<List<TicketResponse>>> GetUserTicketsAsync()
    {
        var username = _userContext.GetUsername();
        if (string.IsNullOrEmpty(username))
        {
            return ServiceResponse<List<TicketResponse>>.ErrorResponse("User not authenticated", 401);
        }

        var ticketsResponse = await _ticketClient.GetUserTicketsAsync();
        if (!ticketsResponse.IsSuccess)
        {
            return ServiceResponse<List<TicketResponse>>.ErrorResponse(
                ticketsResponse.Error?.Message ?? "Ticket service error", 
                ticketsResponse.StatusCode);
        }

        var result = new List<TicketResponse>();

        foreach (var ticket in ticketsResponse.Response ?? new List<TicketResponse>())
        {
            var flightResponse = await _flightClient.GetFlightByNumberAsync(ticket.FlightNumber);
            if (flightResponse.IsSuccess && flightResponse.Response != null)
            {
                result.Add(CreateTicketResponse(ticket, flightResponse.Response));
            }
            else
            {
                result.Add(CreateFallbackTicketResponse(ticket));
            }
        }

        return ServiceResponse<List<TicketResponse>>.Success(result);
    }

    public async Task<ServiceResponse<TicketResponse?>> GetTicketAsync(Guid ticketUid)
    {
        var username = _userContext.GetUsername();
        if (string.IsNullOrEmpty(username))
        {
            return ServiceResponse<TicketResponse?>.ErrorResponse("User not authenticated", 401);
        }

        var ticketResponse = await _ticketClient.GetTicketAsync(ticketUid);
        if (!ticketResponse.IsSuccess || ticketResponse.Response == null)
        {
            return ticketResponse;
        }

        var flightResponse = await _flightClient.GetFlightByNumberAsync(ticketResponse.Response.FlightNumber);
        if (flightResponse.IsSuccess && flightResponse.Response != null)
        {
            var fullTicket = CreateTicketResponse(ticketResponse.Response, flightResponse.Response);
            return ServiceResponse<TicketResponse?>.Success(fullTicket);
        }
        else
        {
            var fallbackTicket = CreateFallbackTicketResponse(ticketResponse.Response);
            return ServiceResponse<TicketResponse?>.Success(fallbackTicket);
        }
    }

    public async Task<ServiceResponse<TicketPurchaseResponse?>> PurchaseTicketAsync(TicketPurchaseRequest request)
    {
        var username = _userContext.GetUsername();
        if (string.IsNullOrEmpty(username))
        {
            return ServiceResponse<TicketPurchaseResponse?>.ErrorResponse("User not authenticated", 401);
        }

        try
        {
            _logger.LogInformation("Starting ticket purchase for user: {Username}, flight: {FlightNumber}", 
                username, request.FlightNumber);

            // 1. FlightService - критичный
            var flightResponse = await _flightClient.GetFlightByNumberAsync(request.FlightNumber);
            if (!flightResponse.IsSuccess || flightResponse.Response == null)
            {
                var errorMsg = flightResponse.Error?.Message ?? "Flight not found";
                return ServiceResponse<TicketPurchaseResponse?>.ErrorResponse(errorMsg, flightResponse.StatusCode);
            }

            var flight = flightResponse.Response;

            // 2. BonusService - не критичный
            var privilegeResponse = await _bonusClient.GetPrivilegeShortInfoAsync();
            var privilegeInfo = privilegeResponse.Response;

            // 3. Рассчитать суммы оплаты
            int paidByMoney, paidByBonuses, bonusToAdd;
            CalculatePaidAmounts(request, privilegeInfo, out paidByBonuses, out paidByMoney, out bonusToAdd);

            // 4. TicketService - критичный
            var ticketResponse = await _ticketClient.PurchaseTicketAsync(request);
            if (!ticketResponse.IsSuccess || ticketResponse.Response == null)
            {
                var errorMsg = ticketResponse.Error?.Message ?? "Failed to create ticket";
                return ServiceResponse<TicketPurchaseResponse?>.ErrorResponse(errorMsg, ticketResponse.StatusCode);
            }

            var ticket = ticketResponse.Response;

            // 5. BonusService update - не критичный, но при ошибке откатываем
            var bonusUpdateResponse = await _bonusClient.UpdatePrivilegeAfterPurchase(
                request, ticket.TicketUid, paidByBonuses, paidByMoney, bonusToAdd);

            if (!bonusUpdateResponse.IsSuccess)
            {
                _logger.LogWarning("Failed to update bonus system, rolling back ticket creation");
                
                // Откат билета
                await _ticketClient.CancelTicketAsync(ticket.TicketUid);
                
                return ServiceResponse<TicketPurchaseResponse?>.ErrorResponse(
                    "Bonus Service unavailable", 503);
            }

            // 6. Получить обновленные бонусы (не критично)
            var updatedPrivilegeResponse = await _bonusClient.GetPrivilegeShortInfoAsync();
            var updatedPrivilege = updatedPrivilegeResponse.Response ?? new PrivilegeShortInfo { Balance = 0, Status = "BRONZE" };

            var purchaseResponse = new TicketPurchaseResponse
            {
                TicketUid = ticket.TicketUid,
                FlightNumber = flight.FlightNumber,
                FromAirport = FormatAirport(flight.FromAirport),
                ToAirport = FormatAirport(flight.ToAirport),
                Date = flight.Date,
                Price = request.Price,
                PaidByMoney = paidByMoney,
                PaidByBonuses = paidByBonuses,
                Status = "PAID",
                Privilege = updatedPrivilege
            };

            return ServiceResponse<TicketPurchaseResponse?>.Success(purchaseResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error purchasing ticket for user: {Username}", username);
            return ServiceResponse<TicketPurchaseResponse?>.ErrorResponse("Internal server error", 500);
        }
    }

    public async Task<ServiceResponse<bool>> CancelTicketAsync(Guid ticketUid)
    {
        var username = _userContext.GetUsername();
        if (string.IsNullOrEmpty(username))
        {
            return ServiceResponse<bool>.ErrorResponse("User not authenticated", 401);
        }

        // 1. Отменяем билет - критичный
        var cancelResponse = await _ticketClient.CancelTicketAsync(ticketUid);
        if (!cancelResponse.IsSuccess)
        {
            return cancelResponse;
        }

        // 2. Обновляем бонусный счет - не критичный, идет в retry queue при ошибке
        var bonusResponse = await _bonusClient.UpdatePrivilegeAfterCancel(ticketUid);
        if (!bonusResponse.IsSuccess)
        {
            _logger.LogWarning("Bonus service unavailable for cancel operation, adding to retry queue");
            
            // Добавляем в retry queue
            _retryQueue.Enqueue(new RetryItem
            {
                OperationType = "UpdatePrivilegeAfterCancel",
                Username = username,
                Data = new { TicketUid = ticketUid },
                Action = async () =>
                {
                    var retryResponse = await _bonusClient.UpdatePrivilegeAfterCancel(ticketUid);
                    return retryResponse.IsSuccess;
                }
            });
        }

        return ServiceResponse<bool>.Success(true);
    }

    public Task<ServiceResponse<PrivilegeInfoResponse?>> GetPrivilegeInfoAsync()
    {
        var username = _userContext.GetUsername();
        if (string.IsNullOrEmpty(username))
        {
            return Task.FromResult(ServiceResponse<PrivilegeInfoResponse?>.ErrorResponse("User not authenticated", 401));
        }

        // Этот метод критичный - пробрасываем ошибки от BonusService
        return _bonusClient.GetPrivilegeInfoAsync();
    }

    // Вспомогательные методы
    private TicketResponse CreateTicketResponse(TicketResponse ticket, FlightDto flight)
    {
        return new TicketResponse
        {
            TicketUid = ticket.TicketUid,
            FlightNumber = ticket.FlightNumber,
            FromAirport = FormatAirport(flight.FromAirport),
            ToAirport = FormatAirport(flight.ToAirport),
            Date = flight.Date,
            Price = ticket.Price,
            Status = ticket.Status
        };
    }

    private TicketResponse CreateFallbackTicketResponse(TicketResponse ticket)
    {
        return new TicketResponse
        {
            TicketUid = ticket.TicketUid,
            FlightNumber = ticket.FlightNumber,
            FromAirport = "Unknown Airport",
            ToAirport = "Unknown Airport", 
            Date = DateTime.MinValue,
            Price = ticket.Price,
            Status = ticket.Status
        };
    }

    private string FormatAirport(AirportDto airport)
    {
        if (airport == null)
            return "Unknown Airport";
        
        var parts = new List<string>();
        
        if (!string.IsNullOrEmpty(airport.City))
            parts.Add(airport.City);
            
        if (!string.IsNullOrEmpty(airport.Name))
            parts.Add(airport.Name);
        
        return parts.Any() ? string.Join(" ", parts) : "Unknown Airport";
    }

    private int CalculateBonusToAdd(int price, string status)
    {
        return (int)(price * 0.1);
    }

    private void CalculatePaidAmounts(TicketPurchaseRequest request, PrivilegeShortInfo? privilege, 
        out int paidByBonuses, out int paidByMoney, out int bonusToAdd)
    {
        paidByBonuses = 0;
        paidByMoney = request.Price;
        bonusToAdd = 0;

        if (request.PaidFromBalance && privilege != null)
        {
            paidByBonuses = Math.Min(privilege.Balance, request.Price);
            paidByMoney = request.Price - paidByBonuses;
        }

        bonusToAdd = CalculateBonusToAdd(request.Price, privilege?.Status ?? "BRONZE");
    }
}