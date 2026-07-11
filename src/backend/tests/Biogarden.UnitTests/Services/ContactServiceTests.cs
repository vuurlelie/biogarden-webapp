using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Contact;
using Biogarden.BusinessLogic.Models.Email;
using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Microsoft.Extensions.Options;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class ContactServiceTests
{
    [Fact]
    public async Task SendMessageAsync_SendsBusinessNotificationAndVisitorConfirmation()
    {
        var emailService = new Mock<IEmailService>(MockBehavior.Strict);
        var templateService = new Mock<IEmailTemplateService>(MockBehavior.Strict);

        var service = new ContactService(
            emailService.Object,
            templateService.Object,
            Options.Create(new EmailBrandingOptions
            {
                ContactEmail = "info@bakonykincse.hu"
            }));

        var businessMessage = new EmailMessage
        {
            To = "info@bakonykincse.hu",
            Subject = "Új kapcsolatfelvételi üzenet",
            PlainTextBody = "business"
        };
        var visitorMessage = new EmailMessage
        {
            To = "visitor@example.com",
            Subject = "Megkaptuk az üzenetedet",
            PlainTextBody = "visitor"
        };

        templateService
            .Setup(template => template.BuildContactBusinessNotificationMessage(
                "info@bakonykincse.hu",
                It.Is<ContactMessageRequestDto>(request =>
                    request.Name == "Teszt Elek"
                    && request.Email == "visitor@example.com"
                    && request.Phone == "+36 20 123 4567"
                    && request.Subject == "Kérdés"
                    && request.Message == "Szeretnék érdeklődni a termékekről.")))
            .Returns(businessMessage);
        templateService
            .Setup(template => template.BuildContactVisitorConfirmationMessage(
                "visitor@example.com",
                It.Is<ContactMessageRequestDto>(request =>
                    request.Name == "Teszt Elek"
                    && request.Email == "visitor@example.com")))
            .Returns(visitorMessage);
        emailService
            .Setup(sender => sender.SendAsync(businessMessage, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        emailService
            .Setup(sender => sender.SendAsync(visitorMessage, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await service.SendMessageAsync(new ContactMessageRequestDto
        {
            Name = " Teszt Elek ",
            Email = " visitor@example.com ",
            Phone = " +36 20 123 4567 ",
            Subject = " Kérdés ",
            Message = " Szeretnék érdeklődni a termékekről. "
        });

        templateService.VerifyAll();
        emailService.VerifyAll();
    }
}
