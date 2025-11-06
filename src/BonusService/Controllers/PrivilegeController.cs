using BonusService.Dto;
using BonusService.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace BonusService.Controllers;

[ApiController]
[Route("api/v1/privilege")]
public class PrivilegeController : ControllerBase
{
    private readonly IPrivilegeService _privilegeService;

    public PrivilegeController(IPrivilegeService privilegeService)
    {
        _privilegeService = privilegeService;
    }

    [HttpGet("manage/health")]
    public IActionResult Health()
    {
        return Ok();
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetPrivilegeInfo()
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
            var privilegeInfo = await _privilegeService.GetPrivilegeInfoAsync(username);
            return Ok(privilegeInfo);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPost("purchase")]
    [Authorize]
    public async Task<IActionResult> UpdateAfterPurchase([FromBody] PurchaseUpdateRequest request)
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
            await _privilegeService.ProcessPurchaseAsync(username, request);
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPost("cancel")]
    [Authorize]
    public async Task<IActionResult> UpdateAfterCancel([FromBody] CancelUpdateRequest request)
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
            await _privilegeService.ProcessCancelAsync(username, request);
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}