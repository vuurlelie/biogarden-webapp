namespace Biogarden.BusinessLogic.DTOs.Contact;

public sealed class ContactMessageRequestDto
{
    public Guid SubmissionId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Subject { get; set; }

    public string Message { get; set; } = string.Empty;
}
