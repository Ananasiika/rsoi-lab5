using GatewayService.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;

namespace GatewayService.Controllers;

[ApiController]
[Route("api/v1/me")]
public class MeController : ControllerBase
{
    private readonly IGatewayService _gatewayService;
    private readonly ILogger<MeController> _logger;
    private IUserContext _userContext;

    public MeController(IGatewayService gatewayService, ILogger<MeController> logger, IUserContext userContext)
    {
        _gatewayService = gatewayService;
        _logger = logger;
        _userContext = userContext;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetUserInfo()
    {
        var username = _userContext.GetUsername();
    
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized(new { message = "Username not found in token" });
        }

        var response = await _gatewayService.GetUserInfoAsync();
        
        if (response.IsSuccess)
        {
            var userInfo = response.Response;
            if (userInfo?.Privilege != null && userInfo.Privilege.Balance == 0 && userInfo.Privilege.Status == "BRONZE")
            {
                // Заменяем privilege на пустой объект
                return Ok(new
                {
                    tickets = userInfo.Tickets,
                    privilege = "" // Пустой объект вместо {balance: 0, status: "BRONZE"}
                });
            }
            return Ok(userInfo);
        }
        
        var errorMessage = response.Error?.Message ?? "Service error";
        return StatusCode(response.StatusCode, new { message = errorMessage });
    }
}