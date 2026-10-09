using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biogarden.DataAccess.Configurations;

public sealed class BrowserSessionConfiguration : IEntityTypeConfiguration<BrowserSession>
{
    public void Configure(EntityTypeBuilder<BrowserSession> browserSessionBuilder)
    {
        browserSessionBuilder.HasKey(browserSession => browserSession.KeyHash);
        browserSessionBuilder.Property(browserSession => browserSession.KeyHash).HasMaxLength(64).IsUnicode(false);
        browserSessionBuilder.HasIndex(browserSession => browserSession.ExpiresAtUtc);
    }
}
