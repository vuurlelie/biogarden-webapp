namespace Biogarden.DataAccess.Abstractions;

public interface IUnitOfWork
{
    IPostRepository PostRepository { get; }
    IProductRepository ProductRepository { get; }
    IAddressRepository AddressRepository { get; }
    ICartRepository CartRepository { get; }
    IOrderRepository OrderRepository { get; }

    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
