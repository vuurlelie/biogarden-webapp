namespace Biogarden.DataAccess.Abstractions;

public interface IUnitOfWork
{
    IPostRepository PostRepository { get; }
    IProductRepository ProductRepository { get; }
    IAddressRepository Addresses { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}