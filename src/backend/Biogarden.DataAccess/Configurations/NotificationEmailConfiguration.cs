using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public sealed class NotificationEmailConfiguration : IEntityTypeConfiguration<NotificationEmail>
{
    public void Configure(EntityTypeBuilder<NotificationEmail> notificationEmailBuilder)
    {
        notificationEmailBuilder.HasKey(notificationEmail => notificationEmail.Id);
        notificationEmailBuilder.Property(notificationEmail => notificationEmail.Kind).HasMaxLength(40);
        notificationEmailBuilder.Property(notificationEmail => notificationEmail.RequestHash).HasMaxLength(64);
        notificationEmailBuilder.Property(notificationEmail => notificationEmail.SecurityStamp).HasMaxLength(256);
        notificationEmailBuilder.Property(notificationEmail => notificationEmail.Recipient).HasMaxLength(320);
        notificationEmailBuilder.Property(notificationEmail => notificationEmail.Subject).HasMaxLength(998);
        notificationEmailBuilder.Property(notificationEmail => notificationEmail.Outcome).HasMaxLength(20);
        notificationEmailBuilder.HasIndex(notificationEmail => new { notificationEmail.EventId, notificationEmail.Kind }).IsUnique();
        notificationEmailBuilder.HasIndex(notificationEmail => new { notificationEmail.CompletedAtUtc, notificationEmail.NextAttemptAtUtc });
        // No user FK: the final deletion notification must survive deletion of the account.
    }
}
