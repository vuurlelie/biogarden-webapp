using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> orderItemBuilder)
    {
        orderItemBuilder.ToTable(
            "OrderItems",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "CK_OrderItems_Quantity_Positive",
                    "[Quantity] > 0");

                tableBuilder.HasCheckConstraint(
                    "CK_OrderItems_UnitPrice_NonNegative",
                    "[UnitPrice] >= 0");

                tableBuilder.HasCheckConstraint(
                    "CK_OrderItems_LineTotal_NonNegative",
                    "[LineTotal] >= 0");
            });

        orderItemBuilder.HasKey(orderItem => orderItem.Id);

        orderItemBuilder.Property(orderItem => orderItem.ProductName)
            .HasMaxLength(200)
            .IsRequired();

        orderItemBuilder.Property(orderItem => orderItem.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        orderItemBuilder.Property(orderItem => orderItem.Unit)
            .HasConversion<byte>()
            .IsRequired();

        orderItemBuilder.Property(orderItem => orderItem.Quantity)
            .IsRequired();

        orderItemBuilder.Property(orderItem => orderItem.LineTotal)
            .HasPrecision(18, 2)
            .IsRequired();

        orderItemBuilder.HasIndex(orderItem => orderItem.OrderId);

        orderItemBuilder.HasIndex(orderItem => orderItem.ProductId);

        orderItemBuilder.HasOne(orderItem => orderItem.Product)
            .WithMany()
            .HasForeignKey(orderItem => orderItem.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
