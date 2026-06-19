# Release Notes

Chaque release taggee `vX.Y.Z` doit avoir son fichier de notes ici:

- `.github/release-notes/v0.1.1.md`
- `.github/release-notes/v0.1.2.md`
- `.github/release-notes/v0.2.0.md`

Le workflow GitHub lit automatiquement le fichier correspondant au tag pousse.

## Procedure

1. Copier `TEMPLATE.md` vers un nouveau fichier `vX.Y.Z.md`.
2. Remplir les notes de version.
3. Committer le fichier.
4. Pousser sur `main`.
5. Creer puis pousser le tag `vX.Y.Z`.

Si le fichier est absent, le workflow de release echoue volontairement pour eviter une release sans notes propres.
