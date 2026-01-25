using System.ComponentModel.DataAnnotations;

namespace WorkOps.Api.Contracts.Auth;

public class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, MinLength(1)]
    public string Password { get; set; } = "";

    [Required, Compare(nameof(Password), ErrorMessage = "Password and ConfirmPassword do not match.")]
    public string ConfirmPassword { get; set; } = "";
}
