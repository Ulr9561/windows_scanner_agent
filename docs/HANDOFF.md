# Handoff

## Objectif

Ce repo contient l'agent local Windows du flux `Scanner une note`.

Mission de l'agent:
- detecter un scanner local
- numeriser des pages
- produire un PDF unique
- renvoyer ce PDF au navigateur

Ce que l'agent ne doit pas faire:
- upload direct backend
- creation de session de scan backend
- modification du workflow metier d'import existant

## Flow metier

`Frontend -> Agent local Windows -> Scanner USB -> PDF unique -> Frontend -> Backend /api/courriers/arrivees/`

## Endpoints attendus

- `GET /health`
- `GET /devices`
- `POST /scan/pdf`

## Contrat HTTP stabilise

### `GET /health`

Retourne un JSON simple:
- `status`
- `version`
- `scannerState`

### `GET /devices`

Retourne la liste des scanners detectes:
- `id`
- `name`
- `driver`

Le frontend n'a pas besoin d'exposer cette liste a l'utilisateur dans le MVP si l'agent choisit automatiquement le scanner.

### `POST /scan/pdf`

Requete utilisateur utile:
- `paperSource`
- `duplex`
- `dpi`
- `colorMode`

Parametres internes a garder cote agent:
- `preferredDeviceId`
- `mode`
- `output`
- `simulatedPages`

Headers de succes:
- `X-Page-Count`
- `X-Scan-Mode`

Format d'erreur stable:
- `ProblemDetails`
- `status`
- `title`
- `detail`
- `errorCode`

## Configuration

Fichiers:
- `appsettings.json`: base locale
- `appsettings.Development.json`: dev locale et devtunnel
- `appsettings.Test.json`: mode fake pour tests

Champs sensibles:
- `Agent.BindAddress`
- `Agent.Port`
- `Agent.Mode`
- `Agent.PreferredDriverOrder`
- `Agent.Quality`
- `Cors.AllowedOrigins`
- `Logging.Path`

## Drivers et etat reel

Etat verifie sur le poste actuel:
- scanner detecte en `WIA`
- `TWAIN` non detecte pour l'instant
- scanner valide: `HP ScanJet Flow 5000 s5 (USB)`

## Strategie CORS

L'agent n'ecoute que sur `127.0.0.1`.

Si le frontend tourne via devtunnel:
- ajouter l'origine publique dans `Cors.AllowedOrigins`
- l'agent n'a pas besoin de connaitre l'URL backend

## Taches restantes principales

- ameliorer encore la qualite documentaire
- valider le comportement TWAIN si un pilote est installe
- ajouter les tests utiles
- finaliser la documentation de deploiement
- preparer le packaging Windows `dotnet publish`
