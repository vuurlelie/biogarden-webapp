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

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _databaseContext.SaveChangesAsync(cancellationToken);
}