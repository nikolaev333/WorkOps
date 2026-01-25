using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WorkOps.Api.Contracts.Auth;
using WorkOps.Api.Options;

namespace WorkOps.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly JwtSettings _jwt;

    public AuthController(UserManager<IdentityUser> userManager, IOptions<JwtSettings> jwt)
    {
        _userManager = userManager;
        _jwt = jwt.Value;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = new IdentityUser { UserName = req.Email, Email = req.Email };
        var result = await _userManager.CreateAsync(user, req.Password);

        if (!result.Succeeded)
        {
            var isDuplicate = result.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail");
            var msg = result.Errors.FirstOrDefault()?.Description ?? "Registration failed.";
            if (isDuplicate)
                return Conflict(new ProblemDetails { Title = "Registration failed", Status = 409, Detail = "An account with this email already exists." });
            return BadRequest(new ProblemDetails { Title = "Registration failed", Status = 400, Detail = msg });
        }

        return CreatedAtAction(nameof(Me), (object?)null, new RegisterResponse { UserId = user.Id, Email = user.Email! });
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, req.Password))
            return new UnauthorizedObjectResult(new ProblemDetails { Title = "Invalid credentials", Status = 401, Detail = "Invalid email or password." });

        var expires = DateTime.UtcNow.AddMinutes(_jwt.ExpiresMinutes);
        var token = CreateToken(user.Id, user.Email!, expires);

        return Ok(new LoginResponse { AccessToken = token, ExpiresAtUtc = expires });
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(sub) || string.IsNullOrEmpty(email))
            return new UnauthorizedObjectResult(new ProblemDetails { Title = "Unauthorized", Status = 401, Detail = "Invalid or missing token." });

        return Ok(new MeResponse { UserId = sub, Email = email });
    }

    private string CreateToken(string userId, string email, DateTime expires)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, email)
        };

        var token = new JwtSecurityToken(
            _jwt.Issuer,
            _jwt.Audience,
            claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
