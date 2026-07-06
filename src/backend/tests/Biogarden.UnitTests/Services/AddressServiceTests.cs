using Biogarden.BusinessLogic.DTOs.Addresses;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class AddressServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);
    private readonly Mock<IAddressRepository> _addresses = new(MockBehavior.Strict);
    private readonly AddressService _service;

    public AddressServiceTests()
    {
        _unitOfWork.SetupGet(x => x.AddressRepository).Returns(_addresses.Object);
        _service = new AddressService(_unitOfWork.Object);
    }

    [Fact]
    public async Task CreateAsync_PersistsMappedAddress_AndReturnsGeneratedId()
    {
        var dto = new AddressCreateDto
        {
            PostalCode = "8431",
            City = "Bakonyszentlászló",
            Street = "Jókai utca",
            HouseNumber = "22"
        };

        _addresses
            .Setup(x => x.AddAsync(It.IsAny<Address>(), It.IsAny<CancellationToken>()))
            .Callback<Address, CancellationToken>((address, _) => address.Id = 42)
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var id = await _service.CreateAsync(dto);

        Assert.Equal(42, id);
        _addresses.Verify(x => x.AddAsync(
            It.Is<Address>(a =>
                a.PostalCode == dto.PostalCode &&
                a.City == dto.City &&
                a.Street == dto.Street &&
                a.HouseNumber == dto.HouseNumber),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenAddressDoesNotExist_ThrowsAndDoesNotSave()
    {
        _addresses
            .Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Address?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UpdateAsync(999, new AddressEditDto()));

        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenAddressExists_DeletesAndSaves()
    {
        var address = CreateAddress();
        _addresses
            .Setup(x => x.GetByIdAsync(address.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);
        _addresses.Setup(x => x.Delete(address));
        _unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await _service.DeleteAsync(address.Id);

        _addresses.Verify(x => x.Delete(address), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Address CreateAddress() => new()
    {
        Id = 7,
        PostalCode = "8431",
        City = "Bakonyszentlászló",
        Street = "Jókai utca",
        HouseNumber = "22"
    };
}
