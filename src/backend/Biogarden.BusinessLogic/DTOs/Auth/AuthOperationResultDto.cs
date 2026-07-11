namespace Biogarden.BusinessLogic.DTOs.Auth;

public sealed class AuthOperationResultDto
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public static AuthOperationResultDto Success()
    {
        return new AuthOperationResultDto { Succeeded = true };
    }

    public static AuthOperationResultDto Failure(string errorMessage)
    {
        return new AuthOperationResultDto
        {
            Succeeded = false,
            ErrorMessage = errorMessage
        };
    }
}
