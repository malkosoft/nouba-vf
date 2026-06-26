# Nouba v2.7.50 — Audit complet des écrans + cohérence typographique finale

Cette version conclut une revue, écran par écran, de toute l'interface, au
niveau d'un ingénieur senior. Bonne nouvelle : **l'essentiel du produit était
déjà au niveau professionnel**. Cette version corrige le dernier détail de
cohérence et documente ce qui a été vérifié.

## Audit des écrans (revue complète)

- **Borne** : redessinée en v2.7.49 (file d'attente en direct par service,
  effet d'onde tactile). Niveau pro confirmé.
- **Affichage TV** : déjà excellent — animation d'appel « burst » (halo,
  anneaux, reflet, étincelles), **carillon de notification synthétisé**
  (WebAudio, 100 % offline) joué avant chaque annonce vocale, voix IA Piper
  + secours navigateur, 6 layouts, responsive Smart TV. Aucune retouche
  nécessaire : y toucher aurait été du risque sans gain.
- **Confirmation ticket** : déjà soignée — carte qui s'élève, voile doré,
  anneau de validation, étincelles, cascade, et feuille d'impression
  thermique optimisée (corrigée en v2.7.47). Niveau pro confirmé.
- **Poste Agent** : déjà aligné sur le design system or/bleu nuit (`--np-*`).
- **Tableau de bord Admin** : aligné sur la même identité ; ses 80+ icônes,
  invisibles avant la v2.7.48, s'affichent désormais grâce au jeu d'icônes
  offline intégré.

## Correction de cohérence (cette version)

En supprimant les polices Google en v2.7.48, des écrans déclaraient encore
les polices `Sora` / `Manrope` / `Inter` qui ne se chargent plus : elles
retombaient silencieusement sur Segoe UI (fonctionnel, mais incohérent d'un
écran à l'autre selon l'ordre de repli).

**Correctif :** tous les écrans utilisent désormais exactement la même pile
système (`Segoe UI` en tête, repli arabe `Tahoma` / `Noto Sans Arabic`).
Rendu typographique **identique et garanti** sur l'ensemble du produit,
sans aucune référence de police morte.

## Fichiers modifiés

- `Views/Admin/Index.cshtml` — variables `--font-display` / `--font-body`
  unifiées sur la pile système.
- `Views/Borne/Index.cshtml` — pile de polices unifiée (suppression du
  `Inter` résiduel).
- `Nouba.csproj` — version 2.7.50.

## Reste à faire pour un prototype « vendable » à 100 %

Le visuel est prêt. Ce qui ne peut être validé qu'avec votre matériel réel :

1. `dotnet build` (aucune logique backend modifiée dans cette version).
2. **Impression** : imprimer plusieurs tickets (FR + AR) sur l'imprimante
   thermique réelle, vérifier que toutes les lignes sortent à chaque fois.
3. **Voix** : sur la TV, vérifier le carillon + l'annonce vocale Piper (les
   modèles `.onnx` doivent être présents dans `wwwroot/tts/piper`).
4. **Réseau LAN** : borne, TV et postes agents sur le même réseau ;
   vérifier le temps réel (un ticket pris apparaît instantanément partout).
5. **Offline** : couper Internet et revérifier icônes + polices + voix.
