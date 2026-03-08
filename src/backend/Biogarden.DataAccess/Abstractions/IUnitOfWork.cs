namespace Biogarden.DataAccess.Abstractions;

public interface IUnitOfWork
{
    IPostRepository PostRepository { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}