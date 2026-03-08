using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public class ApplicationUserConfiguration
    : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> userBuilder)
    {
        userBuilder.Property(user => user.FirstName)
            .HasMaxLength(100);

        userBuilder.Property(user => user.LastName)
            .HasMaxLength(100);

        userBuilder.Property(user => user.RecordStatus)
            .HasConversion<byte>();

        userBuilder.HasOne(user => user.ShippingAddress)
            .WithMany()
            .HasForeignKey(user => user.ShippingAddressId)
            .OnDelete(DeleteBehavior.Restrict);

        userBuilder.HasOne(user => user.BillingAddress)
            .WithMany()
            .HasForeignKey(user => user.BillingAddressId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}