using System.Windows;
using Hub.Models;

namespace Hub;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        while (true)
        {
            var loginWindow = new LoginWindow();
            if (loginWindow.ShowDialog() != true || loginWindow.AuthenticatedUser is null)
            {
                Shutdown();
                return;
            }

            var mainWindow = new MainWindow(loginWindow.AuthenticatedUser);
            MainWindow = mainWindow;
            mainWindow.ShowDialog();

            // Si l'utilisateur a demandé la déconnexion, on reboucle sur le login
            // Si la fenêtre a été fermée normalement, on quitte
            if (!mainWindow.RequestedLogout)
            {
                Shutdown();
                return;
            }
        }
    }
}
