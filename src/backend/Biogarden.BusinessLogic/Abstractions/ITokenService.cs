using Biogarden.DataAccess.Entities;

namespace Biogarden.BusinessLogic.Abstractions;

public interface ITokenService
{
    Task<string> CreateTokenAsync(ApplicationUser user);
}
