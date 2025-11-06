// UserContext.cs
using System.Security.Claims;

namespace GatewayService.Services
{
    public interface IUserContext
    {
        string? GetUsername();
    }

    public class UserContext : IUserContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? GetUsername()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                   ?? _httpContextAccessor.HttpContext?.User?.FindFirst("preferred_username")?.Value 
                   ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
        }
    }
}