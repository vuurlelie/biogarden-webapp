using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess.Repositories;

public class AddressRepository : IAddressRepository
{
    private readonly IAppDbContext _context;

    public AddressRepository(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<Address?> GetByIdAsync(int addressId, CancellationToken cancellationToken = default)
    {
        return await _context.Addresses
            .FirstOrDefaultAsync(address => address.Id == addressId, cancellationToken);
    }

    public async Task<List<Address>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Addresses
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Address address, CancellationToken cancellationToken = default)
    {
        await _context.Addresses
            .AddAsync(address, cancellationToken);
    }

    public void Update(Address address)
    {
        _context.Addresses.Update(address);
    }

    public void Delete(Address address)
    {
        address.RecordStatus = RecordStatus.Deleted;

        _context.Addresses.Update(address);
    }
}
