using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class AdminUserServiceTests
{
    private readonly Mock<IAppDbContext> _databaseContext = new(MockBehavior.Strict);
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly AdminUserService _service;

    public AdminUserServiceTests()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>(MockBehavior.Strict);
        _userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _service = new AdminUserService(_databaseContext.Object, _userManager.Object);
    }

    [Fact]
    public async Task SetAdminRoleAsync_WhenCurrentAdminRemovesOwnRole_ThrowsBeforeUserLookup()
    {
        var currentAdminId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SetAdminRoleAsync(currentAdminId, isAdmin: false, currentAdminId));

        Assert.Equal("A saját admin jogosultságodat nem veheted el.", exception.Message);
        _databaseContext.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeactivateAsync_WhenCurrentAdminTargetsSelf_ThrowsBeforeUserLookup()
    {
        var currentAdminId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeactivateAsync(currentAdminId, currentAdminId));

        Assert.Equal("A saját fiókodat nem deaktiválhatod az admin felületről.", exception.Message);
        _databaseContext.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EraseAsync_WhenCurrentAdminTargetsSelf_ThrowsBeforeUserLookup()
    {
        var currentAdminId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.EraseAsync(currentAdminId, currentAdminId));

        Assert.Equal("A saját fiókodat nem törölheted az admin felületről.", exception.Message);
        _databaseContext.VerifyNoOtherCalls();
    }
}
