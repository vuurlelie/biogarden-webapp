namespace Biogarden.DataAccess.Abstractions;

public interface IUnitOfWork
{
    IPostRepository PostRepository { get; }
    IProductRepository ProductRepository { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}