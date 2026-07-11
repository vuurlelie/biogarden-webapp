using System.ComponentModel.DataAnnotations;

namespace Biogarden.BusinessLogic.DTOs.Contact;

public sealed class ContactMessageRequestDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(40)]
    public string? Phone { get; set; }

    [StringLength(150)]
    public string? Subject { get; set; }

    [Required]
    [StringLength(2000, MinimumLength = 10)]
    public string Message { get; set; } = string.Empty;
}
