using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> orderBuilder)
    {
        orderBuilder.ToTable("Orders");

        orderBuilder.HasKey(order => order.Id);

        orderBuilder.Property(order => order.OrderNumber)
            .HasMaxLength(50)
            .IsRequired();

        orderBuilder.HasIndex(order => order.OrderNumber)
            .IsUnique();

        orderBuilder.Property(order => order.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        orderBuilder.Property(order => order.ConfirmedAtUtc)
            .HasColumnType("datetime2");

        orderBuilder.Property(order => order.CancelledAtUtc)
            .HasColumnType("datetime2");

        orderBuilder.Property(order => order.TotalPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        orderBuilder.Property(order => order.Status)
            .HasConversion<byte>()
            .HasDefaultValue(OrderStatus.Pending)
            .IsRequired();

        orderBuilder.Property(order => order.PaymentStatus)
            .HasConversion<byte>()
            .HasDefaultValue(PaymentStatus.Pending)
            .IsRequired();

        orderBuilder.Property(order => order.FulfillmentMethod)
            .HasConversion<byte>()
            .HasDefaultValue(FulfillmentMethod.PersonalPickup)
            .IsRequired();

        orderBuilder.Property(order => order.CancellationReason)
            .HasMaxLength(500);

        orderBuilder.HasIndex(order => order.UserId);
        orderBuilder.HasIndex(order => order.CreatedAtUtc);

        orderBuilder.HasIndex(order => order.CartId)
            .IsUnique();

        orderBuilder.HasMany(order => order.Items)
            .WithOne(orderItem => orderItem.Order)
            .HasForeignKey(orderItem => orderItem.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        orderBuilder.HasOne(order => order.Cart)
            .WithMany()
            .HasForeignKey(order => order.CartId)
            .OnDelete(DeleteBehavior.Restrict);

        orderBuilder.HasOne(order => order.Address)
            .WithMany()
            .HasForeignKey(order => order.AddressId)
            .OnDelete(DeleteBehavior.Restrict);

        orderBuilder.HasOne(order => order.User)
            .WithMany()
            .HasForeignKey(order => order.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
