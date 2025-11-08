namespace ApexV2.Core.Authentication;

using ApexV2.Core.Database;
using ApexV2.Core.Logging;

public enum AuthStatus { Success, InvalidCredentials, LockedOut, Inactive, PasswordNeedsReset, Error, MustChangePassword }

public record AuthResult(AuthStatus Status, UserEntity? User, string? Message = null, bool PasswordNeedsUpgrade = false);

public class AuthService
{
    private readonly IUserRepository _repo;
    private readonly IPasswordHasher _hasher;
    private readonly Logger _logger;
    private readonly int _lockoutThreshold = 5;
    private readonly TimeSpan _baseLockout = TimeSpan.FromMinutes(5);
    private readonly int _targetIterations = 120_000;

    public AuthService(IUserRepository repo, IPasswordHasher hasher, LogManager logManager)
    {
        _repo = repo;
        _hasher = hasher;
        _logger = logManager.GetLogger("AuthService");
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(string username, string password)
    {
        try
        {
            if (!ValidateUsername(username, out var userErr)) return (false, userErr);
            if (!ValidatePassword(password, out var passErr)) return (false, passErr);
            if (await _repo.UsernameExistsAsync(username)) return (false, "Username already exists");

            var (hash, salt, iterations, alg) = _hasher.HashPassword(password);
            var user = new UserEntity
            {
                Username = username.Trim(),
                UsernameNormalized = username.Trim().ToUpperInvariant(),
                PasswordHash = hash,
                PasswordSalt = salt,
                HashAlgorithm = alg,
                HashIterations = iterations,
                CreatedUtc = DateTime.UtcNow,
                IsActive = true,
                Roles = "User"
            };
            await _repo.AddAsync(user);
            _logger.Info("User registered", new Dictionary<string, object?>{{"user", user.Username}});
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.Error("Registration error", ex);
            return (false, "Internal error");
        }
    }

    public async Task<AuthResult> AuthenticateAsync(string username, string password)
    {
        try
        {
            var user = await _repo.GetByUsernameAsync(username);
            if (user == null)
            {
                _logger.Warn("Login failed - unknown user", new Dictionary<string, object?>{{"user", username}});
                return new AuthResult(AuthStatus.InvalidCredentials, null, "Invalid credentials");
            }
            if (!user.IsActive)
                return new AuthResult(AuthStatus.Inactive, null, "Account inactive");
            if (user.LockoutUntilUtc.HasValue && user.LockoutUntilUtc.Value > DateTime.UtcNow)
            {
                var remaining = user.LockoutUntilUtc.Value - DateTime.UtcNow;
                return new AuthResult(AuthStatus.LockedOut, null, $"Locked out. Try again in {remaining.Minutes}m {remaining.Seconds}s");
            }

            var ok = _hasher.Verify(password, user.PasswordHash, user.PasswordSalt, user.HashIterations, user.HashAlgorithm, out var needsRehash, _targetIterations);
            if (!ok)
            {
                user.FailedLoginCount++;
                if (user.FailedLoginCount >= _lockoutThreshold)
                {
                    var lockoutMinutes = _baseLockout.TotalMinutes * Math.Pow(2, user.FailedLoginCount - _lockoutThreshold); // exponential
                    user.LockoutUntilUtc = DateTime.UtcNow.AddMinutes(Math.Min(lockoutMinutes, 60));
                    _logger.Warn("User locked out", new Dictionary<string, object?>{{"user", user.Username},{"failed", user.FailedLoginCount}});
                }
                await _repo.UpdateAsync(user);
                return new AuthResult(AuthStatus.InvalidCredentials, null, "Invalid credentials");
            }

            // success
            user.FailedLoginCount = 0;
            user.LockoutUntilUtc = null;
            user.LastLoginUtc = DateTime.UtcNow;
            await _repo.UpdateAsync(user);
            _logger.Info("User authenticated", new Dictionary<string, object?>{{"user", user.Username}});
            if (user.MustChangePassword)
            {
                // Verify password first (already ok) then return special status
                await _repo.UpdateAsync(user);
                return new AuthResult(AuthStatus.MustChangePassword, user, "Password change required", needsRehash);
            }
            // TODO [REVIEWED]: Profile ensure will be handled externally after AuthResult consumption for single responsibility
            return new AuthResult(AuthStatus.Success, user, needsRehash ? "Password rehash recommended" : null, needsRehash);
        }
        catch (Exception ex)
        {
            _logger.Error("Authentication error", ex);
            return new AuthResult(AuthStatus.Error, null, "Internal error");
        }
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(Guid userPublicId, string currentPassword, string newPassword)
    {
        try
        {
            var user = await _repo.GetByPublicIdAsync(userPublicId);
            if (user == null) return (false, "User not found");
            var ok = _hasher.Verify(currentPassword, user.PasswordHash, user.PasswordSalt, user.HashIterations, user.HashAlgorithm, out _);
            if (!ok) return (false, "Current password invalid");
            if (!ValidatePassword(newPassword, out var passErr)) return (false, passErr);
            var (hash, salt, iterations, alg) = _hasher.HashPassword(newPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            user.HashAlgorithm = alg;
            user.HashIterations = iterations;
            user.SecurityStamp = Guid.NewGuid().ToString(); // invalidate sessions
            user.MustChangePassword = false; // clear flag
            user.PasswordLastChangedUtc = DateTime.UtcNow;
            await _repo.UpdateAsync(user);
            _logger.Info("Password changed", new Dictionary<string, object?>{{"user", user.Username}});
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.Error("ChangePassword error", ex);
            return (false, "Internal error");
        }
    }

    private bool ValidateUsername(string username, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(username)) { error = "Username required"; return false; }
        if (username.Length < 3) { error = "Username too short"; return false; }
        if (username.Length > 50) { error = "Username too long"; return false; }
        return true;
    }

    private bool ValidatePassword(string password, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(password)) { error = "Password required"; return false; }
        if (password.Length < 10) { error = "Minimum length 10"; return false; }
        int classes = 0;
        if (password.Any(char.IsLower)) classes++;
        if (password.Any(char.IsUpper)) classes++;
        if (password.Any(char.IsDigit)) classes++;
        if (password.Any(c => !char.IsLetterOrDigit(c))) classes++;
        if (classes < 3) { error = "Use 3 of: upper, lower, digit, symbol"; return false; }
        return true;
    }
}
