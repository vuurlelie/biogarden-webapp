using System.Collections;
using System.Linq.Expressions;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class AdminUserServiceTests
{
    private readonly Mock<IAppDbContext> _databaseContext = new(MockBehavior.Strict);
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly AdminUserService _service;

    public AdminUserServiceTests()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>(MockBehavior.Strict);
        _userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _service = new AdminUserService(_databaseContext.Object, _userManager.Object);
    }

    [Fact]
    public async Task SetAdminRoleAsync_WhenCurrentAdminRemovesOwnRole_ThrowsBeforeUserLookup()
    {
        var currentAdminId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SetAdminRoleAsync(currentAdminId, isAdmin: false, currentAdminId));

        Assert.Equal("A saját admin jogosultságodat nem veheted el.", exception.Message);
        _databaseContext.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeactivateAsync_WhenCurrentAdminTargetsSelf_ThrowsBeforeUserLookup()
    {
        var currentAdminId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeactivateAsync(currentAdminId, currentAdminId));

        Assert.Equal("A saját fiókodat nem deaktiválhatod az admin felületről.", exception.Message);
        _databaseContext.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ConfirmEmailAsync_WhenUserIsAnonymized_ThrowsBeforeUpdate()
    {
        var userId = Guid.NewGuid();
        var users = CreateMockDbSet([
            new ApplicationUser
            {
                Id = userId,
                EmailConfirmed = false,
                AnonymizedAtUtc = DateTime.UtcNow
            }
        ]);

        _userManager
            .SetupGet(manager => manager.Users)
            .Returns(users.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmEmailAsync(userId));

        Assert.Equal("Törölt vagy anonimizált fiók email címe nem erősíthető meg.", exception.Message);
        _userManager.Verify(manager => manager.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task EraseAsync_WhenCurrentAdminTargetsSelf_ThrowsBeforeUserLookup()
    {
        var currentAdminId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.EraseAsync(currentAdminId, currentAdminId));

        Assert.Equal("A saját fiókodat nem törölheted az admin felületről.", exception.Message);
        _databaseContext.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CompleteErasureRequestAsync_WhenRequestBelongsToCurrentAdmin_ThrowsBeforeAnonymizing()
    {
        var currentAdminId = Guid.NewGuid();
        var requests = CreateMockDbSet([
            new AccountLifecycleRequest
            {
                Id = 12,
                UserId = currentAdminId,
                User = new ApplicationUser { Id = currentAdminId },
                Type = AccountLifecycleRequestType.Erasure,
                Status = AccountLifecycleRequestStatus.Pending
            }
        ]);

        _databaseContext
            .SetupGet(context => context.AccountLifecycleRequests)
            .Returns(requests.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CompleteErasureRequestAsync(
                12,
                currentAdminId,
                new()));

        Assert.Equal("A saját fiókodat nem törölheted vagy anonimizálhatod az admin felületről.", exception.Message);
        _databaseContext.VerifyGet(context => context.AccountLifecycleRequests, Times.Once);
        _databaseContext.Verify(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _userManager.Verify(manager => manager.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    private static Mock<DbSet<TEntity>> CreateMockDbSet<TEntity>(IEnumerable<TEntity> entities)
        where TEntity : class
    {
        var queryable = entities.AsQueryable();
        var mockSet = new Mock<DbSet<TEntity>>();

        mockSet.As<IAsyncEnumerable<TEntity>>()
            .Setup(set => set.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) => new TestAsyncEnumerator<TEntity>(queryable.GetEnumerator()));

        mockSet.As<IQueryable<TEntity>>()
            .Setup(set => set.Provider)
            .Returns(new TestAsyncQueryProvider<TEntity>(queryable.Provider));
        mockSet.As<IQueryable<TEntity>>().Setup(set => set.Expression).Returns(queryable.Expression);
        mockSet.As<IQueryable<TEntity>>().Setup(set => set.ElementType).Returns(queryable.ElementType);
        mockSet.As<IQueryable<TEntity>>().Setup(set => set.GetEnumerator()).Returns(() => queryable.GetEnumerator());

        return mockSet;
    }

    private sealed class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        public TestAsyncQueryProvider(IQueryProvider inner)
        {
            _inner = inner;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            return new TestAsyncEnumerable<TEntity>(expression);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new TestAsyncEnumerable<TElement>(expression);
        }

        public object? Execute(Expression expression)
        {
            return _inner.Execute(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return _inner.Execute<TResult>(expression);
        }

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var expectedResultType = typeof(TResult).GetGenericArguments()[0];
            var executionResult = typeof(IQueryProvider)
                .GetMethod(
                    nameof(IQueryProvider.Execute),
                    1,
                    [typeof(Expression)])
                ?.MakeGenericMethod(expectedResultType)
                .Invoke(this, [expression]);

            return (TResult)typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(expectedResultType)
                .Invoke(null, [executionResult])!;
        }
    }

    private sealed class TestAsyncEnumerable<TEntity> :
        EnumerableQuery<TEntity>,
        IAsyncEnumerable<TEntity>,
        IQueryable<TEntity>
    {
        public TestAsyncEnumerable(IEnumerable<TEntity> enumerable)
            : base(enumerable)
        {
        }

        public TestAsyncEnumerable(Expression expression)
            : base(expression)
        {
        }

        public IAsyncEnumerator<TEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new TestAsyncEnumerator<TEntity>(this.AsEnumerable().GetEnumerator());
        }

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<TEntity>(this);
    }

    private sealed class TestAsyncEnumerator<TEntity> : IAsyncEnumerator<TEntity>
    {
        private readonly IEnumerator<TEntity> _inner;

        public TestAsyncEnumerator(IEnumerator<TEntity> inner)
        {
            _inner = inner;
        }

        public TEntity Current => _inner.Current;

        public ValueTask DisposeAsync()
        {
            _inner.Dispose();
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return ValueTask.FromResult(_inner.MoveNext());
        }
    }
}
