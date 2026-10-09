using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public sealed class OrderPaymentCorrectionConfiguration : IEntityTypeConfiguration<OrderPaymentCorrection>
{
    public void Configure(EntityTypeBuilder<OrderPaymentCorrection> orderPaymentCorrectionBuilder)
    {
        orderPaymentCorrectionBuilder.HasKey(orderPaymentCorrection => orderPaymentCorrection.Id);
        orderPaymentCorrectionBuilder.Property(orderPaymentCorrection => orderPaymentCorrection.Reason).HasMaxLength(500).IsRequired();
        orderPaymentCorrectionBuilder.HasIndex(orderPaymentCorrection => new { orderPaymentCorrection.OrderId, orderPaymentCorrection.CreatedAtUtc });
        orderPaymentCorrectionBuilder.HasOne(orderPaymentCorrection => orderPaymentCorrection.Order).WithMany().HasForeignKey(orderPaymentCorrection => orderPaymentCorrection.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
