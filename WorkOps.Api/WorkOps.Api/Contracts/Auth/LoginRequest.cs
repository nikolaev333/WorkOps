using System.ComponentModel.DataAnnotations;

namespace WorkOps.Api.Contracts.Auth;

public class LoginRequest
{
    [Required]
    public string Email { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}
