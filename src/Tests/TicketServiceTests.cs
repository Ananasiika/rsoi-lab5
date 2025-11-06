using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using TicketService.Controllers;
using TicketService.Database;
using TicketService.Dto;
using TicketService.Models;

namespace Tests;

public class TicketServiceTests : IDisposable
{
    private readonly TicketDatabaseContext _context;
    private readonly TicketsController _ticketsController;

    public TicketServiceTests()
    {
        _context = DbHelper.CreateContext<TicketDatabaseContext>();
        var ticketService = new TicketService.Services.TicketService(_context);
        _ticketsController = new TicketsController(ticketService);

        SeedData();
        
        // Setup default user context
        SetupUserContext("user1");
    }

    private void SetupUserContext(string username)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, username),
            new Claim("preferred_username", username),
            new Claim("sub", Guid.NewGuid().ToString())
        }, "TestAuthentication"));

        _ticketsController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    private void SeedData()
    {
        // Clear existing data first
        _context.Tickets.RemoveRange(_context.Tickets);
        _context.SaveChanges();

        var tickets = new List<Ticket>
        {
            new Ticket
            {
                Id = 1,
                TicketUid = Guid.NewGuid(),
                FlightNumber = "FL123",
                Price = 5000,
                Status = TicketStatus.PAID,
                Username = "user1"
            },
            new()
            {
                Id = 2,
                TicketUid = Guid.NewGuid(),
                FlightNumber = "FL456",
                Price = 4500,
                Status = TicketStatus.PAID,
                Username = "user1"
            },
            new()
            {
                Id = 3,
                TicketUid = Guid.NewGuid(),
                FlightNumber = "FL789",
                Price = 6000,
                Status = TicketStatus.PAID,
                Username = "user2"
            }
        };

        _context.Tickets.AddRange(tickets);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    [Fact]
    public async Task GetUserTickets_NoUserInToken_ReturnsUnauthorized()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        _ticketsController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        // Act
        var result = await _ticketsController.GetUserTickets();

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task GetTicket_ValidUserAndTicket_ReturnsOk()
    {
        // Arrange
        var existingTicket = await _context.Tickets.FirstAsync(t => t.Username == "user1");
        var ticketUid = existingTicket.TicketUid;
        SetupUserContext("user1");

        // Act
        var result = await _ticketsController.GetTicket(ticketUid);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var ticket = okResult.Value;

        Assert.NotNull(ticket);

        // Check ticket properties using reflection
        var flightNumberProperty = ticket.GetType().GetProperty("FlightNumber");
        var priceProperty = ticket.GetType().GetProperty("Price");

        Assert.Equal(existingTicket.FlightNumber, flightNumberProperty?.GetValue(ticket)?.ToString());
        Assert.Equal(existingTicket.Price, (int?)priceProperty?.GetValue(ticket));
    }

    [Fact]
    public async Task GetTicket_InvalidUser_ReturnsNotFound()
    {
        // Arrange
        var existingTicket = await _context.Tickets.FirstAsync(t => t.Username == "user2");
        var ticketUid = existingTicket.TicketUid;
        SetupUserContext("user1"); // Different user

        // Act
        var result = await _ticketsController.GetTicket(ticketUid);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetTicket_NoUserInToken_ReturnsUnauthorized()
    {
        // Arrange
        var existingTicket = await _context.Tickets.FirstAsync();
        var ticketUid = existingTicket.TicketUid;
        
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        _ticketsController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        // Act
        var result = await _ticketsController.GetTicket(ticketUid);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task PurchaseTicket_ValidRequest_ReturnsOk()
    {
        // Arrange
        SetupUserContext("user3");
        var request = new TicketPurchaseRequestDto
        {
            FlightNumber = "FL999",
            Price = 7000
        };

        // Act
        var result = await _ticketsController.PurchaseTicket(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var ticket = okResult.Value;

        Assert.NotNull(ticket);

        // Check ticket properties using reflection
        var flightNumberProperty = ticket.GetType().GetProperty("FlightNumber");
        var priceProperty = ticket.GetType().GetProperty("Price");
        var statusProperty = ticket.GetType().GetProperty("Status");

        Assert.Equal("FL999", flightNumberProperty?.GetValue(ticket)?.ToString());
        Assert.Equal(7000, (int?)priceProperty?.GetValue(ticket));
        Assert.Equal("PAID", statusProperty?.GetValue(ticket)?.ToString());

        // Verify ticket was created
        var createdTicket = await _context.Tickets.FirstOrDefaultAsync(t =>
            t.Username == "user3" && t.FlightNumber == "FL999");
        Assert.NotNull(createdTicket);
        Assert.Equal(7000, createdTicket.Price);
        Assert.Equal(TicketStatus.PAID, createdTicket.Status);
    }

    [Fact]
    public async Task PurchaseTicket_NoUserInToken_ReturnsUnauthorized()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        _ticketsController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var request = new TicketPurchaseRequestDto
        {
            FlightNumber = "FL999",
            Price = 7000
        };

        // Act
        var result = await _ticketsController.PurchaseTicket(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task DeleteTicket_ValidUserAndTicket_ReturnsNoContent()
    {
        // Arrange
        var existingTicket = await _context.Tickets.FirstAsync(t => t.Username == "user1");
        var ticketUid = existingTicket.TicketUid;
        SetupUserContext("user1");

        // Act
        var result = await _ticketsController.DeleteTicket(ticketUid);

        // Assert
        Assert.IsType<NoContentResult>(result);

        // Verify ticket status was updated to CANCELED
        var canceledTicket = await _context.Tickets.FirstAsync(t => t.TicketUid == ticketUid);
        Assert.Equal(TicketStatus.CANCELED, canceledTicket.Status);
    }

    [Fact]
    public async Task DeleteTicket_InvalidUser_ReturnsNotFound()
    {
        // Arrange
        var existingTicket = await _context.Tickets.FirstAsync(t => t.Username == "user2");
        var ticketUid = existingTicket.TicketUid;
        SetupUserContext("user1"); // Different user

        // Act
        var result = await _ticketsController.DeleteTicket(ticketUid);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteTicket_NoUserInToken_ReturnsUnauthorized()
    {
        // Arrange
        var existingTicket = await _context.Tickets.FirstAsync();
        var ticketUid = existingTicket.TicketUid;
        
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        _ticketsController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        // Act
        var result = await _ticketsController.DeleteTicket(ticketUid);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }
}