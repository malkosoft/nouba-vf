# Nouba v2.7.26 — Admin premium : voix + langue clarifiées

## Objectif

Tu m'as demandé une page admin premium pour un produit vendable, et que
le choix Féminin/Masculin + choix de langue fonctionnent correctement.

## Ce qui change

### 1. Bloc Genre de voix premium

**Avant** : 2 petites cartes côte à côte, sans explication, qui pouvaient
se griser sans qu'on comprenne pourquoi. L'utilisateur pensait que la
fonctionnalité ne marchait pas.

**Maintenant** :
- Cartes plus grandes en grille 2 colonnes avec icône, label, description.
- Effet hover et état sélectionné en dégradé indigo.
- **Note explicite** sous les cartes : *« Le français et l'anglais ont une
  seule voix neurale Piper de haute qualité (féminine). Le choix Masculin /
  Féminin est appliqué à l'arabe uniquement (les deux voix sont disponibles). »*
- Les 2 cartes restent **TOUJOURS sélectionnables** (avant, la carte
  Masculine se grisait si le modèle ONNX arabe masculin était absent —
  l'utilisateur pensait que tout son système était cassé).

### 2. Bloc Langue par défaut premium

**Avant** : un simple `<select>` avec une étiquette « Langue par défaut » peu
parlante, planqué tout en bas d'un sous-onglet.

**Maintenant** :
- 4 cartes en grille (FR, AR, TZ, EN) avec drapeau, nom en français,
  nom natif (français / العربية / Tamaziɣt / English).
- Card sélectionnée avec dégradé indigo accent.
- Note explicative : *« C'est la langue affichée par défaut sur l'écran TV
  et la borne. Les utilisateurs peuvent changer la langue à la prise de ticket. »*
- L'arabe utilise `dir="rtl"` correctement.

### 3. Logs DIAG silencieux en prod

Les logs `[Nouba DIAG]` ajoutés en v2.7.24/25 pour debug du son ne polluent
plus la console des utilisateurs finaux. Ils ne s'affichent que si :
- L'URL contient `?debug=1` (ex: `http://localhost:5000/Display?debug=1`)
- OU `localStorage.setItem('NOUBA_DEBUG','1')` est défini

Tu peux toujours faire du diagnostic technique sans avoir une console
polluée pendant les démos commerciales.

## Vérification voix/langue côté serveur

Tu m'as signalé que le choix Masculin/Féminin ne marchait pas. **En réalité
le binding marche** (`AdminController.UpdateVoiceGender` est appelé,
`settings.VoiceGender` est bien sauvegardé). Le « bug » perçu vient du fait
que le français et l'anglais n'ont qu'un seul modèle ONNX Piper chacun
(`fr_FR-upmc-medium.onnx`, `en_US-lessac-medium.onnx`) — féminine par
construction des modèles. Donc même si tu coches « Masculin », FR et EN
restent féminins. Seul l'arabe a deux modèles.

La note ajoutée dans l'admin explique ça clairement au client.

## Bug son : où on en est

Tu m'as confirmé en v2.7.25 que le log montre :
> `[Nouba DIAG] tentative announceTicket pour signature : c002/guichet2/ déjà faite ? false`

Donc `announceTicket` est appelée. Mais aucun log `[Nouba TTS]` après. Cela
veut dire que le bug est dans la chaîne `announceTicket → speakNext →
playWithPiper`. La v2.7.25 contient déjà les logs pour identifier où exactement.

**Pour finir le diagnostic son**, ajoute `?debug=1` à l'URL `/Display`,
appelle un ticket, copie-colle dans la console **tous** les logs `[Nouba DIAG]`.
Avec ça je trouve la ligne précise qui bloque.

## Vérifications passées
- 578/578 div Admin, 46/46 div Display, scripts équilibrés
- Aucun changement serveur, modèles, DB
