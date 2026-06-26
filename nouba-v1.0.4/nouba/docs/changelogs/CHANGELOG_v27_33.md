# Nouba v2.7.33 — Prononciation arabe nettement améliorée

## Objectif

Rendre la voix arabe plus naturelle et plus claire, en restant 100% local et
gratuit (pas d'Internet, pas de service payant). On n'a pas changé le modèle
de voix lui-même (impossible de le rendre plus naturel) — on a corrigé la
façon dont le TEXTE arabe est préparé et lu, ce qui change beaucoup le rendu.

## Les 5 améliorations

### 1. Nombres au-dessus de 99 enfin prononcés en arabe
Avant, un ticket « 128 » ou un nombre > 99 retombait sur des chiffres latins
que la voix arabe lisait mal (ou sautait). Désormais ils sont convertis en
toutes lettres arabes, dans le bon ordre :
- 128 → « مئة وثمانية وعشرون »
- 28  → « ثمانية وعشرون » (unité puis dizaine, ordre arabe correct)
- 250 → « مئتان وخمسون »

### 2. Numéro de guichet en lettres arabes
Avant : « الشباك 2 » (chiffre latin mal lu). Maintenant : « الشُبّاك رقم اثنان »
(« guichet numéro deux » en arabe complet).

### 3. Lettre du ticket transcrite en arabe
La lettre latine du préfixe (ex. « A ») faisait buter la voix arabe. Elle est
maintenant transcrite phonétiquement en arabe (A → أ, B → بي, etc.).

### 4. Texte voyellé (tachkīl)
Les nombres et mots arabes portent désormais les signes de voyellisation, ce
qui guide la prononciation du modèle.

### 5. Débit ralenti pour l'arabe
Les modèles arabes libres parlent vite, ce qui nuit à la clarté. Le débit
arabe est ralenti à 1.15 (≈ 15% plus lent), uniquement pour l'arabe — le
français et l'anglais gardent leur débit naturel. Résultat : annonce arabe
plus posée et intelligible.

## Limite à garder en tête

Ces réglages améliorent sensiblement la clarté, mais la voix arabe reste un
modèle libre moins abouti que le français/anglais. Pour une qualité « studio »
en arabe, il faudrait une voix premium en ligne (payante, et qui casserait le
fonctionnement hors-ligne). Les améliorations de cette version sont le
meilleur compromis gratuit + local.

## Important : vider le cache audio

Le débit arabe ayant changé, les anciens fichiers audio arabes en cache ne
correspondent plus. La clé de cache a été mise à jour pour les régénérer
automatiquement, mais tu peux aussi vider manuellement le dossier
`wwwroot/tts/cache/` (sans risque, il se reremplit tout seul).

## Fichiers modifiés

- `Views/Display/Index.cshtml` — conversion arabe des centaines, guichet et
  lettre de ticket en arabe, voyellisation.
- `Services/PiperTtsService.cs` — débit arabe ralenti (--length-scale 1.15),
  débit intégré à la clé de cache.
- `Nouba.csproj` — version 2.7.33.

## Vérification

- Conversion arabe des nombres testée (1 à 999) : correcte.
- JavaScript de l'écran d'affichage revalidé.
- Intégrité du service Piper contrôlée.
- À tester de ton côté après `dotnet build` : appelle un ticket en arabe et
  écoute — la voix doit être plus posée et lire correctement les nombres.
