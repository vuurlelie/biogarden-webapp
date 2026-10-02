using System.Net;
using System.Security.Claims;
using System.Text;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.BusinessLogic.DTOs.Users;
using Biogarden.DataAccess.Enums;
using Biogarden.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Biogarden.WebApi.Tests;

public sealed class ResponseStatusTests
{
    [Theory]
    [InlineData("register", "{\"email\":\"test@example.com\",\"password\":\"Password123!\",\"confirmPassword\":\"Password123!\"}", HttpStatusCode.Created)]
    [InlineData("confirm-email", "{\"userId\":\"00000000-0000-0000-0000-000000000001\",\"token\":\"test-token\"}", HttpStatusCode.NoContent)]
    [InlineData("resend-email-confirmation", "{\"email\":\"test@example.com\"}", HttpStatusCode.Accepted)]
    [InlineData("forgot-password", "{\"email\":\"test@example.com\"}", HttpStatusCode.Accepted)]
    [InlineData("reset-password", "{\"userId\":\"00000000-0000-0000-0000-000000000001\",\"token\":\"test-token\",\"password\":\"Password123!\",\"confirmPassword\":\"Password123!\"}", HttpStatusCode.NoContent)]
    public async Task SuccessfulAuthCommand_ReturnsExpectedStatusWithoutBody(string action, string json, HttpStatusCode status)
    {
        using var factory = new BiogardenWebApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAuthService>();
                services.AddSingleton<IAuthService, SuccessfulAuthService>();
            }));
        using var client = factory.CreateClient();
        using var response = await client.PostAsync($"/api/auth/{action}", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(status, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(AccountLifecycleRequestStatus.Pending, StatusCodes.Status202Accepted)]
    [InlineData(AccountLifecycleRequestStatus.Completed, StatusCodes.Status200OK)]
    public async Task AccountLifecycle_DistinguishesPendingProcessingFromCompletedAction(AccountLifecycleRequestStatus state, int status)
    {
        var controller = new ProfileController(new LifecycleService(state))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "test"))
                }
            }
        };
        var response = await controller.RequestAccountLifecycleAsync(new AccountLifecycleRequestDto(), default);
        var result = Assert.IsAssignableFrom<ObjectResult>(response.Result);
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(state, Assert.IsType<AccountLifecycleResultDto>(result.Value).Status);
    }

    private sealed class SuccessfulAuthService : IAuthService
    {
        public Task<AuthOperationResultDto> RegisterAsync(RegisterRequestDto request) => Task.FromResult(AuthOperationResultDto.Success());
        public Task<LoginResultDto> LoginAsync(LoginRequestDto request) => throw new NotSupportedException();
        public Task<AuthOperationResultDto> ConfirmEmailAsync(ConfirmEmailRequestDto request) => Task.FromResult(AuthOperationResultDto.Success());
        public Task ResendEmailConfirmationAsync(ResendEmailConfirmationRequestDto request) => Task.CompletedTask;
        public Task ForgotPasswordAsync(ForgotPasswordRequestDto request) => Task.CompletedTask;
        public Task<AuthOperationResultDto> ResetPasswordAsync(ResetPasswordRequestDto request) => Task.FromResult(AuthOperationResultDto.Success());
    }

    private sealed class LifecycleService(AccountLifecycleRequestStatus state) : IUserService
    {
        public Task<UserProfileDto?> GetProfileAsync(Guid userId) => throw new NotSupportedException();
        public Task UpdateProfileAsync(Guid userId, UpdateUserProfileDto dto) => throw new NotSupportedException();
        public Task<UserAddressesDto?> GetAddressesAsync(Guid userId) => throw new NotSupportedException();
        public Task UpdateAddressesAsync(Guid userId, UpdateUserAddressesDto dto) => throw new NotSupportedException();
        public Task<AccountLifecycleResultDto> RequestAccountLifecycleAsync(Guid userId, AccountLifecycleRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new AccountLifecycleResultDto { Status = state, Message = "Test outcome" });
    }
}
