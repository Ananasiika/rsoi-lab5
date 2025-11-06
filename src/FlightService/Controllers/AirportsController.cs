using FlightService.Interfaces;
using FlightService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace FlightService.Controllers;

[ApiController]
[Route("api/v1/airports")]
public class AirportsController : ControllerBase
{
    private readonly IAirportService _airportService;

    public AirportsController(IAirportService airportService)
    {
        _airportService = airportService;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<Airport>>> GetAirports()
    {
        var airports = await _airportService.GetAllAirportsAsync();
        return Ok(airports);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Airport>> GetAirport(int id)
    {
        var airport = await _airportService.GetAirportByIdAsync(id);
        if (airport == null)
        {
            return NotFound();
        }
        return Ok(airport);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Airport>> CreateAirport(Airport airport)
    {
        try
        {
            var createdAirport = await _airportService.CreateAirportAsync(airport);
            return CreatedAtAction(nameof(GetAirport), new { id = createdAirport.Id }, createdAirport);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error creating airport: {ex.Message}");
        }
    }
}