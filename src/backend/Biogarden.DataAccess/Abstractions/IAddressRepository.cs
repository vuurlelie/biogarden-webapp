using Biogarden.DataAccess.Entities;

public interface IAddressRepository
{
    Task<Address?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<Address>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Address address, CancellationToken cancellationToken = default);
    void Update(Address address);
    void Delete(Address address);
}
