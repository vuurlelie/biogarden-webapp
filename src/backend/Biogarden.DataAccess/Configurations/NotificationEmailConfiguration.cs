using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public sealed class NotificationEmailConfiguration : IEntityTypeConfiguration<NotificationEmail>
{
    public void Configure(EntityTypeBuilder<NotificationEmail> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasMaxLength(40);
        builder.Property(x => x.RequestHash).HasMaxLength(64);
        builder.Property(x => x.SecurityStamp).HasMaxLength(256);
        builder.Property(x => x.Recipient).HasMaxLength(320);
        builder.Property(x => x.Subject).HasMaxLength(998);
        builder.Property(x => x.Outcome).HasMaxLength(20);
        builder.HasIndex(x => new { x.EventId, x.Kind }).IsUnique();
        builder.HasIndex(x => new { x.CompletedAtUtc, x.NextAttemptAtUtc });
        // No user FK: the final deletion notification must survive deletion of the account.
    }
}
