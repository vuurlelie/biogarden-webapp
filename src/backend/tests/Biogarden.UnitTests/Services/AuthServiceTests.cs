using System.Collections;
using System.Linq.Expressions;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.BusinessLogic.Models.Email;
using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Options;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class AuthServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly Mock<ITokenService> _tokenService = new(MockBehavior.Strict);
    private readonly Mock<IEmailService> _emailService = new(MockBehavior.Strict);
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>(MockBehavior.Strict);
        _userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _userManager
            .SetupGet(manager => manager.Users)
            .Returns(CreateAsyncQueryable(Array.Empty<ApplicationUser>()));

        _service = new AuthService(
            _userManager.Object,
            _tokenService.Object,
            _emailService.Object,
            Options.Create(new AuthEmailOptions
            {
                ClientBaseUrl = "https://bakonykincse.hu",
                ConfirmEmailPath = "/email-megerosites",
                UnconfirmedAccountRetentionDays = 14
            }));
    }

    [Fact]
    public async Task RegisterAsync_WhenRegistrationSucceeds_SendsConfirmationEmail()
    {
        _userManager
            .Setup(manager => manager.FindByEmailAsync("vasarlo@example.com"))
            .ReturnsAsync((ApplicationUser?)null);
        _userManager
            .Setup(manager => manager.CreateAsync(
                It.Is<ApplicationUser>(user =>
                    user.Email == "vasarlo@example.com"
                    && user.UserName == "vasarlo@example.com"
                    && !user.EmailConfirmed),
                "Password1"))
            .ReturnsAsync(IdentityResult.Success);
        _userManager
            .Setup(manager => manager.GenerateEmailConfirmationTokenAsync(
                It.Is<ApplicationUser>(user => user.Email == "vasarlo@example.com")))
            .ReturnsAsync("token value");
        _emailService
            .Setup(service => service.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.To == "vasarlo@example.com"
                    && message.Subject.Contains("Email cím megerősítése")
                    && message.PlainTextBody!.Contains("https://bakonykincse.hu/email-megerosites")
                    && message.PlainTextBody.Contains("token%20value")),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.RegisterAsync(new RegisterRequestDto
        {
            Email = " vasarlo@example.com ",
            Password = "Password1",
            ConfirmPassword = "Password1"
        });

        _emailService.VerifyAll();
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsValidButEmailIsUnconfirmed_ThrowsBeforeTokenCreation()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "vasarlo@example.com",
            EmailConfirmed = false
        };

        _userManager
            .Setup(manager => manager.FindByEmailAsync("vasarlo@example.com"))
            .ReturnsAsync(user);
        _userManager
            .Setup(manager => manager.CheckPasswordAsync(user, "Password1"))
            .ReturnsAsync(true);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.LoginAsync(new LoginRequestDto
            {
                Email = "vasarlo@example.com",
                Password = "Password1"
            }));

        Assert.Equal(
            "A fiók még nincs megerősítve. Kérjük, ellenőrizd az email fiókodat.",
            exception.Message);
        _tokenService.Verify(
            service => service.CreateTokenAsync(It.IsAny<ApplicationUser>()),
            Times.Never);
    }

    [Fact]
    public async Task ResendEmailConfirmationAsync_WhenUserDoesNotExist_ReturnsNeutralSuccess()
    {
        _userManager
            .Setup(manager => manager.FindByEmailAsync("missing@example.com"))
            .ReturnsAsync((ApplicationUser?)null);

        await _service.ResendEmailConfirmationAsync(
            new ResendEmailConfirmationRequestDto { Email = "missing@example.com" });

        _emailService.Verify(
            service => service.SendAsync(
                It.IsAny<EmailMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static IQueryable<TEntity> CreateAsyncQueryable<TEntity>(IEnumerable<TEntity> entities)
    {
        return new TestAsyncEnumerable<TEntity>(entities);
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

        public TResult ExecuteAsync<TResult>(
            Expression expression,
            CancellationToken cancellationToken = default)
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

        public IAsyncEnumerator<TEntity> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
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
