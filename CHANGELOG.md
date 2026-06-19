# Changelog

Toutes les evolutions notables du projet sont documentees ici.

## v0.1.1 - 2026-06-19

Correction mineure.

### Fixed

- Ajout de `https://ecable.gouv.bj` dans `Cors:AllowedOrigins` pour autoriser le frontend de production a appeler l'agent local.
- Correction du blocage CORS observe sur le domaine `ecable.gouv.bj`.

### Impact

- Aucun changement sur le pipeline de scan.
- Aucun changement sur le format PDF renvoye.
- Aucun changement sur l'installateur ou la tray app.

## v0.1.0

Premiere version packagee de l'agent Windows local.

### Added

- API locale `GET /health`, `GET /devices`, `POST /scan/pdf`.
- Mode de scan reel via NAPS2.
- Packaging Windows avec tray app et installateur Inno Setup.
