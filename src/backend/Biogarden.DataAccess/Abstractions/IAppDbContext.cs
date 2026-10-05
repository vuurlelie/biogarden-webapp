using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess.Abstractions;

public interface IAppDbContext
{
    DbSet<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>> UserRoles { get; }
    DbSet<Microsoft.AspNetCore.Identity.IdentityRole<Guid>> Roles { get; }
    DbSet<Post> Posts { get; }
    DbSet<Product> Products { get; }
    DbSet<Address> Addresses { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Order> Orders { get; }
    DbSet<PaymentAttempt> PaymentAttempts { get; }
    DbSet<OrderPaymentCorrection> OrderPaymentCorrections { get; }
    DbSet<OrderEmail> OrderEmails { get; }
    DbSet<NotificationEmail> NotificationEmails { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<AccountLifecycleRequest> AccountLifecycleRequests { get; }
    DbSet<OrderWithdrawalRequest> OrderWithdrawalRequests { get; }
    DbSet<PaymentProviderEvent> PaymentProviderEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
