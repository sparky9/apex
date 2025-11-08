namespace ApexV2.Core.Authentication;

using ApexV2.Core.Logging;
using ApexV2.Core.Database;

public static class UserSeeder
{
    public static async Task SeedAdminAsync(IUserRepository repo, AuthService authService, LogManager logManager)
    {
        try
        {
            if (await repo.AnyAsync()) return; // users exist
            var adminUser = "admin";
            var tempPassword = "ChangeMe!123!"; // user forced to change at first login
            var (success, error) = await authService.RegisterAsync(adminUser, tempPassword);
            if (success)
            {
                // retrieve and flag
                var user = await repo.GetByUsernameAsync(adminUser);
                if (user != null)
                {
                    user.MustChangePassword = true;
                    await repo.UpdateAsync(user);
                }
                logManager.GetLogger("AuthService").Warn("Default admin seeded. Username=admin TempPassword=ChangeMe!123! Must change on first login");
            }
            else
            {
                logManager.GetLogger("AuthService").Error("Admin seed failed: {error}", new Exception(error ?? "unknown"));
            }
        }
        catch (Exception ex)
        {
            logManager.GetLogger("AuthService").Error("Admin seeding exception", ex);
        }
    }
}
