# Nouba Pro v2.1 — Guide de déploiement

## Prérequis
- Windows 10/11 (64 bits)
- .NET 8 Runtime installé : https://dotnet.microsoft.com/download/dotnet/8.0
- Google Chrome ou Microsoft Edge (recommandé pour les bornes et l'affichage TV)

---

## Démarrage rapide

1. Copiez le dossier `Nouba` sur le PC serveur (ex: `C:\Nouba\`)
2. Double-cliquez sur `Nouba.exe`
3. Ouvrez `http://127.0.0.1:5000/Admin` dans votre navigateur
4. Créez votre compte administrateur

---

## Impression silencieuse (borne kiosque)

Pour éviter la boîte de dialogue d'impression, lancez Chrome en mode kiosque :

```
chrome.exe --kiosk --kiosk-printing --app=http://127.0.0.1:5000/Borne
```

Créez un raccourci sur le bureau avec cette commande.

---

## Écran TV en plein écran

```
chrome.exe --kiosk --app=http://127.0.0.1:5000/Display
```

---

## Démarrage automatique au démarrage de Windows

1. Créez un fichier `start_nouba.bat` :
```bat
@echo off
cd /d C:\Nouba
start "" Nouba.exe
timeout /t 3
start chrome --kiosk --kiosk-printing --app=http://127.0.0.1:5000/Borne
```
2. Copiez ce fichier dans :
   `C:\Users\[Utilisateur]\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup`

---

## Accès réseau (multi-postes)

1. Dans Admin → Système → Configuration réseau, mettez `http://0.0.0.0:5000`
2. Redémarrez Nouba.exe
3. Sur les autres postes : `http://[IP_SERVEUR]:5000/Borne` (borne), `/Display` (TV), `/Agent` (agents)

Pour trouver l'IP du serveur : ouvrez `cmd` → tapez `ipconfig` → notez l'adresse IPv4.

---

## Données

- Base de données : `C:\ProgramData\Nouba\nouba.db`
- Sauvegardes auto : `C:\ProgramData\Nouba\backups\` (toutes les 6h, 20 dernières conservées)
- Uploads (logos/médias) : `C:\ProgramData\Nouba\uploads\`

**Sauvegardez régulièrement le dossier `C:\ProgramData\Nouba\` sur un support externe.**

---

## Personnalisation pour le client

Avant livraison, éditez `Infrastructure/LicenseInfo.cs` :
```csharp
public static string ClientName  = "Nom du client";
public static string ClientRef   = "REF-2026-XXX";
public static string LicensedTo  = "Nom affiché";
```

---

© 2026 Nouba Software — support@nouba.dz
