namespace ApexV2.Core.Profiles;

using System.Text.RegularExpressions;
using ApexV2.Core.Authentication;
using ApexV2.Core.Database;
using ApexV2.Core.Logging;

public record UserProfileDto(int UserId, string DisplayName, string? FirstName, string? LastName, string? Email, string? Bio, string? AvatarPath);

public class ProfileService
{
    private readonly IUserProfileRepository _profiles;
    private readonly IUserRepository _users;
    private readonly Logger _logger;

    public ProfileService(IUserProfileRepository profiles, IUserRepository users, LogManager logManager)
    {
        _profiles = profiles;
        _users = users;
        _logger = logManager.GetLogger("ProfileService");
    }

    public async Task<UserProfileEntity> EnsureProfileAsync(int userId)
    {
        var existing = await _profiles.GetByUserIdAsync(userId);
        if (existing != null) return existing;
        var user = await _users.GetByIdAsync(userId) ?? throw new InvalidOperationException("User missing for profile creation");
        var profile = new UserProfileEntity
        {
            UserId = user.Id,
            DisplayName = user.Username,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
        await _profiles.AddAsync(profile);
        _logger.Info("Profile created", new Dictionary<string, object?>{{"user", user.Username}});
        return profile;
    }

    public async Task<UserProfileEntity?> GetAsync(int userId) => await _profiles.GetByUserIdAsync(userId);

    public async Task<(bool Success, string? Error, UserProfileEntity? Profile)> UpdateAsync(UserProfileDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.DisplayName)) return (false, "Display name required", null);
            var displayName = NormalizeSpaces(dto.DisplayName.Trim());
            if (displayName.Length > 100) return (false, "Display name too long", null);
            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                var email = dto.Email.Trim().ToLowerInvariant();
                if (!IsValidEmail(email)) return (false, "Invalid email", null);
                dto = dto with { Email = email };
            }
            if (!string.IsNullOrWhiteSpace(dto.Bio) && dto.Bio.Length > 1000) return (false, "Bio too long (max 1000)", null);
            var profile = await _profiles.GetByUserIdAsync(dto.UserId);
            if (profile == null) return (false, "Profile not found", null);
            // change detection
            bool changed = false;
            void Assign<T>(Action<T> setter, T current, T newVal)
            {
                if (!Equals(current, newVal)) { setter(newVal); changed = true; }
            }
            Assign(v => profile.DisplayName = v!, profile.DisplayName, displayName);
            Assign(v => profile.FirstName = v, profile.FirstName, dto.FirstName?.Trim());
            Assign(v => profile.LastName = v, profile.LastName, dto.LastName?.Trim());
            Assign(v => profile.Email = v, profile.Email, dto.Email);
            Assign(v => profile.Bio = v, profile.Bio, dto.Bio?.Trim());
            Assign(v => profile.AvatarPath = v, profile.AvatarPath, dto.AvatarPath);
            if (!changed)
            {
                return (true, null, profile); // nothing to do
            }
            profile.UpdatedUtc = DateTime.UtcNow;
            await _profiles.UpdateAsync(profile);
            _logger.Info("Profile updated", new Dictionary<string, object?>{{"userId", dto.UserId}});
            return (true, null, profile);
        }
        catch (Exception ex)
        {
            var cid = Guid.NewGuid().ToString()[..8];
            _logger.Error($"Profile update failed cid={cid}", ex);
            return (false, $"Internal error (ref {cid})", null);
        }
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
        }
        catch { return false; }
    }

    private static string NormalizeSpaces(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return System.Text.RegularExpressions.Regex.Replace(input, "\\s+", " ");
    }
}
