using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Contact;
using Biogarden.WebApi.Constants;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[Route("api/contact")]
public sealed class ContactController : ControllerBase
{
    private readonly IContactService _contactService;

    public ContactController(IContactService contactService)
    {
        _contactService = contactService;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [EnableRateLimiting(RateLimitPolicyNames.PublicForm)]
    public async Task<IActionResult> SendMessageAsync([FromBody] ContactMessageRequestDto contactMessageRequestDto, CancellationToken cancellationToken)
    {
        await _contactService.SendMessageAsync(contactMessageRequestDto, cancellationToken);

        return Accepted(new { submissionId = contactMessageRequestDto.SubmissionId });
    }
}
