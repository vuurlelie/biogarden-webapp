using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public sealed class BrowserSessionConfiguration : IEntityTypeConfiguration<BrowserSession>
{
    public void Configure(EntityTypeBuilder<BrowserSession> builder)
    {
        builder.HasKey(x => x.KeyHash);
        builder.Property(x => x.KeyHash).HasMaxLength(64).IsUnicode(false);
        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}
