using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> cartItemBuilder)
    {
        cartItemBuilder.ToTable(
            "CartItems",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("CK_CartItems_Quantity_Positive", "[Quantity] > 0");
            });

        cartItemBuilder.HasKey(cartItem => cartItem.Id);

        cartItemBuilder.Property(cartItem => cartItem.Quantity)
            .IsRequired();

        cartItemBuilder.Property(cartItem => cartItem.CreatedAtUtc)
            .HasColumnType("datetime2");

        cartItemBuilder.Property(cartItem => cartItem.UpdatedAtUtc)
            .HasColumnType("datetime2");

        cartItemBuilder.HasIndex(cartItem => new { cartItem.CartId, cartItem.ProductId })
            .IsUnique();

        cartItemBuilder.HasIndex(cartItem => cartItem.ProductId);

        cartItemBuilder.HasOne(cartItem => cartItem.Product)
            .WithMany()
            .HasForeignKey(cartItem => cartItem.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
