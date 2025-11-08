using System.Windows;
using ApexV2.Core.Authentication;
using ApexV2.Core.Database;
using ApexV2.Core.Logging;

namespace ApexV2.Windows;

public partial class LoginWindow : Window
{
    private readonly AuthService _authService;
    private readonly IUserRepository _userRepo;
    private readonly IPasswordHasher _hasher;
    public UserEntity? AuthenticatedUser { get; private set; }

    public LoginWindow(AuthService authService, IUserRepository userRepo, IPasswordHasher hasher)
    {
        InitializeComponent();
        _authService = authService;
        _userRepo = userRepo;
        _hasher = hasher;
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        MessageText.Text = string.Empty;
        var username = UsernameTextBox.Text.Trim();
        var password = PasswordBox.Password;

        if (ShowRegisterCheckBox.IsChecked == true)
        {
            var (ok, err) = await _authService.RegisterAsync(username, password);
            if (!ok)
            {
                MessageText.Text = err;
                return;
            }
            MessageText.Text = "Account created. You are now logged in.";
            var result = await _authService.AuthenticateAsync(username, password);
            if (result.Status == AuthStatus.Success && result.User != null)
            {
                AuthenticatedUser = result.User;
                DialogResult = true;
            }
            return;
        }
        else
        {
            var result = await _authService.AuthenticateAsync(username, password);
            if (result.Status == AuthStatus.Success && result.User != null)
            {
                if (result.PasswordNeedsUpgrade)
                {
                    MessageText.Text = "Password will be upgraded silently.";
                    // Optional: trigger rehash here
                }
                AuthenticatedUser = result.User;
                DialogResult = true;
                return;
            }
            MessageText.Text = result.Message ?? "Login failed";
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
