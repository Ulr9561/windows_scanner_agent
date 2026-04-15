# Local Scan Agent: Init + Handoff

## But

Initier sous Linux un nouveau projet .NET pour l'agent local Windows du module
`Scanner une note`, puis transférer ce projet sur Windows pour continuer le
développement avec un autre agent.

## Flow retenu

Le flow MVP a déjà été figé et ne doit pas être changé pendant
l'initialisation:

`Web app -> Agent local Windows -> Scanner USB -> PDF unique -> Navigateur -> Backend /api/courriers/arrivees/ -> ProcessFichiers`

Conséquences:

- l'agent local ne crée pas de session backend de scan;
- l'agent local n'upload pas directement vers Django;
- l'agent local renvoie un seul PDF au navigateur;
- le frontend fait ensuite le même upload que `Importer fichier`;
- après l'upload, le flow métier doit rester exactement celui de
  `ImportFichier -> ProcessFichiers`.

## Ce que le backend fait deja

Le backend existant accepte déjà un PDF via:

- `POST /api/courriers/arrivees/`

Il crée ensuite le `Courrier`, stocke le PDF, lance l'extraction, puis renvoie
le document créé.

Donc, pour l'agent local:

- pas de `scanSessionId` backend;
- pas de `uploadUrl`;
- pas de `uploadToken`;
- pas de polling backend depuis l'agent.

## Ce que l'agent local doit exposer

API locale minimum:

- `GET /health`
- `GET /devices`
- `POST /scan/pdf`

Exigences:

- un seul scan simultané;
- PDF unique multipage uniquement;
- mode `fake` obligatoire;
- CORS limité à l'origine du frontend;
- écoute sur `127.0.0.1`;
- idéalement HTTPS local pour éviter les problèmes si le frontend est servi en
  HTTPS via devtunnel.

## Strategie Linux -> Windows recommandee

### Recommandation

Initialiser sur Linux uniquement le socle cross-platform:

- solution;
- host ASP.NET Core Minimal API;
- bibliothèques métier;
- contrats;
- infrastructure;
- tests.

Reporter la partie tray Windows sur la machine Windows.

### Pourquoi

Le tray icon repose sur des APIs Windows. Le coeur scan/orchestration peut être
préparé proprement sur Linux, puis la couche tray peut être ajoutée ensuite
sans casser l'architecture.

### Important

Sous Linux, le SDK .NET n'est pas installé dans cet environnement Codex au
moment de la rédaction. Les commandes ci-dessous sont donc fournies comme plan
d'initialisation, mais n'ont pas été exécutées ici.

## Requirements

### Linux

- .NET 8 SDK
- git
- éditeur de code
- accès internet pour restaurer les packages NuGet

### Windows, plus tard

- .NET 8 SDK
- pilote HP installé
- scanner HP branché en USB
- certificat dev HTTPS local approuvé si l'API locale est exposée en HTTPS
- environnement de test avec frontend/backend exposés via devtunnels

### Packages .NET cibles

Packages à prévoir:

- `NAPS2.Sdk`
- `NAPS2.Images.ImageSharp`
- `NAPS2.Sdk.Worker.Win32`
- `Serilog.AspNetCore`
- `Serilog.Sinks.File`

Notes:

- `NAPS2.Sdk` est le package coeur.
- `NAPS2.Images.ImageSharp` fournit le contexte image cross-platform.
- `NAPS2.Sdk.Worker.Win32` est nécessaire pour le worker Win32 côté Windows,
  utile pour TWAIN depuis un process 64 bits.

## Nom et emplacement du projet

Créer un repo séparé, pas dans le frontend ni dans le backend Django.

Nom recommandé:

- `LocalScanAgent`

Exemple d'emplacement:

- `~/Projects/LocalScanAgent`

## Structure recommandee

```text
LocalScanAgent/
  LocalScanAgent.sln
  Directory.Build.props
  README.md
  docs/
    HANDOFF.md
    TESTING.md
  src/
    LocalScanAgent.Host/
    LocalScanAgent.Application/
    LocalScanAgent.Contracts/
    LocalScanAgent.Infrastructure/
  tests/
    LocalScanAgent.Tests/
```

## Commandes d'initialisation sous Linux

Depuis un terminal Linux:

```bash
mkdir -p ~/Projects/LocalScanAgent
cd ~/Projects/LocalScanAgent

dotnet new sln -n LocalScanAgent

mkdir -p src tests docs

dotnet new web -n LocalScanAgent.Host -o src/LocalScanAgent.Host
dotnet new classlib -n LocalScanAgent.Application -o src/LocalScanAgent.Application
dotnet new classlib -n LocalScanAgent.Contracts -o src/LocalScanAgent.Contracts
dotnet new classlib -n LocalScanAgent.Infrastructure -o src/LocalScanAgent.Infrastructure
dotnet new xunit -n LocalScanAgent.Tests -o tests/LocalScanAgent.Tests

dotnet sln add src/LocalScanAgent.Host/LocalScanAgent.Host.csproj
dotnet sln add src/LocalScanAgent.Application/LocalScanAgent.Application.csproj
dotnet sln add src/LocalScanAgent.Contracts/LocalScanAgent.Contracts.csproj
dotnet sln add src/LocalScanAgent.Infrastructure/LocalScanAgent.Infrastructure.csproj
dotnet sln add tests/LocalScanAgent.Tests/LocalScanAgent.Tests.csproj
```

## Références entre projets

```bash
dotnet add src/LocalScanAgent.Application/LocalScanAgent.Application.csproj reference src/LocalScanAgent.Contracts/LocalScanAgent.Contracts.csproj

dotnet add src/LocalScanAgent.Infrastructure/LocalScanAgent.Infrastructure.csproj reference src/LocalScanAgent.Application/LocalScanAgent.Application.csproj
dotnet add src/LocalScanAgent.Infrastructure/LocalScanAgent.Infrastructure.csproj reference src/LocalScanAgent.Contracts/LocalScanAgent.Contracts.csproj

dotnet add src/LocalScanAgent.Host/LocalScanAgent.Host.csproj reference src/LocalScanAgent.Application/LocalScanAgent.Application.csproj
dotnet add src/LocalScanAgent.Host/LocalScanAgent.Host.csproj reference src/LocalScanAgent.Contracts/LocalScanAgent.Contracts.csproj
dotnet add src/LocalScanAgent.Host/LocalScanAgent.Host.csproj reference src/LocalScanAgent.Infrastructure/LocalScanAgent.Infrastructure.csproj

dotnet add tests/LocalScanAgent.Tests/LocalScanAgent.Tests.csproj reference src/LocalScanAgent.Application/LocalScanAgent.Application.csproj
dotnet add tests/LocalScanAgent.Tests/LocalScanAgent.Tests.csproj reference src/LocalScanAgent.Contracts/LocalScanAgent.Contracts.csproj
dotnet add tests/LocalScanAgent.Tests/LocalScanAgent.Tests.csproj reference src/LocalScanAgent.Infrastructure/LocalScanAgent.Infrastructure.csproj
```

## Packages NuGet

### Infrastructure

```bash
dotnet add src/LocalScanAgent.Infrastructure/LocalScanAgent.Infrastructure.csproj package NAPS2.Sdk
dotnet add src/LocalScanAgent.Infrastructure/LocalScanAgent.Infrastructure.csproj package NAPS2.Images.ImageSharp
dotnet add src/LocalScanAgent.Infrastructure/LocalScanAgent.Infrastructure.csproj package NAPS2.Sdk.Worker.Win32
```

### Host

```bash
dotnet add src/LocalScanAgent.Host/LocalScanAgent.Host.csproj package Serilog.AspNetCore
dotnet add src/LocalScanAgent.Host/LocalScanAgent.Host.csproj package Serilog.Sinks.File
```

## Fichiers minimum à créer juste après

### `Directory.Build.props`

Contenu recommandé:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

### `README.md`

Le README doit rappeler:

- le flow retenu;
- que l'agent renvoie le PDF au frontend;
- que le frontend upload vers `/api/courriers/arrivees/`;
- que le backend de scan-session n'existe pas dans ce MVP.

### `docs/HANDOFF.md`

Ce fichier doit servir au prochain agent Windows.

Il doit contenir:

- le flow métier exact;
- la structure de solution;
- les endpoints attendus;
- la stratégie CORS avec devtunnels;
- la liste des tâches restantes.

## Contrats cibles à garder en tête

### `GET /health`

```json
{
  "status": "ok",
  "version": "0.1.0",
  "scannerState": "ready"
}
```

### `GET /devices`

```json
[
  {
    "id": "hp-7000s3-twain",
    "name": "HP ScanJet Enterprise Flow 7000 s3",
    "driver": "twain"
  }
]
```

### `POST /scan/pdf`

Requête:

```json
{
  "mode": "real",
  "preferredDeviceId": "hp-7000s3-twain",
  "dpi": 300,
  "paperSource": "feeder",
  "duplex": true,
  "colorMode": "grayscale",
  "output": "pdf"
}
```

Réponse recommandée:

- `200 application/pdf`
- `Content-Disposition: attachment; filename="scan_note.pdf"`
- `X-Page-Count: 7`

Mode fake:

```json
{
  "mode": "fake",
  "simulatedPages": 5,
  "dpi": 300,
  "paperSource": "feeder",
  "duplex": true,
  "colorMode": "grayscale",
  "output": "pdf"
}
```

## Configuration à prévoir

### `appsettings.Development.json`

Clés recommandées:

```json
{
  "Agent": {
    "BindAddress": "127.0.0.1",
    "Port": 18765,
    "AllowOnlyOneScanAtATime": true,
    "PreferredDriverOrder": ["twain", "wia"],
    "TempRoot": "temp",
    "Mode": "real"
  },
  "Cors": {
    "AllowedOrigins": []
  },
  "Logging": {
    "Path": "logs/agent-.log"
  }
}
```

## Devtunnels et tests

Le frontend et le backend seront exposés via devtunnels.

Conséquences pour l'agent:

- l'origine frontend sera une URL publique `https://...devtunnels.ms`;
- cette origine doit être ajoutée à `Cors:AllowedOrigins`;
- l'agent n'a pas besoin de connaître l'URL backend pour le MVP;
- le backend reste appelé par le navigateur, pas par l'agent.

## Premier découpage de code

### `LocalScanAgent.Contracts`

Y mettre:

- `HealthResponse`
- `DeviceDto`
- `ScanPdfRequest`
- `ScanMode`
- `DriverKind`

### `LocalScanAgent.Application`

Y mettre:

- `ScanOrchestrator`
- interfaces métier:
  - `IScanSource`
  - `IPdfService`
  - `IAgentLogger`

### `LocalScanAgent.Infrastructure`

Y mettre:

- `FakeScanSource`
- `Naps2ScanSource`
- `PdfService`
- `AgentLogger`

### `LocalScanAgent.Host`

Y mettre:

- Minimal API
- configuration
- CORS
- Serilog
- endpoints `/health`, `/devices`, `/scan/pdf`

## Strategie tray Windows

Ne pas bloquer l'init Linux avec le tray.

Plan recommandé:

1. Initialiser et coder le coeur cross-platform d'abord.
2. Sur Windows, retargeter le host si nécessaire.
3. Ajouter le tray ensuite.

Deux options côté Windows:

- soit convertir `LocalScanAgent.Host` en host Windows avec tray;
- soit ajouter un projet séparé `LocalScanAgent.Tray` qui démarre le host.

Option la plus simple pour l'agent Windows suivant:

- démarrer avec le host API;
- ajouter un projet `LocalScanAgent.Tray` plus tard si besoin.

## Si vous voulez préparer le ciblage Windows plus tard

Quand le développement reprend sur Windows, si un projet doit cibler Windows
depuis une machine non-Windows, Microsoft indique qu'il faut activer
`EnableWindowsTargeting`.

Mais pour l'init Linux de ce MVP, il est préférable de rester sur `net8.0`
pour les projets coeur, puis de faire le ciblage Windows ensuite.

## Tâches prioritaires pour l'agent Windows suivant

1. Restaurer la solution et vérifier que tout compile.
2. Implémenter le mode fake en premier.
3. Exposer `/health`.
4. Exposer `/devices`.
5. Exposer `/scan/pdf` en mode fake.
6. Tester depuis le frontend déjà existant.
7. Ajouter ensuite la détection réelle scanner.
8. Brancher TWAIN, puis fallback WIA.
9. Ajouter logs.
10. Ajouter tray.

## Points à ne pas changer

Ne pas dériver vers:

- un upload direct agent -> backend;
- une création de session backend de scan;
- plusieurs PDF par session;
- un workflow séparé de `Importer fichier`.

Le coeur du MVP est:

- l'agent produit un unique `File` PDF;
- le frontend réutilise l'upload existant;
- le backend existant continue de traiter comme un import classique.

## Sources utiles

- NAPS2 SDK packages et architecture modulaire:
  https://www.naps2.com/sdk/doc/api/
- Exemple NAPS2 scan -> PDF:
  https://www.naps2.com/sdk
- .NET Windows targeting depuis Linux/macOS:
  https://learn.microsoft.com/en-us/dotnet/core/tools/sdk-errors/netsdk1100
