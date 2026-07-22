using System.Net;
using System.Net.Http.Json;
using System.Text;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.WebApi.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Biogarden.WebApi.Tests;

public sealed class RateLimitingTests
{
    [Fact]
    public async Task SensitiveAuthEndpoint_ReturnsTooManyRequests_WhenPolicyLimitIsExceeded()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IAuthService>();
                    services.AddScoped<IAuthService, AlwaysFailingAuthService>();
                });
            });

        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var allowedResponse = await client.PostAsync(
                "/api/auth/login",
                CreateLoginRequestContent());

            Assert.Equal(HttpStatusCode.Unauthorized, allowedResponse.StatusCode);
        }

        using var rejectedResponse = await client.PostAsync(
            "/api/auth/login",
            CreateLoginRequestContent());

        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
        Assert.NotNull(rejectedResponse.Headers.RetryAfter?.Delta);

        var error = await rejectedResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status429TooManyRequests, error.Status);
        Assert.Equal("Too many requests.", error.Title);
        Assert.Contains("Túl sok kérés", error.Detail);
        Assert.False(string.IsNullOrWhiteSpace(error.TraceId));
    }

    private static StringContent CreateLoginRequestContent()
    {
        const string loginRequest = """
            {
              "email": "test@example.com",
              "password": "WrongPassword1"
            }
            """;

        return new StringContent(
            loginRequest,
            Encoding.UTF8,
            "application/json");
    }

    private sealed class AlwaysFailingAuthService : IAuthService
    {
        public Task<AuthOperationResultDto> RegisterAsync(RegisterRequestDto request)
        {
            return Task.FromResult(AuthOperationResultDto.Failure("Registration failed."));
        }

        public Task<LoginResultDto> LoginAsync(LoginRequestDto loginRequestDto)
        {
            return Task.FromResult(LoginResultDto.Failure("Authentication failed."));
        }

        public Task<AuthOperationResultDto> ConfirmEmailAsync(
            ConfirmEmailRequestDto confirmEmailRequestDto)
        {
            return Task.FromResult(AuthOperationResultDto.Failure("Email confirmation failed."));
        }

        public Task ResendEmailConfirmationAsync(
            ResendEmailConfirmationRequestDto resendEmailConfirmationRequestDto)
        {
            return Task.CompletedTask;
        }

        public Task ForgotPasswordAsync(ForgotPasswordRequestDto forgotPasswordRequestDto)
        {
            return Task.CompletedTask;
        }

        public Task<AuthOperationResultDto> ResetPasswordAsync(
            ResetPasswordRequestDto resetPasswordRequestDto)
        {
            return Task.FromResult(AuthOperationResultDto.Failure("Password reset failed."));
        }
    }
}
