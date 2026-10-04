using SeatLock.Api.Models;

namespace SeatLock.Api.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task CreateAsync(
        User user,
        CancellationToken cancellationToken);
}