# Testing

## Prerequis

- .NET 10 SDK
- scanner USB branche si test reel
- pilote constructeur installe si necessaire
- agent configure en `Mode = Real` pour les tests materiels

## Environnements

- `Development`: scan local avec logs verbeux et CORS de dev
- `Test`: mode fake, utile pour les tests de contrat HTTP
- base `appsettings.json`: configuration locale par defaut

Pour lancer en mode test:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Test"
dotnet run --project .\src\LocalScanAgent.Host\LocalScanAgent.Host.csproj
```

## Build

```powershell
dotnet build .\LocalScanAgent.slnx
```

## Run

```powershell
dotnet run --project .\src\LocalScanAgent.Host\LocalScanAgent.Host.csproj
```

L'API ecoute par defaut sur:

```text
http://127.0.0.1:18765
```

## Packaging / Tray

Le mode deployable utilisateur final repose sur:
- `LocalScanAgent.Host.exe`
- `LocalScanAgent.Tray.exe`
- un installateur Inno Setup

### Staging du package

Si le host peut encore etre publie localement:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Installer.ps1 -Version 0.1.0 -SkipInstaller
```

Si le SDK local bloque sur `MSB4276`, reutiliser un host deja publie:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Installer.ps1 `
  -Version 0.1.0 `
  -HostPublishDir .\publish\LocalScanAgent-win-x64 `
  -SkipInstaller
```

Verifier ensuite:
- `artifacts\installer\app\LocalScanAgent.Tray.exe`
- `artifacts\installer\app\host\LocalScanAgent.Host.exe`

### Build du Setup.exe

Prerequis supplementaire:
- Inno Setup installe avec `iscc.exe` disponible

Commande:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Installer.ps1 -Version 0.1.0
```

Sortie attendue:
- `artifacts\installer\output\LocalScanAgentSetup-0.1.0.exe`

### Verification manuelle de la tray

Apres installation:
- verifier qu'une icone tray `Local Scan Agent` apparait
- verifier que le menu permet `Demarrer`, `Arreter`, `Redemarrer`
- verifier que `GET /health` repond une fois la tray lancee
- verifier que la fermeture via `Quitter` arrete aussi le host

## Test `GET /health`

```powershell
Invoke-RestMethod -Uri "http://127.0.0.1:18765/health"
```

Attendu:
- `status = ok`
- `scannerState = Ready`

## Test `GET /devices`

```powershell
Invoke-RestMethod -Uri "http://127.0.0.1:18765/devices"
```

Attendu:
- mode fake: au moins un faux scanner
- mode real: scanner reel detecte si disponible

## Test `POST /scan/pdf` en fake

```powershell
$body = @{
  mode = "Fake"
  dpi = 300
  paperSource = "Feeder"
  duplex = $false
  colorMode = "Grayscale"
  output = "Pdf"
  simulatedPages = 3
} | ConvertTo-Json

$response = Invoke-WebRequest `
  -Uri "http://127.0.0.1:18765/scan/pdf" `
  -Method Post `
  -ContentType "application/json" `
  -Body $body `
  -OutFile ".\scan_fake.pdf"

$response.Headers["X-Page-Count"]
$response.Headers["X-Scan-Mode"]
```

## Test `POST /scan/pdf` en reel

```powershell
$body = @{
  mode = "Real"
  dpi = 300
  paperSource = "Feeder"
  duplex = $false
  colorMode = "Grayscale"
  output = "Pdf"
} | ConvertTo-Json

$response = Invoke-WebRequest `
  -Uri "http://127.0.0.1:18765/scan/pdf" `
  -Method Post `
  -ContentType "application/json" `
  -Body $body `
  -OutFile ".\scan_real.pdf"

$response.Headers["X-Page-Count"]
$response.Headers["X-Scan-Mode"]
```

## Test vitre / flatbed

```powershell
$body = @{
  mode = "Real"
  dpi = 300
  paperSource = "Flatbed"
  duplex = $false
  colorMode = "Grayscale"
  output = "Pdf"
} | ConvertTo-Json

$response = Invoke-WebRequest `
  -Uri "http://127.0.0.1:18765/scan/pdf" `
  -Method Post `
  -ContentType "application/json" `
  -Body $body `
  -OutFile ".\scan_flatbed.pdf"
```

## Test d'erreur chargeur vide

Sans feuille dans le chargeur:

```powershell
$body = @{
  mode = "Real"
  dpi = 300
  paperSource = "Feeder"
  duplex = $false
  colorMode = "Grayscale"
  output = "Pdf"
} | ConvertTo-Json

try {
  Invoke-WebRequest `
    -Uri "http://127.0.0.1:18765/scan/pdf" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body
}
catch {
  $_.Exception.Response.StatusCode.value__
  $_.ErrorDetails.Message
}
```

Attendu:
- status `409`
- `errorCode = scanner_feeder_empty`

## Frontend

Le frontend doit exposer seulement:
- source papier
- recto/verso
- qualite
- couleur

Le frontend ne doit pas exposer:
- `preferredDeviceId`
- `mode`
- `output`
- `simulatedPages`

## Brancher le frontend

Le frontend appelle l'agent local, recupere le PDF, puis reutilise l'upload existant vers le backend.

Sequence:
1. appeler l'agent local
2. recuperer le PDF comme `Blob`
3. convertir en `File`
4. reutiliser le flow `/api/courriers/arrivees/`

## Scanner et driver valides a date

- scanner valide: `HP ScanJet Flow 5000 s5 (USB)`
- driver reel valide: `WIA`
- TWAIN: non detecte a ce stade sur le poste teste
