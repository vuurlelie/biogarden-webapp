using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;

namespace Biogarden.DataAccess.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public RecordStatus RecordStatus { get; set; } = RecordStatus.Active;
}