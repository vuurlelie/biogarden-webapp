using Biogarden.DataAccess.Entities;

namespace Biogarden.DataAccess.Abstractions;

public interface IAddressRepository
{
    Task<Address?> GetByIdAsync(int id);

    Task<List<Address>> GetAllAsync();

    Task AddAsync(Address address);

    void Update(Address address);

    void Delete(Address address);
}