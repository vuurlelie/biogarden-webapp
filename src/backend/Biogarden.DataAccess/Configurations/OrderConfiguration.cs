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
        orderBuilder.Property(order => order.Subtotal).HasPrecision(18, 2);
        orderBuilder.Property(order => order.DeliveryFee).HasPrecision(18, 2);
        orderBuilder.Property(order => order.PaymentFee).HasPrecision(18, 2);
        orderBuilder.Property(order => order.Currency).HasMaxLength(3);
        orderBuilder.Property(order => order.GlsDeliveryPointId).HasMaxLength(100);
        orderBuilder.Property(order => order.GlsDeliveryPointName).HasMaxLength(200);
        orderBuilder.Property(order => order.GlsDeliveryPointCountryCode).HasMaxLength(2);
        orderBuilder.Property(order => order.GlsDeliveryPointAddress).HasMaxLength(300);
        orderBuilder.Property(order => order.RowVersion).IsRowVersion();

        orderBuilder.Property(order => order.OrderNumber)
            .HasMaxLength(50)
            .IsRequired();

        orderBuilder.HasIndex(order => order.OrderNumber)
            .IsUnique();

        orderBuilder.Property(order => order.IdempotencyKey)
            .HasMaxLength(100);

        orderBuilder.HasIndex(order => order.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        orderBuilder.Property(order => order.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        orderBuilder.Property(order => order.ConfirmedAtUtc)
            .HasColumnType("datetime2");

        orderBuilder.Property(order => order.CancelledAtUtc)
            .HasColumnType("datetime2");

        orderBuilder.Property(order => order.DeletedAtUtc)
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

        orderBuilder.Property(order => order.SumUpCheckoutId)
            .HasMaxLength(200);

        orderBuilder.Property(order => order.SumUpTransactionId)
            .HasMaxLength(200);

        orderBuilder.HasIndex(order => order.SumUpCheckoutId)
            .IsUnique()
            .HasFilter("[SumUpCheckoutId] IS NOT NULL");

        orderBuilder.HasIndex(order => order.SumUpTransactionId)
            .IsUnique()
            .HasFilter("[SumUpTransactionId] IS NOT NULL");

        orderBuilder.Property(order => order.CancellationReason)
            .HasMaxLength(500);

        orderBuilder.Property(order => order.CustomerName).HasMaxLength(200).IsRequired();
        orderBuilder.Property(order => order.CustomerEmail).HasMaxLength(256).IsRequired();
        orderBuilder.Property(order => order.CustomerPhone).HasMaxLength(50).IsRequired();
        orderBuilder.Property(order => order.PostalCode).HasMaxLength(20).IsRequired();
        orderBuilder.Property(order => order.City).HasMaxLength(100).IsRequired();
        orderBuilder.Property(order => order.Street).HasMaxLength(200).IsRequired();
        orderBuilder.Property(order => order.HouseNumber).HasMaxLength(50).IsRequired();
        orderBuilder.Property(order => order.Building).HasMaxLength(50);
        orderBuilder.Property(order => order.Floor).HasMaxLength(50);
        orderBuilder.Property(order => order.Door).HasMaxLength(50);
        orderBuilder.Property(order => order.Doorbell).HasMaxLength(50);
        orderBuilder.Property(order => order.BillingCompanyName).HasMaxLength(200);
        orderBuilder.Property(order => order.BillingTaxNumber).HasMaxLength(20);
        orderBuilder.Property(order => order.CustomerType)
            .HasConversion<byte>()
            .HasDefaultValue(CustomerType.Private)
            .IsRequired();
        orderBuilder.Property(order => order.PrintedInvoiceRequested)
            .HasDefaultValue(false)
            .IsRequired();
        orderBuilder.Property(order => order.AcceptedTermsVersion)
            .HasMaxLength(50)
            .IsRequired();
        orderBuilder.Property(order => order.AcceptedPrivacyPolicyVersion)
            .HasMaxLength(50)
            .IsRequired();
        orderBuilder.Property(order => order.DeliveryPostalCode).HasMaxLength(20);
        orderBuilder.Property(order => order.DeliveryCity).HasMaxLength(100);
        orderBuilder.Property(order => order.DeliveryStreet).HasMaxLength(200);
        orderBuilder.Property(order => order.DeliveryHouseNumber).HasMaxLength(50);
        orderBuilder.Property(order => order.DeliveryBuilding).HasMaxLength(50);
        orderBuilder.Property(order => order.DeliveryFloor).HasMaxLength(50);
        orderBuilder.Property(order => order.DeliveryDoor).HasMaxLength(50);
        orderBuilder.Property(order => order.DeliveryDoorbell).HasMaxLength(50);

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
