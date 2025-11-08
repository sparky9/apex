namespace ApexV2.Core.Authentication;

using ApexV2.Core.Database;
using Microsoft.EntityFrameworkCore;

public interface IUserRepository
{
    Task<UserEntity?> GetByUsernameAsync(string username);
    Task<UserEntity?> GetByPublicIdAsync(Guid publicId);
    Task<UserEntity?> GetByIdAsync(int id);
    Task<bool> UsernameExistsAsync(string username);
    Task AddAsync(UserEntity user);
    Task UpdateAsync(UserEntity user);
    Task<bool> AnyAsync();
}

public class UserRepository : IUserRepository
{
    private readonly DbContextOptions<ApexDbContext> _options;
    public UserRepository(DbContextOptions<ApexDbContext> options)
    {
        _options = options;
    }

    private static string Normalize(string username) => username.Trim().ToUpperInvariant();

    public async Task<UserEntity?> GetByUsernameAsync(string username)
    {
        await using var ctx = new ApexDbContext(_options);
        var norm = Normalize(username);
        return await ctx.Users.FirstOrDefaultAsync(u => u.UsernameNormalized == norm);
    }

    public async Task<UserEntity?> GetByPublicIdAsync(Guid publicId)
    {
        await using var ctx = new ApexDbContext(_options);
        return await ctx.Users.FirstOrDefaultAsync(u => u.PublicId == publicId);
    }

    public async Task<UserEntity?> GetByIdAsync(int id)
    {
        await using var ctx = new ApexDbContext(_options);
        return await ctx.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        await using var ctx = new ApexDbContext(_options);
        var norm = Normalize(username);
        return await ctx.Users.AnyAsync(u => u.UsernameNormalized == norm);
    }

    public async Task AddAsync(UserEntity user)
    {
        await using var ctx = new ApexDbContext(_options);
        user.UsernameNormalized = Normalize(user.Username);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
    }

    public async Task UpdateAsync(UserEntity user)
    {
        await using var ctx = new ApexDbContext(_options);
        ctx.Users.Update(user);
        await ctx.SaveChangesAsync();
    }

    public async Task<bool> AnyAsync()
    {
        await using var ctx = new ApexDbContext(_options);
        return await ctx.Users.AnyAsync();
    }
}
