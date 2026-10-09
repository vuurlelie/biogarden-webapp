using Biogarden.BusinessLogic.Constants;
using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.DTOs.Products;
using Biogarden.BusinessLogic.Validation;
using Biogarden.DataAccess.Enums;
using FluentValidation;

namespace Biogarden.UnitTests.Validation;

public sealed class RequestValidatorTests
{
    private static PlaceOrderDto Order() => new()
    {
        IdempotencyKey = "checkout-test-123",
        AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
        AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion,
        CustomerName = "Teszt Vásárló",
        CustomerEmail = "test@example.com",
        CustomerPhone = "123456789"
    };

    [Fact]
    public async Task GuestPickup_DoesNotRequireBillingOrDeliveryAddress()
    {
        var result = await new PlaceOrderValidator().ValidateAsync(Order());
        Assert.True(result.IsValid, result.ToString());
    }

    [Fact]
    public async Task AccountCheckout_CanUseStoredContactAndAddress()
    {
        var order = Order();
        order.CustomerName = order.CustomerEmail = order.CustomerPhone = null;
        order.CustomerType = CustomerType.Company;
        order.AddressId = 7;
        var context = new ValidationContext<PlaceOrderDto>(order);
        context.RootContextData["Authenticated"] = true;
        var result = await new PlaceOrderValidator().ValidateAsync(context);
        Assert.True(result.IsValid, result.ToString());
        Assert.False((await new PlaceOrderValidator().ValidateAsync(order)).IsValid);
    }

    [Fact]
    public async Task GuestCompany_ReportsConditionalFieldsAndInvalidTaxNumber()
    {
        var order = Order();
        order.CustomerType = CustomerType.Company;
        order.BillingCompanyName = "Teszt Kft.";
        order.BillingTaxNumber = "invalid";
        var result = await new PlaceOrderValidator().ValidateAsync(order);
        Assert.Contains(result.Errors, error => error.PropertyName == "PostalCode");
        Assert.Contains(result.Errors, error => error.PropertyName == "BillingTaxNumber");
    }

    [Fact]
    public async Task PartialManualContact_CannotFallBackToAccountSilently()
    {
        var order = Order();
        order.CustomerEmail = null;
        var context = new ValidationContext<PlaceOrderDto>(order);
        context.RootContextData["Authenticated"] = true;
        var result = await new PlaceOrderValidator().ValidateAsync(context);
        Assert.Contains(result.Errors, error => error.PropertyName == "CustomerEmail");
    }

    [Fact]
    public async Task UndefinedEnumsAndOldLegalVersion_AreInputErrors()
    {
        var order = Order();
        order.FulfillmentMethod = (FulfillmentMethod)999;
        order.AcceptedTermsVersion = "old";
        var result = await new PlaceOrderValidator().ValidateAsync(order);
        Assert.Contains(result.Errors, error => error.PropertyName == "FulfillmentMethod");
        Assert.Contains(result.Errors, error => error.PropertyName == "AcceptedTermsVersion");
    }

    [Fact]
    public async Task Product_RejectsUndefinedEnumsNegativeStockAndOverlongName()
    {
        var productCreate = new ProductCreateDto { Name = new string('a', 201), StockQuantity = -1, Category = (ProductCategory)999 };
        var result = await new ProductCreateDtoValidator().ValidateAsync(productCreate);
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        Assert.Contains(result.Errors, error => error.PropertyName == "StockQuantity");
        Assert.Contains(result.Errors, error => error.PropertyName == "Category");
    }

    [Fact]
    public async Task ProfileEditing_PreservesOptionalCompanyDetails()
    {
        var result = await new UpdateUserProfileDtoValidator().ValidateAsync(new Biogarden.BusinessLogic.DTOs.Users.UpdateUserProfileDto { CustomerType = CustomerType.Company });
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task NestedAddress_ReportsTheFieldBeforeSqlLengthFailure()
    {
        var result = await new UpdateUserAddressesDtoValidator().ValidateAsync(new Biogarden.BusinessLogic.DTOs.Users.UpdateUserAddressesDto
        {
            ShippingAddress = new Biogarden.BusinessLogic.DTOs.Addresses.AddressCreateDto { PostalCode = "12345", City = "Budapest", Street = "Utca", HouseNumber = "1" }
        });
        Assert.Contains(result.Errors, error => error.PropertyName == "ShippingAddress.PostalCode");
    }

    [Fact]
    public async Task CompanyRegistration_RequiresNestedBillingAddress()
    {
        var result = await new RegisterRequestDtoValidator().ValidateAsync(new RegisterRequestDto
        {
            Email = "test@example.com",
            Password = "test",
            ConfirmPassword = "test",
            CustomerType = CustomerType.Company,
            CompanyName = "Teszt",
            TaxNumber = "12345678-1-12"
        });
        Assert.Contains(result.Errors, error => error.PropertyName == "BillingAddress");
    }
}
