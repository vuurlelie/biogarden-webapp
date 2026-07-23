using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> addressBuilder)
    {
        addressBuilder.ToTable("Addresses");

        addressBuilder.Property(address => address.PostalCode)
            .HasMaxLength(4)
            .IsRequired();

        addressBuilder.Property(address => address.City)
            .HasMaxLength(100)
            .IsRequired();

        addressBuilder.Property(address => address.Street)
            .HasMaxLength(200)
            .IsRequired();

        addressBuilder.Property(address => address.HouseNumber)
            .HasMaxLength(20)
            .IsRequired();

        addressBuilder.Property(address => address.Building)
            .HasMaxLength(20);

        addressBuilder.Property(address => address.Floor)
            .HasMaxLength(20);

        addressBuilder.Property(address => address.Door)
            .HasMaxLength(20);

        addressBuilder.Property(address => address.Doorbell)
            .HasMaxLength(50);
    }
}
