using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess.Abstractions;

public interface IAppDbContext
{
    DbSet<Post> Posts { get; }
    DbSet<Product> Products { get; }
    DbSet<Address> Addresses { get; }


    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}