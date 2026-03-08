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

    public async Task<Address?> GetByIdAsync(int id)
    {
        return await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<List<Address>> GetAllAsync()
    {
        return await _context.Addresses.ToListAsync();
    }

    public async Task AddAsync(Address address)
    {
        await _context.Addresses.AddAsync(address);
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