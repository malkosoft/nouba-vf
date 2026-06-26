# Nouba v2.7.17 — Header wrap + diagnostic son TV visible

## Bug 1 : « Agence Air Algérie » coupé

**Cause** : le `.header-center` avait `white-space:nowrap;text-overflow:ellipsis`
→ tout texte plus long que la largeur disponible était tronqué avec « ... ».

**Correctif** : remplacement par un mode multiligne propre :
- `white-space:normal` + line-clamp à 2 lignes maximum.
- `word-break:break-word` pour les noms longs.
- `line-height:1.18` pour un rendu compact.

Les noms type « Agence Air Algérie Tizi-Ouzou » s'affichent maintenant en
entier sur 2 lignes au lieu d'être tronqués.

## Bug 2 : Titre Nouba ne change pas

**Cause** : le fallback côté JS était incomplet. Si l'admin **vidait** le
champ HeaderTextFr en pensant remettre la valeur par défaut, le titre côté
TV passait à `''` (chaîne vide) au lieu de retomber sur le `SiteName`.

**Correctif** : ajout du fallback final sur `data.settings.siteName` dans
`updateStaticLabels()`. Ordre de priorité maintenant :
HeaderText (admin) → DisplayTitle (borne) → SiteName → vide.

## Bug 3 : Son TV (panneau diagnostic visible)

Sans accès F12 sur Smart TV, je peux ajouter un panneau de diagnostic
**directement à l'écran TV** qui affiche en temps réel ce qui bloque le son.

**Implémentation** :
- Petit panneau orange en bas-gauche de l'écran TV.
- Apparaît automatiquement à la première erreur TTS.
- Affiche les 3 dernières erreurs avec horodatage.
- Disparaît dès qu'une lecture audio réussit.
- N'apparaît jamais si le son fonctionne du premier coup.

**À l'usage** : prends une photo de ce panneau quand tu testes la TV et
envoie-la-moi. Tu sauras si c'est :
- `Piper KO (fr) : 204` → modèle Piper absent côté serveur
- `Piper KO (fr) : 500` → erreur Piper côté serveur
- `Exception playWithPiper` → bug JS
- `Web Speech API indisponible` → navigateur TV ne supporte ni Piper ni Web Speech
- (pas d'erreur affichée mais pas de son) → autoplay bloqué silencieusement

## Bug 4 : Mise en page

Tu n'as pas précisé **laquelle** des 6 mises en page pose problème. Si tu peux
me dire « Standard / Compact / Plein écran / TV Wall / Vidéo dominante / Minimal »
+ une photo, je corrige précisément. Sans cette info je ne peux que tâtonner.

## Vérifications passées
- 60 fichiers, 0 déséquilibre syntaxique.
- Aucun champ admin déplacé ou supprimé (compatibilité POST préservée).
