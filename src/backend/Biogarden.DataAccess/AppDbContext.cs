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
    public DbSet<Address> Addresses => Set<Address>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Post>(post =>
        {
            post.ToTable("Posts");

            post.Property(post => post.Title)
                .HasMaxLength(200)
                .IsRequired();

            post.HasIndex(post => post.Title).IsUnique();

            post.Property(post => post.Article)
                .IsRequired();

            post.Property(post => post.Image)
                .HasMaxLength(500);

            post.Property(post => post.PublishedAt)
                .HasColumnType("datetime2")
                .IsRequired()
                .HasDefaultValueSql("SYSUTCDATETIME()");

            post.Property(post => post.RecordStatus)
                .HasConversion<byte>();

            post.HasQueryFilter(post => post.RecordStatus == RecordStatus.Active);

            post.HasIndex(post => post.PublishedAt);
        });

        modelBuilder.Entity<Address>(addressBuilder =>
        {
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
        });
    }
}