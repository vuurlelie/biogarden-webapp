using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public sealed class OrderEmailConfiguration : IEntityTypeConfiguration<OrderEmail>
{
    public void Configure(EntityTypeBuilder<OrderEmail> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasMaxLength(20);
        builder.Property(x => x.Recipient).HasMaxLength(320);
        builder.Property(x => x.Subject).HasMaxLength(998);
        builder.HasIndex(x => new { x.OrderId, x.Kind, x.EventId }).IsUnique();
        builder.HasIndex(x => new { x.SentAtUtc, x.NextAttemptAtUtc });
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}
