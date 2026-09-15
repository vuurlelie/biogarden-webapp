using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.BusinessLogic.Options;
using Biogarden.DataAccess.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Biogarden.WebApi.Tests;

public sealed class PaymentAuthorizationTests : IClassFixture<BiogardenWebApiFactory>
{
    private readonly BiogardenWebApiFactory _factory;

    public PaymentAuthorizationTests(BiogardenWebApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Customer")]
    [InlineData("Admin")]
    public async Task CustomerPaymentConfirmationEndpoint_IsNotExposed(string? role)
    {
        using var client = CreateClient(role);

        using var response = await client.PostAsync(
            $"/api/orders/{Guid.NewGuid()}/confirm-payment", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    public async Task ManualPaymentUpdate_RejectsNonAdministrators(
        string? role, HttpStatusCode expectedStatus)
    {
        using var client = CreateClient(role);

        using var response = await client.PutAsJsonAsync(
            $"/api/admin/orders/{Guid.NewGuid()}",
            new AdminOrderUpdateDto
            {
                Status = OrderStatus.Confirmed,
                PaymentStatus = PaymentStatus.Paid
            });

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    private HttpClient CreateClient(string? role)
    {
        var client = _factory.CreateClient();
        if (role is null)
        {
            return client;
        }

        var settings = _factory.Services.GetRequiredService<IOptions<JwtSettings>>().Value;
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, role)
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)),
                SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));

        return client;
    }
}
