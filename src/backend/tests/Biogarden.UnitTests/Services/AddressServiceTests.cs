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
        _unitOfWork.SetupGet(unitOfWork => unitOfWork.AddressRepository).Returns(_addresses.Object);
        _service = new AddressService(_unitOfWork.Object);
    }

    [Fact]
    public async Task CreateAsync_PersistsMappedAddress_AndReturnsGeneratedId()
    {
        var addressCreate = new AddressCreateDto
        {
            PostalCode = "8431",
            City = "Bakonyszentlászló",
            Street = "Jókai utca",
            HouseNumber = "22"
        };

        _addresses
            .Setup(addressRepository => addressRepository.AddAsync(It.IsAny<Address>(), It.IsAny<CancellationToken>()))
            .Callback<Address, CancellationToken>((address, _) => address.Id = 42)
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var id = await _service.CreateAsync(addressCreate);

        Assert.Equal(42, id);
        _addresses.Verify(
            addressRepository => addressRepository.AddAsync(
                        It.Is<Address>(candidateAddress =>
                            candidateAddress.PostalCode == addressCreate.PostalCode &&
                            candidateAddress.City == addressCreate.City &&
                            candidateAddress.Street == addressCreate.Street &&
                            candidateAddress.HouseNumber == addressCreate.HouseNumber),
                        It.IsAny<CancellationToken>()
                    ),
            Times.Once
        );
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenAddressDoesNotExist_ThrowsAndDoesNotSave()
    {
        _addresses
            .Setup(addressRepository => addressRepository.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Address?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UpdateAsync(999, new AddressEditDto()));

        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenAddressExists_DeletesAndSaves()
    {
        var address = CreateAddress();
        _addresses
            .Setup(addressRepository => addressRepository.GetByIdAsync(address.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);
        _addresses.Setup(addressRepository => addressRepository.Delete(address));
        _unitOfWork
            .Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await _service.DeleteAsync(address.Id);

        _addresses.Verify(addressRepository => addressRepository.Delete(address), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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
