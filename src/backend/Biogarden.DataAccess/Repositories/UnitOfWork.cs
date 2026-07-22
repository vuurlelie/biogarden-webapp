using Biogarden.DataAccess.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly IAppDbContext _databaseContext;

    private IPostRepository? _postRepository;
    private IProductRepository? _productRepository;
    private IAddressRepository? _addressRepository;
    private ICartRepository? _cartRepository;
    private IOrderRepository? _orderRepository;


    public UnitOfWork(IAppDbContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public IPostRepository PostRepository
        => _postRepository ??= new PostRepository(_databaseContext);

    public IProductRepository ProductRepository
        => _productRepository ??= new ProductRepository(_databaseContext);
    public IAddressRepository AddressRepository
    => _addressRepository ??= new AddressRepository(_databaseContext);
    public ICartRepository CartRepository =>
        _cartRepository ??= new CartRepository(_databaseContext);
    public IOrderRepository OrderRepository =>
        _orderRepository ??= new OrderRepository(_databaseContext);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        if (_databaseContext is not DbContext dbContext)
        {
            return await operation(cancellationToken);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return result;
        });
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _databaseContext.SaveChangesAsync(cancellationToken);
}
