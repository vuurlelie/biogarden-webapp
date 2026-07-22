using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public sealed class OrderWithdrawalRequestConfiguration : IEntityTypeConfiguration<OrderWithdrawalRequest>
{
    public void Configure(EntityTypeBuilder<OrderWithdrawalRequest> requestBuilder)
    {
        requestBuilder.ToTable("OrderWithdrawalRequests");
        requestBuilder.HasKey(request => request.Id);
        requestBuilder.Property(request => request.CustomerName).HasMaxLength(200).IsRequired();
        requestBuilder.Property(request => request.CustomerEmail).HasMaxLength(256).IsRequired();
        requestBuilder.Property(request => request.Reason).HasMaxLength(1000).IsRequired();
        requestBuilder.Property(request => request.Status)
            .HasConversion<byte>()
            .HasDefaultValue(OrderWithdrawalRequestStatus.Pending)
            .IsRequired();
        requestBuilder.Property(request => request.RequestedAtUtc)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();
        requestBuilder.Property(request => request.HandledAtUtc).HasColumnType("datetime2");
        requestBuilder.HasIndex(request => new { request.OrderId, request.Status });
        requestBuilder.HasOne(request => request.Order)
            .WithMany(order => order.WithdrawalRequests)
            .HasForeignKey(request => request.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
