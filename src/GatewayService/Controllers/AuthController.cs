using Microsoft.AspNetCore.Mvc;

namespace GatewayService.Controllers;

[ApiController]
[Route("oauth")]
public class AuthController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IHttpClientFactory httpClientFactory, ILogger<AuthController> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
    }

    [HttpPost("token")]
    public async Task<IActionResult> GetToken([FromForm] LoginRequest request)
    {
        try
        {
            var tokenRequest = new List<KeyValuePair<string, string>>
            {
                new("client_id", "flight-booking-client"),
                new("client_secret", "flight-booking-secret-2025-rsoi-lab5"),
                new("username", request.Email),
                new("password", request.Password),
                new("grant_type", "password"),
                new("scope", "openid")
            };

            var response = await _httpClient.PostAsync(
                "http://keycloak:8080/realms/flight-booking/protocol/openid-connect/token",
                new FormUrlEncodedContent(tokenRequest));

            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Token obtained successfully for user: {Email}", request.Email);
                return Content(content, "application/json");
            }

            _logger.LogWarning("Failed to get token for user: {Email}. Status: {Status}, Response: {Content}", 
                request.Email, response.StatusCode, content);
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obtaining token for user: {Email}", request.Email);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

public class LoginRequest
{
    [FromForm(Name = "username")]
    public string Email { get; set; }

    [FromForm(Name = "password")]
    public string Password { get; set; }

    [FromForm(Name = "scope")]
    public string Scope { get; set; } = "openid";

    [FromForm(Name = "grant_type")]
    public string GrantType { get; set; } = "password";

    [FromForm(Name = "clientId")]
    public string ClientId { get; set; }

    [FromForm(Name = "clientSecret")]
    public string ClientSecret { get; set; }
}