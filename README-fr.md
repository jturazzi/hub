# Hub

Portail bureautique Windows donnant acces aux applications web internes, concu pour les postes a **session Windows partagee**.

---

## Problematique

Sur un poste partage, plusieurs utilisateurs tournent sous le **meme compte Windows**. Un navigateur classique (Chrome, Edge...) ne cloisonne pas les sessions : l'utilisateur suivant herite des cookies, de la messagerie ouverte et des mots de passe enregistres du precedent.

Hub resout ce probleme :

- **Authentification AD obligatoire** a chaque lancement, avec test prealable de joignabilite du controleur de domaine (ping ICMP + bind LDAP)
- **Cache WebView2 isole par SID** - chaque utilisateur AD possede son propre profil de navigation dans `%LocalAppData%\Hub\Cache\{SID}\`
- **Deconnexion explicite** - ferme toutes les sessions web actives
- **Tracabilite** - chaque connexion, deconnexion, ajout et suppression de profil est enregistre dans un CSV mensuel

---

## Fonctionnalites

### Authentification
- Authentification LDAP / Active Directory
- **Test de joignabilite AD** avant la tentative (ping + bind LDAP) : message clair si le serveur est inaccessible plutot qu''un faux "identifiants invalides"
- Memorisation des comptes precedemment connectes (fichier chiffre DPAPI)
- Selection rapide via une liste deroulante
- Lien **"Premiere connexion"** configurable
- Suppression d''un profil : efface egalement le cache WebView2 associe

### Navigateur integre
- **Multi-onglets** independants : chaque onglet possede sa propre instance WebView2 et son historique
- **Page d''accueil interne** avec grille de cartes des favoris
- **Bouton Accueil** dans la barre de navigation
- **Bouton "+"** pour ouvrir un nouvel onglet sur la page d''accueil
- **Ouverture automatique au demarrage** des favoris marques `OpenOnStartup` (avec prechargement)
- Barre d''onglets defilable a la molette
- Sauvegarde automatique des mots de passe WebView2

### Interface
- Traduction automatique francais / anglais selon la langue systeme
- Design clair et compact (deux barres : onglets + navigation)
- Avatar utilisateur + bouton deconnexion dans la barre principale

### Audit
- Journal CSV mensuel dans `%LocalAppData%\Hub\Logs\`
- Evenements traces : CONNEXION_OK, CONNEXION_ECHEC, DECONNEXION, FERMETURE, AJOUT_UTILISATEUR, SUPPRESSION_UTILISATEUR

---

## Stack technique

| Composant | Detail |
|---|---|
| Framework | .NET 8, WPF |
| Navigateur embarque | Microsoft WebView2 |
| Authentification | Active Directory - `System.DirectoryServices.AccountManagement` |
| Test reseau | ICMP via `System.Net.NetworkInformation.Ping` |
| Configuration | JSON (`hub.config.json`) |
| Deploiement | `PublishSingleFile`, necessite le runtime .NET 8 Desktop |

---

## Structure du projet

```
Hub/
- Models/
  - AuthenticatedUser.cs
  - HubConfig.cs              # contient AppEntry avec OpenOnStartup
  - SavedUser.cs
- Resources/
  - Strings.fr.resx
  - Strings.en.resx
- Services/
  - ConnectionLogService.cs   # journalisation CSV (6 evenements)
  - HubConfigService.cs
  - L10n.cs
  - LdapAuthService.cs        # IsReachable() + Authenticate()
  - UserHistoryService.cs     # DPAPI, Save() retourne true si nouvel utilisateur
  - WebViewHostManager.cs     # gestion par cle d''onglet + Remove()
- LoginWindow.xaml(.cs)
- MainWindow.xaml(.cs)        # multi-onglets, page accueil HTML
- hub.config.json
- App.xaml(.cs)
```

---

## Configuration (`hub.config.json`)

```json
{
  "AppTitle": "ANEF Loire",
  "Domain": "anefloire.lan",
  "CacheFolder": null,
  "FirstConnectionUrl": "https://intranet.company.lan/premiere-connexion",
  "Apps": [
    { "Name": "Google", "Url": "https://google.fr", "OpenOnStartup": true },
    { "Name": "Test 1", "Url": "https://test1.com/" },
    { "Name": "Test 2", "Url": "https://test2.com/" }
  ]
}
```

| Champ | Description |
|---|---|
| `AppTitle` | Nom affiche dans la barre de titre et sur l''ecran de connexion |
| `Domain` | Domaine AD utilise pour l''authentification |
| `CacheFolder` | Dossier racine du cache WebView2. `null` = `%LocalAppData%\Hub\Cache\` |
| `FirstConnectionUrl` | URL ouverte par le lien "Premiere connexion". `null` masque le lien |
| `Apps` | Liste des favoris |
| `Apps[].Name` | Libelle du favori et titre par defaut de l''onglet |
| `Apps[].Url` | URL chargee a l''ouverture |
| `Apps[].OpenOnStartup` | `true` pour ouvrir ce favori dans un onglet au lancement (optionnel) |

**Comportement au demarrage :**
- Si **aucun** favori n''a `OpenOnStartup: true` -> un seul onglet "Accueil" (hub) s''ouvre
- Si **au moins un** favori a `OpenOnStartup: true` -> seuls ces favoris sont ouverts (le 1er actif), pas d''onglet d''accueil supplementaire

---

## Journal (`%LocalAppData%\Hub\Logs\`)

Fichier CSV mensuel : `connexions-YYYY-MM.csv`

```
Date;Heure;Evenement;Login
2025-06-10;08:30:00;AJOUT_UTILISATEUR;jdupont
2025-06-10;08:32:14;CONNEXION_OK;jdupont
2025-06-10;08:45:01;CONNEXION_ECHEC;jmartin
2025-06-10;09:12:33;DECONNEXION;jdupont
2025-06-10;09:14:00;FERMETURE;jmartin
2025-06-10;14:05:22;SUPPRESSION_UTILISATEUR;jmartin
```

| Evenement | Signification |
|---|---|
| `CONNEXION_OK` | Authentification reussie |
| `CONNEXION_ECHEC` | Identifiants invalides |
| `DECONNEXION` | Clic sur le bouton Deconnexion |
| `FERMETURE` | Fenetre fermee sans deconnexion |
| `AJOUT_UTILISATEUR` | Nouveau compte memorise apres 1ere connexion reussie |
| `SUPPRESSION_UTILISATEUR` | Profil supprime via le bouton X de l''ecran de connexion |

---

## Installation

Generer l''installateur via le script PowerShell :

```powershell
.\installer\Build-Installer.ps1
```

Le setup genere (`installer\Output\Hub-Setup.exe`) :
- Verifie la presence du runtime .NET 8 Desktop et le telecharge si absent
- Installe l''application dans `%ProgramFiles%\Hub\`
- Cree un raccourci sur le bureau public

---

## Traduction

Les textes de l''interface sont dans `Resources/Strings.{lang}.resx`.  
La langue est detectee automatiquement au demarrage (`fr` ou `en` par defaut).  
Pour ajouter une langue, creer `Resources/Strings.es.resx` et l''inclure dans le projet.
