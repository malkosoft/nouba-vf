# Nouba v2.7.5 — Sélecteur de voix restauré + 3 nouvelles mises en page

## Bug 1 : choix masculin/féminin disparu

Dans la version PREMIUM, le sélecteur Féminine/Masculine et tous les sliders
(répétitions, vitesse, pitch, volume) avaient été remplacés par des
`<input type="hidden">` figés. Résultat : impossible de modifier le genre,
toujours figé sur féminin.

**Correctif** — restauration des contrôles visibles dans le panneau Voix
(onglet Affichage → Voix IA) :
- Cartes radio Féminine ♀ / Masculine ♂ (le clic met à jour la bordure et la couleur).
- Slider Répétitions (1× à 5×).
- Slider Vitesse de parole (0.5× à 2.0×).
- Slider Hauteur / pitch (0.5 à 2.0).
- Slider Volume (0.1 à 1.0).

Tous ces inputs ont leurs `name="..."` exacts d'origine, donc le POST
`UpdateSettings` côté serveur n'a pas besoin d'être touché.

## Bug 2 : seulement 3 mises en page

Tu n'avais que Standard / Compact / Plein écran. La page admin propose
maintenant **6 mises en page**, et chacune a un vrai rendu CSS sur
`/Display` (pas une option morte) :

| Clé | Visuel | Cas d'usage |
|---|---|---|
| **standard** | Ticket à gauche, vidéo + historique à droite | Cas général |
| **compact** | Tickets plus larges, panneau droit réduit | Beaucoup de guichets |
| **large** | Ticket plein écran, pas de panneau droit | Petite borne sans vidéo |
| **tv-wall** | Ticket géant, historique en bandeau bas horizontal (3 derniers) | Hall d'aéroport, gare |
| **video-hero** | Vidéo dominante en haut, ticket en grand en bas | Communication d'agence |
| **minimal** | Un seul ticket énorme, pas d'historique | Façade simple |

### Implémentation
- HTML inchangé (`.left-col` + `.right-col`) : aucun risque de casser
  l'existant.
- CSS pilote tout via une classe `.page--standard`, `.page--compact`,
  `.page--large`, `.page--tv-wall`, `.page--video-hero`, `.page--minimal`
  appliquée à l'élément `<main class="page">`.
- Validation côté serveur étendue : `AdminController.UpdateSettings` accepte
  désormais les 6 valeurs (avant, n'accepter que `standard|compact|large` les
  ramenait à `standard` si on choisissait un nouveau layout).

## Comment tester
1. Admin → Affichage → Apparence → menu déroulant **Mise en page** → choisir
   « TV Wall ».
2. Cliquer Enregistrer en haut.
3. Ouvrir `/Display` dans un autre onglet : le ticket doit prendre presque
   tout l'écran et l'historique passe en bandeau bas avec 3 cellules en ligne.
4. Refaire l'expérience avec « Vidéo dominante » et « Minimal ».

## Conservé tel quel
Tickets, impression, CSV, langues, SMS, IA admin, agents, guichets, services,
monitoring imprimante, licence, base de données, backend Piper, fallback
navigateur, presets thème (8 secteurs depuis v2.7.4).
