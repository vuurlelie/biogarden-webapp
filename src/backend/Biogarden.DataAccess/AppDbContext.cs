using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess;

public class AppDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>,
      IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderPaymentCorrection> OrderPaymentCorrections => Set<OrderPaymentCorrection>();
    public DbSet<OrderEmail> OrderEmails => Set<OrderEmail>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<AccountLifecycleRequest> AccountLifecycleRequests => Set<AccountLifecycleRequest>();
    public DbSet<PaymentProviderEvent> PaymentProviderEvents => Set<PaymentProviderEvent>();
    public DbSet<OrderWithdrawalRequest> OrderWithdrawalRequests => Set<OrderWithdrawalRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}
