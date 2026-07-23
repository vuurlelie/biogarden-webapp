using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> cartBuilder)
    {
        cartBuilder.ToTable(
            "Carts",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "CK_Carts_UserId_Or_SessionId",
                    "[UserId] IS NOT NULL OR [SessionId] IS NOT NULL");
            });

        cartBuilder.HasKey(cart => cart.Id);

        cartBuilder.Property(cart => cart.SessionId)
            .HasMaxLength(100);

        cartBuilder.Property(cart => cart.CreatedAtUtc)
            .HasColumnType("datetime2");

        cartBuilder.Property(cart => cart.LastActivityAtUtc)
            .HasColumnType("datetime2");

        cartBuilder.Property(cart => cart.ExpiresAtUtc)
            .HasColumnType("datetime2");

        cartBuilder.Property(cart => cart.CheckedOutAtUtc)
            .HasColumnType("datetime2");

        cartBuilder.Property(cart => cart.RowVersion)
            .IsRowVersion();

        cartBuilder.HasIndex(cart => cart.UserId);
        cartBuilder.HasIndex(cart => cart.SessionId);
        cartBuilder.HasIndex(cart => cart.ExpiresAtUtc);

        cartBuilder.HasMany(cart => cart.Items)
            .WithOne(cartItem => cartItem.Cart)
            .HasForeignKey(cartItem => cartItem.CartId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
