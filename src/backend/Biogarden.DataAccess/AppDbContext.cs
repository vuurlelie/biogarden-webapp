using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Post>(postBuilder =>
        {
            postBuilder.ToTable("Posts");

            postBuilder.Property(post => post.Title)
                .HasMaxLength(200)
                .IsRequired();

            postBuilder.HasIndex(post => post.Title).IsUnique();

            postBuilder.Property(post => post.Article)
                .IsRequired();

            postBuilder.Property(post => post.Image)
                .HasMaxLength(500);

            postBuilder.Property(post => post.PublishedAt)
                .HasColumnType("datetime2")
                .IsRequired()
                .HasDefaultValueSql("SYSUTCDATETIME()");

            postBuilder.Property(post => post.RecordStatus)
                .HasConversion<byte>();

            postBuilder.HasQueryFilter(post => post.RecordStatus == RecordStatus.Active);

            postBuilder.HasIndex(post => post.PublishedAt);
        });

        modelBuilder.Entity<Product>(productBuilder =>
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

            productBuilder.Property(product => product.PackageSize)
                .HasMaxLength(100);

            productBuilder.Property(product => product.RecordStatus)
                .HasConversion<byte>();

            productBuilder.HasQueryFilter(product => product.RecordStatus == RecordStatus.Active);

            productBuilder.HasIndex(product => product.Name);

            productBuilder.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Products_Price_NonNegative", "[Price] >= 0");
                table.HasCheckConstraint("CK_Products_StockQuantity_NonNegative", "[StockQuantity] >= 0");
            });
        });
    }
}