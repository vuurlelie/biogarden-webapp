using Biogarden.DataAccess.Abstractions;

namespace Biogarden.DataAccess.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly IAppDbContext _databaseContext;

    private IPostRepository? _postRepository;

    public UnitOfWork(IAppDbContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public IPostRepository PostRepository
        => _postRepository ??= new PostRepository(_databaseContext);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _databaseContext.SaveChangesAsync(cancellationToken);
}