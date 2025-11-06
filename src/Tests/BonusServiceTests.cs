using BonusService.Dto;
using BonusService.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using GatewayService.Models;
using PrivilegeController = BonusService.Controllers.PrivilegeController;

namespace Tests;

public class BonusServiceTests
{
    private readonly Mock<IPrivilegeService> _mockPrivilegeService;
    private readonly PrivilegeController _privilegeController;

    public BonusServiceTests()
    {
        _mockPrivilegeService = new Mock<IPrivilegeService>();
        _privilegeController = new PrivilegeController(_mockPrivilegeService.Object);
        
        // Setup default user context
        SetupUserContext("testuser");
    }

    private void SetupUserContext(string username)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, username),
            new Claim("preferred_username", username),
            new Claim("sub", Guid.NewGuid().ToString())
        }, "TestAuthentication"));

        _privilegeController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetPrivilegeInfo_NoUserInToken_ReturnsUnauthorized()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity()); // Empty identity
        _privilegeController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        // Act
        var result = await _privilegeController.GetPrivilegeInfo();

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task ProcessPurchase_ValidRequest_ReturnsOk()
    {
        // Arrange
        var username = "testuser";
        var request = new PurchaseUpdateRequest
        {
            TicketUid = Guid.NewGuid(),
            Price = 5000,
            PaidFromBalance = true
        };

        _mockPrivilegeService.Setup(x => x.ProcessPurchaseAsync(username, request))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _privilegeController.UpdateAfterPurchase(request);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ProcessPurchase_NoUserInToken_ReturnsUnauthorized()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        _privilegeController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var request = new PurchaseUpdateRequest
        {
            TicketUid = Guid.NewGuid(),
            Price = 5000,
            PaidFromBalance = true
        };

        // Act
        var result = await _privilegeController.UpdateAfterPurchase(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task ProcessCancel_ValidRequest_ReturnsOk()
    {
        // Arrange
        var username = "testuser";
        var request = new CancelUpdateRequest
        {
            TicketUid = Guid.NewGuid()
        };

        _mockPrivilegeService.Setup(x => x.ProcessCancelAsync(username, request))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _privilegeController.UpdateAfterCancel(request);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ProcessCancel_NoUserInToken_ReturnsUnauthorized()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        _privilegeController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var request = new CancelUpdateRequest
        {
            TicketUid = Guid.NewGuid()
        };

        // Act
        var result = await _privilegeController.UpdateAfterCancel(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task ProcessPurchase_ServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var username = "testuser";
        var request = new PurchaseUpdateRequest
        {
            TicketUid = Guid.NewGuid(),
            Price = 5000,
            PaidFromBalance = true
        };

        _mockPrivilegeService.Setup(x => x.ProcessPurchaseAsync(username, request))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _privilegeController.UpdateAfterPurchase(request);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }
}