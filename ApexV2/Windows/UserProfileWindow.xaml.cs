using System.Windows;
using ApexV2.Core.Profiles;
using ApexV2.Core.Authentication;
using ApexV2.Core.Database;
using ApexV2.Core.Logging;

namespace ApexV2.Windows;

public partial class UserProfileWindow : Window
{
    private readonly ProfileService _profileService;
    private readonly UserEntity _user;
    private UserProfileEntity? _profile;
    private readonly Logger _logger;

    public UserProfileWindow(ProfileService service, UserEntity user, Logger logger)
    {
        InitializeComponent();
        _profileService = service;
        _user = user;
        _logger = logger;
        Loaded += UserProfileWindow_Loaded;
    }

    private async void UserProfileWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _profile = await _profileService.EnsureProfileAsync(_user.Id);
        DisplayNameText.Text = _profile.DisplayName;
        FirstNameText.Text = _profile.FirstName;
        LastNameText.Text = _profile.LastName;
        EmailText.Text = _profile.Email;
        BioText.Text = _profile.Bio;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_profile == null) return;
        var dto = new UserProfileDto(_user.Id, DisplayNameText.Text, FirstNameText.Text, LastNameText.Text, EmailText.Text, BioText.Text, _profile.AvatarPath);
        var (ok, error, updated) = await _profileService.UpdateAsync(dto);
        if (!ok)
        {
            MessageText.Text = error;
            return;
        }
        _profile = updated;
        MessageText.Text = "Saved.";
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
