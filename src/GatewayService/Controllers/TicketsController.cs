using GatewayService.Models;
using GatewayService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace GatewayService.Controllers;

[ApiController]
[Route("api/v1/tickets")]
public class TicketsController : ControllerBase
{
    private readonly IGatewayService _gatewayService;
    private readonly ILogger<TicketsController> _logger;

    public TicketsController(IGatewayService gatewayService, ILogger<TicketsController> logger)
    {
        _gatewayService = gatewayService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetUserTickets()
    {
        var response = await _gatewayService.GetUserTicketsAsync();
        
        if (response.IsSuccess)
        {
            return Ok(response.Response);
        }
        
        var errorMessage = response.Error?.Message ?? "Service error";
        return StatusCode(response.StatusCode, new { message = errorMessage });
    }

    [HttpGet("{ticketUid}")]
    [Authorize]
    public async Task<IActionResult> GetTicket([FromRoute] Guid ticketUid)
    {
        var response = await _gatewayService.GetTicketAsync(ticketUid);
        
        if (response.IsSuccess)
        {
            if (response.Response == null)
            {
                return NotFound(new { message = "Ticket not found" });
            }
            return Ok(response.Response);
        }
        
        var errorMessage = response.Error?.Message ?? "Service error";
        return StatusCode(response.StatusCode, new { message = errorMessage });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> PurchaseTicket([FromBody] TicketPurchaseRequest request)
    {
        var response = await _gatewayService.PurchaseTicketAsync(request);
        
        if (response.IsSuccess)
        {
            if (response.Response == null)
            {
                return BadRequest(new { message = "Failed to purchase ticket" });
            }
            return Ok(response.Response);
        }
        
        var errorMessage = response.Error?.Message ?? "Service error";
        return StatusCode(response.StatusCode, new { message = errorMessage });
    }

    [HttpDelete("{ticketUid}")]
    [Authorize]
    public async Task<IActionResult> CancelTicket([FromRoute] Guid ticketUid)
    {
        var response = await _gatewayService.CancelTicketAsync(ticketUid);
        
        if (response.IsSuccess)
        {
            return NoContent();
        }
        
        var errorMessage = response.Error?.Message ?? "Service error";
        return StatusCode(response.StatusCode, new { message = errorMessage });
    }
}