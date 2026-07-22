using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess.Abstractions;

public interface IAppDbContext
{
    DbSet<Post> Posts { get; }
    DbSet<Product> Products { get; }
    DbSet<Address> Addresses { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<AccountLifecycleRequest> AccountLifecycleRequests { get; }
    DbSet<PaymentProviderEvent> PaymentProviderEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
