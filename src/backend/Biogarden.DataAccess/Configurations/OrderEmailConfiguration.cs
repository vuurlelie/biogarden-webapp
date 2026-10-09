using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public sealed class OrderEmailConfiguration : IEntityTypeConfiguration<OrderEmail>
{
    public void Configure(EntityTypeBuilder<OrderEmail> orderEmailBuilder)
    {
        orderEmailBuilder.HasKey(orderEmail => orderEmail.Id);
        orderEmailBuilder.Property(orderEmail => orderEmail.Kind).HasMaxLength(20);
        orderEmailBuilder.Property(orderEmail => orderEmail.Recipient).HasMaxLength(320);
        orderEmailBuilder.Property(orderEmail => orderEmail.Subject).HasMaxLength(998);
        orderEmailBuilder.HasIndex(orderEmail => new { orderEmail.OrderId, orderEmail.Kind, orderEmail.EventId }).IsUnique();
        orderEmailBuilder.HasIndex(orderEmail => new { orderEmail.SentAtUtc, orderEmail.NextAttemptAtUtc });
        orderEmailBuilder.HasOne<Order>().WithMany().HasForeignKey(orderEmail => orderEmail.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}
