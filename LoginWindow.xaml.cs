using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Hub.Models;
using Hub.Services;

namespace Hub;

public partial class LoginWindow : Window
{
    private readonly string    _domain;
    private readonly HubConfig _config;
    private readonly LdapAuthService _authService = new();

    private SavedUser? _selectedUser;

    public AuthenticatedUser? AuthenticatedUser { get; private set; }

    public LoginWindow()
    {
        _config = HubConfigService.Load();
        _domain = _config.Domain;
        InitializeComponent();
        Title = _config.AppTitle;
        ApplyStrings();
        Loaded += LoginWindow_Loaded;
    }

    private void ApplyStrings()
    {
        AppTitleText.Text           = _config.AppTitle;
        SelectUserLabel.Text        = L10n.T("Login_SelectUser");
        SelectConnectButton.Content = L10n.T("Login_Connect");
        OtherAccountText.Text       = L10n.T("Login_OtherAccount");
        QuitButton.Content          = L10n.T("Login_Quit");

        if (!string.IsNullOrWhiteSpace(_config.FirstConnectionUrl))
        {
            FirstConnectionText.Text       = L10n.T("Login_FirstConnection");
            FirstConnectionLink.Visibility = Visibility.Visible;
            SepText.Visibility             = Visibility.Visible;
        }

        PasswordLabel.Text      = L10n.T("Login_Password");
        BackButton.Content      = L10n.T("Login_Back");
        LoginButton.Content     = L10n.T("Login_Connect");

        UsernameLabel.Text        = L10n.T("Login_Username");
        NewPasswordLabel.Text     = L10n.T("Login_Password");
        BackFromNewButton.Content = L10n.T("Login_Back");
        NewLoginButton.Content    = L10n.T("Login_Connect");
    }

    private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var users = UserHistoryService.Load();
        if (users.Count == 0)
            ShowNewAccountView();
        else
        {
            PopulateComboBox(users);
            ShowSelectView();
        }
    }

    private void PopulateComboBox(List<SavedUser> users)
    {
        UserComboBox.Items.Clear();
        foreach (var u in users)
            UserComboBox.Items.Add(u);
        UserComboBox.SelectedIndex = 0;
    }

    private void UserComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var has = UserComboBox.SelectedItem is SavedUser;
        SelectConnectButton.IsEnabled = has;
        RemoveUserButton.Visibility   = has ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RemoveSelectedUser_Click(object sender, RoutedEventArgs e)
    {
        if (UserComboBox.SelectedItem is not SavedUser user) return;

        var result = MessageBox.Show(
            this,
            L10n.T("Msg_ConfirmDelete", user.DisplayName),
            L10n.T("Msg_ConfirmDelete_Title"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes) return;

        UserHistoryService.Remove(user.SamAccountName);
        ConnectionLogService.LogUserRemoved(user.SamAccountName);
        if (!string.IsNullOrEmpty(user.SidValue))
            WebViewHostManager.DeleteCacheForSid(user.SidValue, _config.CacheFolder);

        var users = UserHistoryService.Load();
        if (users.Count == 0) ShowNewAccountView();
        else { PopulateComboBox(users); ShowSelectView(); }
    }

    private void SelectConnect_Click(object sender, RoutedEventArgs e)
    {
        if (UserComboBox.SelectedItem is not SavedUser user) return;

        _selectedUser = user;
        SelectedDisplayName.Text = user.DisplayName;
        ErrorText.Visibility = Visibility.Collapsed;
        PasswordBox.Clear();

        ShowPasswordView();
        PasswordBox.Focus();
    }

    private void NewAccount_Click(object sender, RoutedEventArgs e) => ShowNewAccountView();

    private void BackToSelect_Click(object sender, RoutedEventArgs e)
    {
        var users = UserHistoryService.Load();
        if (users.Count == 0) ShowNewAccountView();
        else { PopulateComboBox(users); ShowSelectView(); }
    }

    private void FirstConnection_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_config.FirstConnectionUrl))
            Process.Start(new ProcessStartInfo(_config.FirstConnectionUrl) { UseShellExecute = true });
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedUser is null) return;
        await AuthenticateAsync(_selectedUser.SamAccountName, PasswordBox.Password, isNew: false);
    }

    private async void NewLoginButton_Click(object sender, RoutedEventArgs e)
    {
        await AuthenticateAsync(UserNameTextBox.Text.Trim(), NewPasswordBox.Password, isNew: true);
    }

    private async Task AuthenticateAsync(string username, string password, bool isNew)
    {
        var activeBtn = isNew ? NewLoginButton : LoginButton;
        activeBtn.IsEnabled = false;
        Cursor = System.Windows.Input.Cursors.Wait;

        try
        {
            // 1) Vérifier que AD est joignable avant toute tentative
            var reachable = await Task.Run(() => _authService.IsReachable(_domain));
            if (!reachable)
            {
                ShowError(isNew, L10n.T("Login_AdUnreachable", _domain));
                return;
            }

            // 2) Authentification
            var user = await Task.Run(() => _authService.Authenticate(_domain, username, password));

            if (user is null)
            {
                ShowError(isNew, L10n.T("Login_InvalidCreds"));
                ConnectionLogService.LogLoginFailed(username);
                if (isNew) { NewPasswordBox.Clear(); NewPasswordBox.Focus(); }
                else       { PasswordBox.Clear();    PasswordBox.Focus();    }
                return;
            }

            var wasAdded = UserHistoryService.Save(user);
            if (wasAdded)
                ConnectionLogService.LogUserAdded(user.SamAccountName);
            ConnectionLogService.LogLoginSuccess(user.SamAccountName);

            AuthenticatedUser = user;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(isNew, L10n.T("Login_Error", ex.Message));
        }
        finally
        {
            Cursor = System.Windows.Input.Cursors.Arrow;
            activeBtn.IsEnabled = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowSelectView()
    {
        SelectView.Visibility     = Visibility.Visible;
        PasswordView.Visibility   = Visibility.Collapsed;
        NewAccountView.Visibility = Visibility.Collapsed;
    }

    private void ShowPasswordView()
    {
        SelectView.Visibility     = Visibility.Collapsed;
        PasswordView.Visibility   = Visibility.Visible;
        NewAccountView.Visibility = Visibility.Collapsed;
    }

    private void ShowNewAccountView()
    {
        SelectView.Visibility     = Visibility.Collapsed;
        PasswordView.Visibility   = Visibility.Collapsed;
        NewAccountView.Visibility = Visibility.Visible;

        var hasHistory = UserHistoryService.Load().Count > 0;
        BackFromNewButton.Visibility = hasHistory ? Visibility.Visible : Visibility.Collapsed;

        UserNameTextBox.Clear();
        NewPasswordBox.Clear();
        NewErrorText.Visibility = Visibility.Collapsed;
        UserNameTextBox.Focus();
    }

    private void ShowError(bool isNew, string message)
    {
        if (isNew) { NewErrorText.Text = message; NewErrorText.Visibility = Visibility.Visible; }
        else       { ErrorText.Text    = message; ErrorText.Visibility    = Visibility.Visible; }
    }
}
