using System.Collections;
using System.Linq.Expressions;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Addresses;
using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.BusinessLogic.Models.Email;
using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class AuthServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly Mock<ITokenService> _tokenService = new(MockBehavior.Strict);
    private readonly Mock<IAccountNotificationQueue> _emailService = new(MockBehavior.Strict);
    private readonly Mock<IEmailTemplateService> _emailTemplateService = new(MockBehavior.Strict);
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>(MockBehavior.Strict);
        _userManager = CreateUserManagerMock(userStore.Object);

        _userManager
            .SetupGet(manager => manager.Users)
            .Returns(CreateAsyncQueryable(Array.Empty<ApplicationUser>()));

        _service = new AuthService(
            _userManager.Object,
            _tokenService.Object,
            _emailService.Object,
            _emailTemplateService.Object,
            Options.Create(new AuthEmailOptions
            {
                ClientBaseUrl = "https://bakonykincse.hu",
                ConfirmEmailPath = "/email-megerosites",
                ResetPasswordPath = "/jelszo-visszaallitas",
                UnconfirmedAccountRetentionDays = 14
            }), Mock.Of<Biogarden.DataAccess.Abstractions.IAppDbContext>());
    }

    [Fact]
    public async Task RegisterAsync_WhenRegistrationSucceeds_QueuesConfirmationEmail()
    {
        _userManager
            .Setup(manager => manager.FindByEmailAsync("vasarlo@example.com"))
            .ReturnsAsync((ApplicationUser?)null);
        _userManager
            .Setup(manager => manager.CreateAsync(It.Is<ApplicationUser>(user => user.Email == "vasarlo@example.com" && user.UserName == "vasarlo@example.com" && !user.EmailConfirmed), "Password1"))
            .ReturnsAsync(IdentityResult.Success);
        _emailService
            .Setup(service => service.QueueAuthenticationAsync(It.Is<ApplicationUser>(user => user.Email == "vasarlo@example.com"), AccountNotificationQueue.Confirmation, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.RegisterAsync(new RegisterRequestDto
        {
            Email = " vasarlo@example.com ",
            Password = "Password1",
            ConfirmPassword = "Password1"
        });

        Assert.True(result.Succeeded);
        _emailService.VerifyAll();
        _emailTemplateService.VerifyAll();
    }

    [Fact]
    public async Task RegisterAsync_ForCompanyAccount_CreatesUserWithBillingAddressAndInvoicePreference()
    {
        ApplicationUser? createdUser = null;

        _userManager
            .Setup(manager => manager.FindByEmailAsync("ceg@example.com"))
            .ReturnsAsync((ApplicationUser?)null);
        _userManager
            .Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), "Password1"))
            .Callback<ApplicationUser, string>((user, _) => createdUser = user)
            .ReturnsAsync(IdentityResult.Success);
        _emailService
            .Setup(service => service.QueueAuthenticationAsync(It.Is<ApplicationUser>(user => user.Email == "ceg@example.com"), AccountNotificationQueue.Confirmation, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.RegisterAsync(new RegisterRequestDto
        {
            Email = " ceg@example.com ",
            Password = "Password1",
            ConfirmPassword = "Password1",
            CustomerType = CustomerType.Company,
            CompanyName = " Teszt Kft. ",
            TaxNumber = "12345678-1-12",
            PrintedInvoiceRequested = true,
            BillingAddress = new AddressCreateDto
            {
                PostalCode = " 2112 ",
                City = " Veresegyhaz ",
                Street = " Szemere Pal utca ",
                HouseNumber = " 25 "
            }
        });

        Assert.True(result.Succeeded);
        Assert.NotNull(createdUser);
        Assert.Equal(CustomerType.Company, createdUser.CustomerType);
        Assert.Equal("Teszt Kft.", createdUser.CompanyName);
        Assert.Equal("12345678-1-12", createdUser.TaxNumber);
        Assert.True(createdUser.PrintedInvoiceRequested);
        Assert.NotNull(createdUser.BillingAddress);
        Assert.Equal("2112", createdUser.BillingAddress.PostalCode);
        Assert.Equal("Veresegyhaz", createdUser.BillingAddress.City);
        Assert.Equal("Szemere Pal utca", createdUser.BillingAddress.Street);
        Assert.Equal("25", createdUser.BillingAddress.HouseNumber);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ReturnsFailureWithoutSendingEmail()
    {
        _userManager
            .Setup(manager => manager.FindByEmailAsync("vasarlo@example.com"))
            .ReturnsAsync(new ApplicationUser { Email = "vasarlo@example.com" });

        var result = await _service.RegisterAsync(new RegisterRequestDto
        {
            Email = "vasarlo@example.com",
            Password = "Password1",
            ConfirmPassword = "Password1"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Ezzel az email címmel már létezik felhasználó.", result.ErrorMessage);
        _emailService.Verify(service => service.QueueAuthenticationAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenPasswordsDoNotMatch_ReturnsFailureBeforeUserLookup()
    {
        var result = await _service.RegisterAsync(new RegisterRequestDto
        {
            Email = "vasarlo@example.com",
            Password = "Password1",
            ConfirmPassword = "Password2"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("A jelszavak nem egyeznek.", result.ErrorMessage);
        _userManager.Verify(manager => manager.FindByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsValidButEmailIsUnconfirmed_ReturnsFailureBeforeTokenCreation()
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

        var result = await _service.LoginAsync(new LoginRequestDto
        {
            Email = "vasarlo@example.com",
            Password = "Password1"
        });

        Assert.Equal("Hibás email vagy jelszó.", result.ErrorMessage);
        Assert.False(result.Succeeded);
        _tokenService.Verify(service => service.CreateTokenAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsInvalid_ReturnsGenericFailureBeforeTokenCreation()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "vasarlo@example.com",
            EmailConfirmed = true
        };

        _userManager
            .Setup(manager => manager.FindByEmailAsync("vasarlo@example.com"))
            .ReturnsAsync(user);
        _userManager
            .Setup(manager => manager.CheckPasswordAsync(user, "WrongPassword1"))
            .ReturnsAsync(false);

        var result = await _service.LoginAsync(new LoginRequestDto
        {
            Email = "vasarlo@example.com",
            Password = "WrongPassword1"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Hibás email vagy jelszó.", result.ErrorMessage);
        _tokenService.Verify(service => service.CreateTokenAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task ResendEmailConfirmationAsync_WhenUserDoesNotExist_ReturnsNeutralSuccess()
    {
        _userManager
            .Setup(manager => manager.FindByEmailAsync("missing@example.com"))
            .ReturnsAsync((ApplicationUser?)null);

        await _service.ResendEmailConfirmationAsync(
            new ResendEmailConfirmationRequestDto { Email = "missing@example.com" });

        _emailService.Verify(service => service.QueueAuthenticationAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserExistsAndIsActive_QueuesResetEmail()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "vasarlo@example.com",
            EmailConfirmed = true
        };

        _userManager
            .Setup(manager => manager.FindByEmailAsync("vasarlo@example.com"))
            .ReturnsAsync(user);
        _emailService
            .Setup(service => service.QueueAuthenticationAsync(It.Is<ApplicationUser>(user => user.Email == "vasarlo@example.com"), AccountNotificationQueue.PasswordReset, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.ForgotPasswordAsync(new ForgotPasswordRequestDto
        {
            Email = " vasarlo@example.com "
        });

        _emailService.VerifyAll();
        _emailTemplateService.VerifyAll();
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserDoesNotExist_ReturnsNeutralSuccess()
    {
        _userManager
            .Setup(manager => manager.FindByEmailAsync("missing@example.com"))
            .ReturnsAsync((ApplicationUser?)null);

        await _service.ForgotPasswordAsync(new ForgotPasswordRequestDto
        {
            Email = "missing@example.com"
        });

        _emailService.Verify(service => service.QueueAuthenticationAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenIsValid_ResetsPassword()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "vasarlo@example.com"
        };

        _userManager
            .Setup(manager => manager.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);
        _userManager
            .Setup(manager => manager.ResetPasswordAsync(user, "valid-token", "Password1"))
            .ReturnsAsync(IdentityResult.Success);
        _emailTemplateService
            .Setup(service => service.BuildPasswordChangedMessage("vasarlo@example.com"))
            .Returns(new EmailMessage
            {
                To = "vasarlo@example.com",
                Subject = "Jelszavad megváltozott - Bakony Kincse Biokertészet",
                PlainTextBody = "password changed",
                HtmlBody = "<p>password changed</p>"
            });
        _emailService
            .Setup(service => service.QueueMessageAsync(
                It.Is<EmailMessage>(message =>
                    message.To == "vasarlo@example.com"
                    && message.Subject.Contains("Jelszavad megváltozott")),
                "password-changed",
                It.IsAny<CancellationToken>()
            ))
            .Returns(Task.CompletedTask);

        var result = await _service.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            UserId = user.Id,
            Token = "valid-token",
            Password = "Password1",
            ConfirmPassword = "Password1"
        });

        Assert.True(result.Succeeded);
        _emailService.VerifyAll();
        _emailTemplateService.VerifyAll();
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenPasswordsDoNotMatch_ReturnsFailureBeforeUserLookup()
    {
        var result = await _service.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            UserId = Guid.NewGuid(),
            Token = "token",
            Password = "Password1",
            ConfirmPassword = "Password2"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("A jelszavak nem egyeznek.", result.ErrorMessage);
        _userManager.Verify(manager => manager.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmEmailAsync_WhenTokenIsInvalid_ReturnsFailure()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "vasarlo@example.com",
            EmailConfirmed = false
        };

        _userManager
            .Setup(manager => manager.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);
        _userManager
            .Setup(manager => manager.ConfirmEmailAsync(user, "invalid-token"))
            .ReturnsAsync(IdentityResult.Failed());

        var result = await _service.ConfirmEmailAsync(new ConfirmEmailRequestDto
        {
            UserId = user.Id,
            Token = "invalid-token"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Az email megerősítés nem sikerült.", result.ErrorMessage);
    }

    [Fact]
    public async Task ConfirmEmailAsync_WhenEmailIsAlreadyConfirmed_ReturnsSuccess()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "vasarlo@example.com",
            EmailConfirmed = true
        };

        _userManager
            .Setup(manager => manager.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);

        var result = await _service.ConfirmEmailAsync(new ConfirmEmailRequestDto
        {
            UserId = user.Id,
            Token = "any-token"
        });

        Assert.True(result.Succeeded);
        _userManager.Verify(manager => manager.ConfirmEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmEmailAsync_WhenTokenFailsButEmailWasConfirmed_ReturnsSuccess()
    {
        var userId = Guid.NewGuid();
        var userBeforeConfirmation = new ApplicationUser
        {
            Id = userId,
            Email = "vasarlo@example.com",
            EmailConfirmed = false
        };
        var userAfterConfirmation = new ApplicationUser
        {
            Id = userId,
            Email = "vasarlo@example.com",
            EmailConfirmed = true
        };

        _userManager
            .SetupSequence(manager => manager.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(userBeforeConfirmation)
            .ReturnsAsync(userAfterConfirmation);
        _userManager
            .Setup(manager => manager.ConfirmEmailAsync(userBeforeConfirmation, "token"))
            .ReturnsAsync(IdentityResult.Failed());

        var result = await _service.ConfirmEmailAsync(new ConfirmEmailRequestDto
        {
            UserId = userId,
            Token = "token"
        });

        Assert.True(result.Succeeded);
    }

    private static IQueryable<TEntity> CreateAsyncQueryable<TEntity>(IEnumerable<TEntity> entities)
    {
        return new TestAsyncEnumerable<TEntity>(entities);
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock(
        IUserStore<ApplicationUser> userStore) =>
        new(
            userStore,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            // Identity 10 resolves an optional metrics factory during construction.
            Mock.Of<IServiceProvider>(provider => provider.GetService(typeof(System.Diagnostics.Metrics.IMeterFactory)) == null, MockBehavior.Strict),
            NullLogger<UserManager<ApplicationUser>>.Instance);

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
                .GetMethod(nameof(IQueryProvider.Execute), 1, [typeof(Expression)])
                ?? throw new InvalidOperationException("IQueryProvider.Execute method was not found.");
            var typedExecutionResult = executionResult
                .MakeGenericMethod(expectedResultType)
                .Invoke(this, [expression]);

            var fromResultMethod = typeof(Task)
                .GetMethod(nameof(Task.FromResult))
                ?? throw new InvalidOperationException("Task.FromResult method was not found.");

            var taskResult = fromResultMethod
                .MakeGenericMethod(expectedResultType)
                .Invoke(null, [typedExecutionResult])
                ?? throw new InvalidOperationException("Task.FromResult invocation returned null.");

            return (TResult)taskResult;
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
