using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public class AccountLifecycleRequestConfiguration : IEntityTypeConfiguration<AccountLifecycleRequest>
{
    public void Configure(EntityTypeBuilder<AccountLifecycleRequest> requestBuilder)
    {
        requestBuilder.ToTable("AccountLifecycleRequests");

        requestBuilder.HasKey(request => request.Id);

        requestBuilder.Property(request => request.Type)
            .HasConversion<byte>()
            .IsRequired();

        requestBuilder.Property(request => request.Status)
            .HasConversion<byte>()
            .IsRequired();

        requestBuilder.Property(request => request.Reason)
            .HasMaxLength(1000);

        requestBuilder.Property(request => request.Message)
            .HasMaxLength(1000)
            .IsRequired();

        requestBuilder.Property(request => request.RequestedAtUtc)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        requestBuilder.Property(request => request.HandledAtUtc)
            .HasColumnType("datetime2");

        requestBuilder.Property(request => request.AdminNote)
            .HasMaxLength(1000);

        requestBuilder.HasIndex(request => new { request.UserId, request.Status });
        requestBuilder.HasIndex(request => request.RequestedAtUtc);

        requestBuilder.HasOne(request => request.User)
            .WithMany()
            .HasForeignKey(request => request.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        requestBuilder.HasOne(request => request.HandledByAdmin)
            .WithMany()
            .HasForeignKey(request => request.HandledByAdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
