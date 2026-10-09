using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> paymentAttemptBuilder)
    {
        paymentAttemptBuilder.HasKey(paymentAttempt => paymentAttempt.Id);
        paymentAttemptBuilder.Property(paymentAttempt => paymentAttempt.Reference).HasMaxLength(64);
        paymentAttemptBuilder.Property(paymentAttempt => paymentAttempt.CheckoutId).HasMaxLength(100);
        paymentAttemptBuilder.Property(paymentAttempt => paymentAttempt.Status).HasMaxLength(20);
        paymentAttemptBuilder.Property(paymentAttempt => paymentAttempt.RowVersion).IsRowVersion();
        paymentAttemptBuilder.HasIndex(paymentAttempt => paymentAttempt.Reference).IsUnique();
        paymentAttemptBuilder.HasIndex(paymentAttempt => paymentAttempt.CheckoutId).IsUnique().HasFilter("[CheckoutId] IS NOT NULL");
        paymentAttemptBuilder.HasIndex(paymentAttempt => new { paymentAttempt.OrderId, paymentAttempt.CreatedAtUtc });
        paymentAttemptBuilder.HasOne<Order>().WithMany().HasForeignKey(paymentAttempt => paymentAttempt.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
