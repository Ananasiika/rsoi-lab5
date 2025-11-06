using Microsoft.AspNetCore.Mvc;
using TicketService.Dto;
using TicketService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace TicketService.Controllers;

[ApiController]
[Route("api/v1/tickets")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;

    public TicketsController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    [HttpGet("manage/health")]
    public IActionResult Health()
    {
        return Ok();
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetUserTickets()
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("preferred_username")?.Value 
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized("Username not found in token");
        }

        var tickets = await _ticketService.GetUserTicketsAsync(username);

        // Возвращаем полный формат как ожидает Gateway
        var response = tickets.Select(t => new
        {
            t.TicketUid,
            t.FlightNumber,
            FromAirport = "Unknown", // Gateway добавит правильные данные
            ToAirport = "Unknown",   // Gateway добавит правильные данные
            Date = DateTime.MinValue, // Gateway добавит правильные данные
            t.Price,
            Status = t.Status.ToString()
        });

        return Ok(response);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> PurchaseTicket([FromBody] TicketPurchaseRequestDto request)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("preferred_username")?.Value 
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized("Username not found in token");
        }

        try
        {
            var ticket = await _ticketService.CreateTicketAsync(request, username);

            // Возвращаем полный ответ с TicketUid
            var response = new
            {
                TicketUid = ticket.TicketUid,
                FlightNumber = request.FlightNumber,
                FromAirport = "Unknown",
                ToAirport = "Unknown",
                Date = DateTime.MinValue,
                Price = request.Price,
                Status = ticket.Status.ToString()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("{ticketUid}")]
    [Authorize]
    public async Task<IActionResult> GetTicket(Guid ticketUid)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("preferred_username")?.Value 
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized("Username not found in token");
        }

        var ticket = await _ticketService.GetTicketByUidAsync(ticketUid);
        if (ticket == null || ticket.Username != username)
        {
            return NotFound();
        }

        var response = new
        {
            ticket.TicketUid,
            ticket.FlightNumber,
            ticket.Price,
            Status = ticket.Status.ToString()
        };
        return Ok(response);
    }

    [HttpDelete("{ticketUid}")]
    [Authorize]
    public async Task<IActionResult> DeleteTicket(Guid ticketUid)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("preferred_username")?.Value 
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized("Username not found in token");
        }

        var ticket = await _ticketService.GetTicketByUidAsync(ticketUid);
        if (ticket == null || ticket.Username != username)
        {
            return NotFound();
        }

        var success = await _ticketService.DeleteTicketAsync(ticketUid);
        if (!success)
        {
            return StatusCode(500, "Failed to cancel ticket");
        }

        return NoContent(); // 204 No Content
    }
}