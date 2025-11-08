namespace ApexV2.Core.Profiles;

using ApexV2.Core.Database;
using Microsoft.EntityFrameworkCore;

public interface IUserProfileRepository
{
    Task<UserProfileEntity?> GetByUserIdAsync(int userId);
    Task AddAsync(UserProfileEntity profile);
    Task UpdateAsync(UserProfileEntity profile);
    Task<bool> ExistsForUserAsync(int userId);
}

public class UserProfileRepository : IUserProfileRepository
{
    private readonly DbContextOptions<ApexDbContext> _options;
    public UserProfileRepository(DbContextOptions<ApexDbContext> options) => _options = options;

    public async Task<UserProfileEntity?> GetByUserIdAsync(int userId)
    {
        await using var ctx = new ApexDbContext(_options);
        return await ctx.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
    }

    public async Task AddAsync(UserProfileEntity profile)
    {
        await using var ctx = new ApexDbContext(_options);
        ctx.UserProfiles.Add(profile);
        await ctx.SaveChangesAsync();
    }

    public async Task UpdateAsync(UserProfileEntity profile)
    {
        await using var ctx = new ApexDbContext(_options);
        ctx.UserProfiles.Update(profile);
        await ctx.SaveChangesAsync();
    }

    public async Task<bool> ExistsForUserAsync(int userId)
    {
        await using var ctx = new ApexDbContext(_options);
        return await ctx.UserProfiles.AnyAsync(p => p.UserId == userId);
    }
}
