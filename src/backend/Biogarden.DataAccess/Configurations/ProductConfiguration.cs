using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> productBuilder)
    {
        productBuilder.ToTable("Products");

        productBuilder.Property(product => product.Name)
            .HasMaxLength(200)
            .IsRequired();

        productBuilder.Property(product => product.Description)
            .HasMaxLength(1000);

        productBuilder.Property(product => product.Image)
            .HasMaxLength(500);

        productBuilder.Property(product => product.Price)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        productBuilder.Property(product => product.UnitAmount)
            .HasPrecision(18, 2);

        productBuilder.Property(product => product.RecordStatus)
            .HasConversion<byte>();

        productBuilder.Property(product => product.RowVersion)
            .IsRowVersion();

        productBuilder.HasIndex(product => product.Name);

        productBuilder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Products_Price_NonNegative",
                "[Price] >= 0");

            table.HasCheckConstraint(
                "CK_Products_StockQuantity_NonNegative",
                "[StockQuantity] >= 0");
        });
    }
}
