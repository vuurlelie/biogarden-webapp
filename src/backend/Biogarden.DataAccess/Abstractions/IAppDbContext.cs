using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess.Abstractions;

public interface IAppDbContext
{
    DbSet<Post> Posts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}