using GatewayService.Models;
using GatewayService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace GatewayService.Controllers;

[ApiController]
[Route("api/v1/flights")]
public class FlightsController : ControllerBase
{
    private readonly IGatewayService _gatewayService;
    private readonly ILogger<FlightsController> _logger;

    public FlightsController(IGatewayService gatewayService, ILogger<FlightsController> logger)
    {
        _gatewayService = gatewayService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetFlights([FromQuery] int page = 1, [FromQuery] int size = 10)
    {
        _logger.LogInformation("=== GET FLIGHTS REQUEST ===");
        _logger.LogInformation("Path: {Path}", HttpContext.Request.Path);
        _logger.LogInformation("QueryString: {QueryString}", HttpContext.Request.QueryString);
        _logger.LogInformation("Page: {Page}, Size: {Size}", page, size);
    
        // Логируем все заголовки
        _logger.LogInformation("--- REQUEST HEADERS ---");
        foreach (var header in HttpContext.Request.Headers)
        {
            _logger.LogInformation("  {Key}: {Value}", header.Key, header.Value);
        }

        // Логируем IP адрес и прочую информацию
        _logger.LogInformation("Client IP: {RemoteIpAddress}", HttpContext.Connection.RemoteIpAddress);
        _logger.LogInformation("User Agent: {UserAgent}", HttpContext.Request.Headers["User-Agent"].FirstOrDefault());
        
        if (page < 1 || size < 1 || size > 100)
        {
            return BadRequest(new { message = "Invalid page or size parameters" });
        }

        var response = await _gatewayService.GetFlightsAsync(page, size);
        
        if (response is { IsSuccess: true, Response: not null })
        {
            var result = new PaginationResponse<FlightResponse>
            {
                Page = response.Response.Page,
                PageSize = response.Response.PageSize,
                TotalElements = response.Response.TotalElements,
                Items = response.Response.Items.Select(f => new FlightResponse
                {
                    Date = f.Date,
                    FlightNumber = f.FlightNumber,
                    FromAirport = f.FromAirport.City + " " + f.FromAirport.Name,
                    ToAirport = f.ToAirport.City + " " + f.ToAirport.Name,
                    Price = f.Price,
                }).ToList()
            };
            return Ok(result);
        }
        
        var errorMessage = response.Error?.Message ?? "Service error";
        return StatusCode(response.StatusCode, new { message = errorMessage });
    }
}