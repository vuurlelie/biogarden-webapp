using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reference).HasMaxLength(64);
        builder.Property(x => x.CheckoutId).HasMaxLength(100);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.CheckoutId).IsUnique().HasFilter("[CheckoutId] IS NOT NULL");
        builder.HasIndex(x => new { x.OrderId, x.CreatedAtUtc });
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
