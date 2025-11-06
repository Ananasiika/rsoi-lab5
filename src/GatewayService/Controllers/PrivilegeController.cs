using GatewayService.Models;
using GatewayService.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace GatewayService.Controllers;

[ApiController]
[Route("api/v1/privilege")]
public class PrivilegeController : ControllerBase
{
    private readonly IGatewayService _gatewayService;
    private readonly ILogger<PrivilegeController> _logger;

    public PrivilegeController(IGatewayService gatewayService, ILogger<PrivilegeController> logger)
    {
        _gatewayService = gatewayService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetPrivilegeInfo()
    {
        var response = await _gatewayService.GetPrivilegeInfoAsync();
        var json = JsonSerializer.Serialize(response);
        _logger.LogInformation(json);
        
        return response.IsSuccess ? Ok(response.Response) : StatusCode(503, new { message = "Bonus Service unavailable" });
    }
}