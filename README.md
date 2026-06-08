# Hub

A Windows desktop portal giving access to internal web applications, designed for **shared Windows sessions**.

---

## Problem

On a shared workstation, multiple users run under the **same Windows account**. A standard browser (Chrome, Edge...) does not isolate sessions: the next user inherits cookies, open mailboxes and saved passwords from the previous one.

Hub solves this by:

- **Mandatory AD authentication** on every launch, with a pre-check (ICMP ping + LDAP bind) to make sure the domain controller is reachable
- **WebView2 cache isolated by SID** - each AD user has their own browsing profile stored in `%LocalAppData%\Hub\Cache\{SID}\`
- **Explicit sign-out** - closes all active web sessions
- **Audit trail** - every login, sign-out, profile add and profile removal is written to a monthly CSV log

---

## Features

### Authentication
- LDAP / Active Directory authentication
- **AD reachability test** before the attempt (ping + LDAP bind): clear message if the server is unreachable instead of a misleading "invalid credentials"
- Previously connected accounts remembered (DPAPI-encrypted file)
- Quick account selection via a dropdown
- Configurable **"First connection"** link
- Removing a profile also clears its WebView2 cache

### Embedded browser
- **Independent multi-tabs**: each tab owns its own WebView2 instance and history
- **Internal home page** rendered locally as a grid of favorite cards
- **Home button** in the navigation bar
- **"+" button** to open a new tab on the home page
- **Auto-open on startup** for favorites marked with `OpenOnStartup` (with preloading)
- Tab bar scrollable with the mouse wheel
- Automatic password saving in WebView2

### UI
- Auto-translated (French / English based on system locale)
- Clean and compact design (two bars: tabs + navigation)
- User avatar + sign-out button in the main bar

### Audit
- Monthly CSV audit log in `%LocalAppData%\Hub\Logs\`
- Logged events: CONNEXION_OK, CONNEXION_ECHEC, DECONNEXION, FERMETURE, AJOUT_UTILISATEUR, SUPPRESSION_UTILISATEUR

---

## Tech stack

| Component | Detail |
|---|---|
| Framework | .NET 8, WPF |
| Embedded browser | Microsoft WebView2 |
| Authentication | Active Directory - `System.DirectoryServices.AccountManagement` |
| Network probe | ICMP via `System.Net.NetworkInformation.Ping` |
| Configuration | JSON (`hub.config.json`) |
| Deployment | `PublishSingleFile`, requires .NET 8 Desktop Runtime |

---

## Project structure

```
Hub/
- Models/
  - AuthenticatedUser.cs
  - HubConfig.cs              # AppEntry now has OpenOnStartup
  - SavedUser.cs
- Resources/
  - Strings.fr.resx
  - Strings.en.resx
- Services/
  - ConnectionLogService.cs   # CSV audit logging (6 events)
  - HubConfigService.cs
  - L10n.cs
  - LdapAuthService.cs        # IsReachable() + Authenticate()
  - UserHistoryService.cs     # DPAPI, Save() returns true on new user
  - WebViewHostManager.cs     # per-tab key + Remove()
- LoginWindow.xaml(.cs)
- MainWindow.xaml(.cs)        # multi-tabs, internal HTML home page
- hub.config.json
- App.xaml(.cs)
```

---

## Configuration (`hub.config.json`)

```json
{
  "AppTitle": "Company",
  "Domain": "company.lan",
  "CacheFolder": null,
  "FirstConnectionUrl": "https://intranet.company.lan/first-connection",
  "Apps": [
    { "Name": "Google", "Url": "https://google.fr", "OpenOnStartup": true },
    { "Name": "Test 1", "Url": "https://test1.com/" },
    { "Name": "Test 2", "Url": "https://test2.com/" }
  ]
}
```

| Field | Description |
|---|---|
| `AppTitle` | Shown in the title bar and on the login screen |
| `Domain` | AD domain used for authentication |
| `CacheFolder` | Root folder for WebView2 cache. `null` = `%LocalAppData%\Hub\Cache\` |
| `FirstConnectionUrl` | URL opened by the "First connection" link on the login screen. `null` hides the link |
| `Apps` | List of favorites |
| `Apps[].Name` | Favorite label and default tab title |
| `Apps[].Url` | URL loaded when opened |
| `Apps[].OpenOnStartup` | `true` to open this favorite automatically on launch (optional) |

**Startup behavior:**
- If **no** favorite has `OpenOnStartup: true` -> a single "Home" tab opens
- If **at least one** favorite has `OpenOnStartup: true` -> only those favorites are opened (first one active), no extra Home tab

---

## Audit log (`%LocalAppData%\Hub\Logs\`)

Monthly CSV file: `connexions-YYYY-MM.csv`

```
Date;Heure;Evenement;Login
2025-06-10;08:30:00;AJOUT_UTILISATEUR;jdupont
2025-06-10;08:32:14;CONNEXION_OK;jdupont
2025-06-10;08:45:01;CONNEXION_ECHEC;jmartin
2025-06-10;09:12:33;DECONNEXION;jdupont
2025-06-10;09:14:00;FERMETURE;jmartin
2025-06-10;14:05:22;SUPPRESSION_UTILISATEUR;jmartin
```

| Event | Meaning |
|---|---|
| `CONNEXION_OK` | Successful authentication |
| `CONNEXION_ECHEC` | Invalid credentials |
| `DECONNEXION` | User clicked Sign out |
| `FERMETURE` | Window closed without signing out |
| `AJOUT_UTILISATEUR` | New account saved after first successful login |
| `SUPPRESSION_UTILISATEUR` | Profile removed via the X button on the login screen |

---

## Installation

Build the installer using the PowerShell script:

```powershell
.\installer\Build-Installer.ps1
```

The generated setup (`installer\Output\Hub-Setup.exe`):
- Checks for the .NET 8 Desktop Runtime and downloads it if missing
- Installs the application to `%ProgramFiles%\Hub\`
- Creates a public desktop shortcut

---

## Translations

UI strings are stored in `Resources/Strings.{lang}.resx`.  
The language is auto-detected at startup (`fr` or `en` fallback).  
To add a language, create `Resources/Strings.es.resx` and include it in the project.
