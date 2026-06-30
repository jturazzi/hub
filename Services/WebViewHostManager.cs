using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Hub.Services;

/// <summary>
/// Gère un environnement WebView2 unique partagé entre toutes les applications,
/// avec un cache global par session utilisateur (SID).
/// </summary>
public sealed class WebViewHostManager : IDisposable
{
    private readonly Dictionary<string, WebView2> _webViews = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _userDataFolder;
    private CoreWebView2Environment? _environment;
    private bool _disposed;

    public WebViewHostManager(string sid, string? cacheRoot = null)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new ArgumentException("SID invalide.", nameof(sid));
        }

        var root = cacheRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Hub", "Cache");

        // Cache global : un seul dossier par utilisateur, partagé entre toutes les apps
        _userDataFolder = Path.Combine(root, SanitizePathToken(sid));
    }

    public string UserDataFolder => _userDataFolder;

    public WebView2 GetOrCreate(string appName)
    {
        ThrowIfDisposed();

        if (_webViews.TryGetValue(appName, out var existing))
        {
            return existing;
        }

        var webView = new WebView2();
        webView.NavigationCompleted += OnNavigationCompleted;
        _webViews[appName] = webView;

        return webView;
    }

    public async Task EnsureInitializedAsync(string appName)
    {
        ThrowIfDisposed();

        if (!_webViews.TryGetValue(appName, out var webView))
        {
            throw new InvalidOperationException($"WebView2 '{appName}' introuvable. Appelez GetOrCreate d'abord.");
        }

        if (webView.CoreWebView2 is not null)
        {
            return;
        }

        // Crée l'environnement partagé une seule fois pour toutes les apps
        if (_environment is null)
        {
            try
            {
                Directory.CreateDirectory(_userDataFolder);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Impossible de créer le dossier cache : {_userDataFolder}", ex);
            }

            // --auth-server-allowlist / --auth-negotiate-delegate-allowlist avec une valeur
            // bidon (_) coupent l'authentification intégrée Windows (NTLM/Negotiate) : sinon
            // WebView2 transmet automatiquement et silencieusement les identifiants du compte
            // Windows de la session aux sites intranet, exactement comme Edge.
            // msSingleSignOn / msWamBroker + AllowSingleSignOnUsingOSPrimaryAccount=false
            // coupent le SSO vers le compte Microsoft/AAD connecté à l'OS.
            var options = new CoreWebView2EnvironmentOptions(
                additionalBrowserArguments:
                    "--disable-features=msSingleSignOn,msWamBroker,msImplicitSignin,msaSingleSignOn,AadSingleSignOn " +
                    "--no-service-autorun " +
                    "--auth-server-allowlist=_ " +
                    "--auth-negotiate-delegate-allowlist=_")
            {
                AllowSingleSignOnUsingOSPrimaryAccount = false
            };

            _environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: _userDataFolder,
                options: options);

            Trace.WriteLine($"CoreWebView2Environment créé : {_userDataFolder}");
        }

        await webView.EnsureCoreWebView2Async(_environment);

        // Activer la sauvegarde des mots de passe (désactivée par défaut dans WebView2)
        if (webView.CoreWebView2 is { } core)
            core.Settings.IsPasswordAutosaveEnabled = true;

        Trace.WriteLine($"WebView2 initialisé pour '{appName}'");
    }

    /// <summary>
    /// Libère et supprime un WebView2 identifié par sa clé (utile pour la fermeture d'onglet).
    /// </summary>
    public void Remove(string key)
    {
        if (_disposed) return;
        if (!_webViews.TryGetValue(key, out var webView)) return;

        try
        {
            webView.NavigationCompleted -= OnNavigationCompleted;
            webView.CoreWebView2?.Stop();
            webView.Dispose();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Erreur Remove WebView2 '{key}' : {ex.Message}");
        }
        finally
        {
            _webViews.Remove(key);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var webView in _webViews.Values)
        {
            try
            {
                webView.NavigationCompleted -= OnNavigationCompleted;
                webView.CoreWebView2?.Stop();
                webView.Dispose();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Erreur lors de la fermeture WebView2 : {ex}");
            }
        }

        _webViews.Clear();
        _disposed = true;
    }

    /// <summary>
    /// Supprime le dossier de cache WebView2 d'un utilisateur identifié par son SID.
    /// </summary>
    public static void DeleteCacheForSid(string sid, string? cacheRoot = null)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            return;
        }

        var root = cacheRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Hub", "Cache");

        var folder = Path.Combine(root, SanitizePathToken(sid));

        if (Directory.Exists(folder))
        {
            try   { Directory.Delete(folder, recursive: true); }
            catch (Exception ex) { Trace.WriteLine($"Impossible de supprimer le cache : {ex.Message}"); }
        }
    }

    private static string SanitizePathToken(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            builder.Append(Array.IndexOf(invalidChars, c) >= 0 ? '_' : c);
        }

        return builder.ToString();
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        var status = e.IsSuccess ? "OK" : $"Erreur ({e.WebErrorStatus})";
        Trace.WriteLine($"Navigation terminée : {status}");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WebViewHostManager));
        }
    }
}
