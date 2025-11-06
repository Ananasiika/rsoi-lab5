using GatewayService.Models;
using System.Text;
using System.Text.Json;
using GatewayService.Dto;

namespace GatewayService.HttpClients;
public class BonusClient : IBonusClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BonusClient> _logger;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BonusClient(HttpClient httpClient, ILogger<BonusClient> logger, CircuitBreaker circuitBreaker, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _logger = logger;
        _circuitBreaker = circuitBreaker;
        _httpContextAccessor = httpContextAccessor;

        _circuitBreaker.RegisterHealthCheck("BonusService", HealthCheckAsync);
    }

    private string? GetAuthToken()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();
    }
    
    private async Task<bool> HealthCheckAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/manage/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ServiceResponse<PrivilegeInfoResponse?>> GetPrivilegeInfoAsync()
    {
        return await _circuitBreaker.ExecuteAsync(
            "BonusService",
            async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/privilege");
                var token = GetAuthToken();
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Add("Authorization", token);
                }

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var privilegeInfo = JsonSerializer.Deserialize<PrivilegeInfoResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return ServiceResponse<PrivilegeInfoResponse?>.Success(privilegeInfo);
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return ServiceResponse<PrivilegeInfoResponse?>.Success(new PrivilegeInfoResponse
                    {
                        Balance = 0,
                        Status = "BRONZE",
                        History = new List<BalanceHistory>()
                    });
                }
                _logger.LogError("Error while getting privilege info");
                
                return ServiceResponse<PrivilegeInfoResponse?>.ErrorResponse(
                    $"Failed to get privilege info: {response.StatusCode}", 
                    (int)response.StatusCode);
            },
            ServiceResponse<PrivilegeInfoResponse?>.ServiceUnavailable("Bonus"));
    }

    public async Task<ServiceResponse<PrivilegeShortInfo?>> GetPrivilegeShortInfoAsync()
    {
        var result = await GetPrivilegeInfoAsync();
        
        if (result.IsSuccess)
        {
            var shortInfo = new PrivilegeShortInfo
            {
                Balance = result.Response?.Balance ?? 0,
                Status = result.Response?.Status ?? "BRONZE"
            };
            return ServiceResponse<PrivilegeShortInfo?>.Success(shortInfo);
        }

        return ServiceResponse<PrivilegeShortInfo?>.ServiceUnavailable("Bonus");
    }

    public async Task<ServiceResponse<bool>> UpdatePrivilegeAfterPurchase(TicketPurchaseRequest request, Guid ticketUid, int paidByBonuses, int paidByMoney, int bonusToAdd = 0)
    {
        return await _circuitBreaker.ExecuteAsync(
            "BonusService",
            async () =>
            {
                var updateRequest = new
                {
                    TicketUid = ticketUid,
                    FlightNumber = request.FlightNumber,
                    Price = request.Price,
                    PaidFromBalance = request.PaidFromBalance,
                    PaidByBonuses = paidByBonuses,
                    PaidByMoney = paidByMoney,
                    BonusToAdd = bonusToAdd
                };

                var json = JsonSerializer.Serialize(updateRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/privilege/purchase")
                {
                    Content = content
                };
                var token = GetAuthToken();
                if (!string.IsNullOrEmpty(token))
                {
                    httpRequest.Headers.Add("Authorization", token);
                }

                var response = await _httpClient.SendAsync(httpRequest);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully updated privilege after purchase");
                    return ServiceResponse<bool>.Success(true);
                }
                
                return ServiceResponse<bool>.ErrorResponse(
                    $"Failed to update privilege: {response.StatusCode}", 
                    (int)response.StatusCode);
            },
            ServiceResponse<bool>.ServiceUnavailable("Bonus"));
    }

    public async Task<ServiceResponse<bool>> UpdatePrivilegeAfterCancel(Guid ticketUid)
    {
        return await _circuitBreaker.ExecuteAsync(
            "BonusService",
            async () =>
            {
                var cancelRequest = new
                {
                    TicketUid = ticketUid
                };

                var json = JsonSerializer.Serialize(cancelRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/privilege/cancel")
                {
                    Content = content
                };
                
                var token = GetAuthToken();
                if (!string.IsNullOrEmpty(token))
                {
                    httpRequest.Headers.Add("Authorization", token);
                }

                var response = await _httpClient.SendAsync(httpRequest);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully updated privilege after cancel");
                    return ServiceResponse<bool>.Success(true);
                }
                
                return ServiceResponse<bool>.ErrorResponse(
                    $"Failed to update privilege: {response.StatusCode}", 
                    (int)response.StatusCode);
            },
            ServiceResponse<bool>.ServiceUnavailable("Bonus"));
    }
}