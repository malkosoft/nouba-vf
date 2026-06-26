# Nouba Pro — v2.7.53

Date : 2026-06-23
Type : revue d'ingénierie senior + durcissement + préparation livraison vendable

## Contexte

Cette version fait suite à la campagne QA automatisée v2.7.52 (corrections de
sécurité, concurrence CallNext, refonte DOM de l'écran Display). Elle ajoute une
**revue d'ingénierie indépendante** de l'ensemble du code, un dernier durcissement
de sécurité, et la mise en forme d'un livrable propre, prêt à la vente.

## Revue indépendante — résultats

Vérification statique complète (le runtime .NET n'étant pas requis pour l'audit) :

- **Équilibrage structurel** de tous les fichiers C# : OK.
- **Syntaxe JavaScript** de toutes les vues `.cshtml` : OK (contrôle Node après
  neutralisation Razor).
- **Offline 100 %** confirmé : aucune dépendance CDN, Google Fonts, jQuery ou
  Bootstrap distant. Les icônes sont des SVG inline ; SignalR est servi en local.
- **Concurrence** : le verrou `SemaphoreSlim` de `CallNext` est acquis hors du
  bloc `try` et libéré dans `finally` (motif correct, pas de fuite de jeton). La
  création de ticket gère la course d'allocation par index UNIQUE + ré-essai
  automatique (jusqu'à 20 tentatives avec back-off).
- **Sécurité** : endpoints `Printer/*`, `Ai/Summary`, `Ai/Report`, `Diagnostics`
  protégés par session admin ; `TestPrintJson` protégé par jeton anti-CSRF ;
  en-têtes `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`
  présents ; uploads validés par extension + type MIME + signature binaire
  (magic bytes) + nom de fichier généré côté serveur (pas de traversée de chemin).
- **Double impression** : la garde `escposHandled` + le drapeau idempotent
  `printed` empêchent toute double boîte d'impression navigateur.
- **Affichage Display** : aucun `innerHTML` (rendu DOM sûr via
  `createElement` / `textContent` / `replaceChildren`).

## Correction appliquée

1. **`Ai/Translate` verrouillé sur session admin.**
   C'était le dernier endpoint IA resté public. Il n'est appelé par aucun
   client (borne, TV, agent, suivi) ; le laisser ouvert exposait inutilement la
   logique de traduction. Ajout de `if (!IsAdmin()) return Unauthorized();`,
   cohérent avec `Ai/Summary` et `Ai/Report`. Aucun impact fonctionnel.

## Préparation du livrable vendable

Retrait des artefacts de développement/QA qui n'ont pas leur place dans un
produit livré au client final :

- `tools/` (scripts QA `.mjs` + projet `CallNextConcurrencyQa`)
- `Tester_Nouba_QA.cmd`
- `DIFF_Nouba_v2_7_52.patch`
- `qa-display-browser.png`
- `QA_REPORT_Nouba_v2_7_52.md` / `.pdf`
- `apercu-effet-wow.html`
- `RAPPORT_CORRECTIONS_TV_ADMIN_QA.md`

Les journaux de version historiques ont été regroupés dans `docs/changelogs/`
pour alléger la racine du projet. Le code source, les launchers
(`Lancer_Nouba.cmd`, `Borne_Kiosque.cmd`, `TV_Kiosque.cmd`), le guide
(`wwwroot/guide.html`) et la documentation Piper sont conservés.

## Fichiers modifiés

- `Controllers/AiController.cs` (durcissement `Translate`)
- `Nouba.csproj` (version 2.7.52 → 2.7.53)

## Tests restant à faire sur site (inchangés)

- Impression ESC/POS sur imprimante physique (papier, coupe, bip, manque papier).
- Écoute humaine des voix Piper réelles (FR/AR/EN/TZ).
- Test multi-postes LAN réel (borne, TV, poste agent, smartphone QR).
- Mode licence production (sans `NOUBA_DEV_BYPASS_LICENSE`).
