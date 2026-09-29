using System.ComponentModel.DataAnnotations;

namespace ChatApp.DTOs.Settings;

public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 10)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class VerifyAuthenticatorDto
{
    [Required]
    [StringLength(10, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
}

public class DisableTwoFactorDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;
}
