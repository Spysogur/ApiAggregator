using System.Security.Claims;

namespace ApiAggregator.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("1. Authentication")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;
    public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("Login")] 
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<TokenResponse> Login([FromBody] LoginRequest request)
    {
        var username = _configuration["Auth:Username"];
        var password = _configuration["Auth:Password"];
        // In a real application, validate the user credentials here
        if (request.Username == username && request.Password == password)
        {
            var token = GenerateJwtToken(request.Username);
            _logger.LogInformation("User {Username} logged in successfully", request.Username);
            return Ok(new TokenResponse { Token = token, ExpiresIn = 3600 });
        }
        _logger.LogWarning("Failed login attempt for user {Username}", request.Username);

        return new UnauthorizedResult();
    }
    [HttpGet("ValidateToken")]
    [Authorize]
    [ProducesResponseType(typeof(TokenValidationResponse),StatusCodes.Status200OK)]
    public ActionResult<TokenValidationResponse> ValidateToken()
    {
        var username = User.Identity?.Name;
        var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();

        _logger.LogInformation("✅ Token validated for user {Username}", username);

        return Ok(new TokenValidationResponse
        {
            Valid = true,
            Username = username ?? "Unknown",
            Message = "Your token is valid and you are authenticated!",
            AuthenticatedAt = DateTime.UtcNow,
            Claims = claims
        });
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(AuthStatusResponse), StatusCodes.Status200OK)]
    public ActionResult<AuthStatusResponse> GetStatus()
    {
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
        var username = User.Identity?.Name;

        return Ok(new AuthStatusResponse
        {
            IsAuthenticated = isAuthenticated,
            Username = username,
            Message = isAuthenticated
                ? $"Authenticated as {username}"
                : "Not authenticated - Please login and authorize first"
        });
    }
    private string GenerateJwtToken(string username)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is missing in configuration.");
        var key = Encoding.ASCII.GetBytes(secretKey);

        var expires = DateTime.UtcNow.AddHours(1);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] 
            {
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, username)
            }),

            Expires = expires,
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
               SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class TokenResponse
{
    public string Token { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}
