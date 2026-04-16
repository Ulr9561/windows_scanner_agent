# LocalScanAgent

Agent local Windows pour le flux `Scanner une note`.

Le but du projet est simple:
- parler au scanner branché sur le poste Windows
- produire un PDF unique multipage
- renvoyer ce PDF au navigateur
- laisser le frontend reutiliser l'upload existant vers `/api/courriers/arrivees/`

L'agent ne doit pas:
- creer une session backend de scan
- uploader directement le PDF au backend
- introduire un workflow metier parallele a `Importer fichier`

## Flow MVP

`Frontend -> Agent local Windows -> Scanner USB -> PDF unique -> Frontend -> Backend /api/courriers/arrivees/`

## Architecture

- `src/LocalScanAgent.Contracts`: DTO et enums partages
- `src/LocalScanAgent.Application`: orchestration metier et abstractions
- `src/LocalScanAgent.Infrastructure`: scan fake, scan reel NAPS2, export PDF, logging
- `src/LocalScanAgent.Host`: API ASP.NET Core locale, config, CORS, logs
- `src/LocalScanAgent.Tray`: application Windows tray qui lance et surveille le host local
- `tests/LocalScanAgent.Tests`: tests du projet

## Endpoints

- `GET /health`
- `GET /devices`
- `POST /scan/pdf`

### Contrat `GET /health`

Reponse:

```json
{
  "status": "ok",
  "version": "0.1.0",
  "scannerState": "Ready"
}
```

### Contrat `GET /devices`

Reponse:

```json
[
  {
    "id": "wia::{6BDD1FC6-810F-11D0-BEC7-08002BE2092F}\\0000",
    "name": "HP ScanJet Flow 5000 s5 (USB)",
    "driver": "Wia"
  }
]
```

### Contrat `POST /scan/pdf`

Corps attendu par l'API:

```json
{
  "mode": "Real",
  "dpi": 300,
  "paperSource": "Feeder",
  "duplex": false,
  "colorMode": "Grayscale",
  "output": "Pdf"
}
```

Reponse:
- `200 application/pdf`
- header `X-Page-Count`
- header `X-Scan-Mode`

## Parametres UX a exposer

Le frontend devrait exposer seulement:
- `paperSource`
- `duplex`
- `dpi` via un choix de qualite
- `colorMode`

Le frontend ne devrait pas exposer directement:
- `preferredDeviceId`
- `mode`
- `output`
- `simulatedPages`

### Signification des parametres UX

- `paperSource`: `Feeder` pour le chargeur, `Flatbed` pour la vitre
- `duplex`: `true` pour recto-verso, `false` pour recto simple
- `dpi`: resolution de numerisation. `300` est le meilleur defaut documentaire
- `colorMode`: `Grayscale`, `Color`, `BlackAndWhite`

## Configuration

Les fichiers de config actuels sont:
- `src/LocalScanAgent.Host/appsettings.json`: base et configuration locale par defaut
- `src/LocalScanAgent.Host/appsettings.Development.json`: dev locale, logs verbeux, CORS devtunnel
- `src/LocalScanAgent.Host/appsettings.Test.json`: mode fake et logs de test

Sections importantes:
- `Agent.BindAddress`
- `Agent.Port`
- `Agent.Mode`
- `Agent.PreferredDriverOrder`
- `Agent.Quality`
- `Cors.AllowedOrigins`
- `Logging.Path`

## Lancement

En developpement:

```powershell
dotnet run --project .\src\LocalScanAgent.Host\LocalScanAgent.Host.csproj
```

Build solution:

```powershell
dotnet build .\LocalScanAgent.slnx
```

L'API ecoute par defaut sur:

```text
http://127.0.0.1:18765
```

## Packaging Windows

Le livrable utilisateur final vise maintenant ce schema:
- `LocalScanAgent.Tray.exe` dans la zone de notification Windows
- `LocalScanAgent.Host.exe` lance en arriere-plan par la tray
- un installateur `Setup.exe` pour installer le tout sur un autre poste

Le packaging est prepare dans:
- `scripts/Publish-Installer.ps1`
- `installer/LocalScanAgent.iss`

### Generer le staging de l'installateur

Si le `publish` du host fonctionne normalement sur ta machine:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Installer.ps1 -Version 0.1.0 -SkipInstaller
```

Si ton SDK local a encore le probleme `MSB4276` mais que tu as deja un host publie valide:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Installer.ps1 `
  -Version 0.1.0 `
  -HostPublishDir .\publish\LocalScanAgent-win-x64 `
  -SkipInstaller
```

Le contenu pret a etre empaquete sera dans:

```text
artifacts\installer\app
```

### Generer le Setup.exe

Installer d'abord Inno Setup sur le poste de build, puis lancer:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Installer.ps1 -Version 0.1.0
```

Sortie attendue:

```text
artifacts\installer\output\LocalScanAgentSetup-0.1.0.exe
```

### Ce que fait l'installateur

- installe l'application dans `%LocalAppData%\Programs\LocalScanAgent`
- ajoute un raccourci menu demarrer
- peut ajouter le lancement automatique a l'ouverture de session
- lance la tray app a la fin de l'installation

### Ce que l'installateur ne fait pas

- n'installe pas les pilotes constructeur du scanner
- ne configure pas automatiquement TWAIN/WIA
- ne remplace pas la verification materielle sur le poste cible

## Scanner valide jusqu'ici

Scanner verifie manuellement:
- `HP ScanJet Flow 5000 s5 (USB)`

Driver reel valide a date:
- `WIA`

Constat actuel:
- la detection WIA fonctionne
- la numerisation reelle fonctionne
- le multi-pages via chargeur fonctionne
- TWAIN n'a pas encore ete detecte sur le poste actuel

## Erreurs materielles prises en charge

Exemples d'erreurs attendues:
- chargeur vide
- scanner introuvable
- scanner indisponible
- erreur de numerisation

Format stable pour les erreurs `ProblemDetails`:
- `title`
- `detail`
- `status`
- `errorCode`

## Frontend

Le frontend doit:
1. appeler l'agent local
2. recuperer le PDF comme `Blob`
3. le convertir en `File`
4. reutiliser la logique d'upload existante vers `/api/courriers/arrivees/`

Le frontend ne doit pas:
- parler directement au scanner
- changer le flow metier backend

## Voir aussi

- [docs/HANDOFF.md](C:/Users/uadegoke/Documents/windows_scanner_agent/docs/HANDOFF.md)
- [docs/TESTING.md](C:/Users/uadegoke/Documents/windows_scanner_agent/docs/TESTING.md)
