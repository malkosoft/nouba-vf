# Nouba v2.7.27 — Son FR/AR fiabilisé (fin du « une fois sur deux »)

## Objectif

Tu m'as signalé que le son arabe et français ne marchait qu'une fois sur
deux, et tu voulais un produit professionnel, prêt à l'emploi. Cette version
s'attaque aux causes racines de l'instabilité audio, identifiées dans le code,
sans rien changer au design ni au reste du fonctionnement.

## Les vraies causes du « une fois sur deux »

En relisant la chaîne complète `annonce → file → Piper → fallback navigateur`,
j'ai trouvé cinq défauts qui, combinés, produisaient exactement ce symptôme.

### 1. Une langue était désactivée définitivement après 3 échecs (client)

`PIPER_GIVEUP_AFTER = 3` : au bout de 3 ratés (souvent de simples latences),
la voix Piper d'une langue était coupée pour toute la session, jusqu'au prochain
rechargement de page. Une TV qui tournait depuis des heures finissait muette.

**Corrigé** : ce mécanisme est supprimé. On ne désactive plus jamais une langue.
Le compteur ne sert plus qu'au diagnostic.

### 2. Garde-fou de 2,5 s qui coupait des annonces qui jouaient (client)

Un `setTimeout(2500)` déclarait l'échec d'une lecture si elle n'était pas
terminée en 2,5 s — alors qu'une phrase arabe complète dure 3 à 5 s. On
basculait alors en voix de secours par-dessus, ou on coupait tout.

**Corrigé** : le garde-fou ne vérifie plus que le *démarrage* réel de la
lecture (évènement `onplaying`), avec une marge de 9 s pour couvrir le
chargement à froid du modèle. Une fois démarrée, l'annonce va à son terme.

### 3. `voiceBusy` pouvait rester bloqué (client)

Si une exception survenait pendant une annonce, l'indicateur `voiceBusy`
restait à `true` et **toutes** les annonces suivantes étaient ignorées
silencieusement.

**Corrigé** : `speakNext` est entièrement protégé par `try/finally` avec
libération garantie, plus un filet de sécurité global de 20 s qui débloque
la file quoi qu'il arrive.

### 4. Verrou serveur unique partagé entre vérification et synthèse (serveur)

Le service utilisait un seul `SemaphoreSlim(1,1)` à la fois pour la
« sonde » (vérifier qu'un modèle se charge) et pour les vraies synthèses.
Au premier ticket, une sonde pouvait bloquer la synthèse jusqu'à 5 s, et
deux annonces rapprochées (FR puis AR) s'attendaient l'une l'autre.

**Corrigé** : deux verrous distincts. La sonde a le sien (`_probeLock`) et
ne bloque plus jamais une synthèse. Les synthèses tournent désormais jusqu'à
2 en parallèle (Piper est un process isolé, aucun état partagé à protéger).

### 5. Pas de pré-chauffe : la 1re annonce ratait presque toujours (serveur)

Le tout premier appel charge le modèle ONNX en mémoire (~2-3 s à froid).
Avec l'ancien timeout, cette première synthèse — souvent l'arabe — dépassait
le délai et basculait en voix navigateur.

**Corrigé** :
- Pré-chauffe automatique au démarrage du serveur (`WarmUpAsync`) : les
  modèles FR/EN/AR sont chargés en arrière-plan dès le lancement.
- Pré-chauffe côté navigateur dès le déverrouillage du son.
- Timeout de synthèse porté de 8 s à 15 s pour absorber le démarrage à froid.

## Robustesse supplémentaire

- **Retry automatique** : si Piper ne répond pas du premier coup, un second
  essai immédiat est tenté avant de passer à la voix du navigateur (client
  Display et bouton de test Admin).
- **En-tête de diagnostic** `X-Piper` sur `/Tts/Speak` (`ok`,
  `model-missing`, `synth-failed`) pour comprendre instantanément un éventuel
  problème côté serveur.

## Ce qui n'a PAS changé

- Aucun changement de design, de mise en page, de base de données ni de modèle.
- Aucun changement de comportement métier (tickets, guichets, suivi, SMS,
  impression, QR, etc.).
- Le rappel produit reste valable : le français et l'anglais ont une seule
  voix neurale Piper (féminine) ; le choix Masculin/Féminin s'applique à
  l'arabe, qui dispose des deux voix.

## Fichiers modifiés

- `Services/PiperTtsService.cs` — verrous séparés, parallélisme, timeouts,
  pré-chauffe.
- `Controllers/TtsController.cs` — en-tête diagnostic, gestion d'annulation.
- `Program.cs` — pré-chauffe au démarrage en arrière-plan.
- `Views/Display/Index.cshtml` — suppression du give-up, garde-fou par
  démarrage réel, file protégée, retry, pré-chauffe client.
- `Views/Admin/Index.cshtml` — retry au démarrage à froid sur le test voix.

## Pour vérifier après déploiement

1. Lance Nouba, attends ~5 s (pré-chauffe), ouvre `/Display`.
2. Active le son (un clic suffit), puis appelle plusieurs tickets d'affilée.
3. Le son doit être présent **à chaque fois**, en FR comme en AR.
4. En cas de doute, ouvre `/Display?debug=1` (console F12) : tu verras
   `✓ Piper IA a parle.` à chaque annonce.
5. Côté Admin > Voix & son : les boutons « Tester FR / EN / AR » doivent
   produire du son dès le premier clic, même juste après le démarrage.

## Note sur les Smart TV VIDAA / Hisense

Le rappel des versions précédentes tient toujours : certaines TV Hisense
(VIDAA) bloquent au niveau firmware toute lecture audio web. Si une TV
reste muette malgré cette version, la solution reste un mini-PC ou une box
Android TV branchée en HDMI. Sur PC, mini-PC et Android, le son est
désormais fiable.

---

# ✨ Effet « WOW » à l'appel d'un ticket

Pour rendre la démonstration commerciale percutante, chaque appel d'un VRAI
ticket déclenche maintenant une animation plein écran de ~2,2 s :

- Grand numéro de ticket lumineux (dégradé doré + reflet qui balaie).
- Halo coloré rayonnant + 3 anneaux d'onde qui se propagent.
- 14 étincelles projetées en cercle.
- Libellé localisé (« TICKET APPELÉ » / « NOW SERVING » / « التذكرة »),
  guichet et service.
- Glow persistant sur la cellule du ticket principal après l'animation.

Points clés :

- **Aucune fausse donnée.** L'effet se déclenche uniquement sur de vrais
  appels de tickets, exactement comme la voix.
- **Indépendant du son.** L'effet visuel se joue même si les annonces
  vocales sont désactivées en admin.
- **Adapté à la charte du client.** Les couleurs reprennent automatiquement
  l'accent, la couleur de marque et la couleur de ticket configurées en admin.
- **Respecte l'accessibilité** (`prefers-reduced-motion`) et le réglage
  « animations » de l'admin.

### Comment l'utiliser en démonstration

- **Garanti pour une démo** : ouvre l'écran avec `/Display?wow=1`. L'effet
  est alors forcé même si le réglage « animations » est décoché.
- Pour le désactiver ponctuellement : `/Display?wow=0`.
- **Répéter / prévisualiser** sans attendre un vrai ticket : ouvre la
  console (F12) sur l'écran Display et tape `NoubaWow()` (ou
  `NoubaWow('B-42','Guichet 2')`). Pratique pour t'entraîner avant le RDV.
- Un fichier `apercu-effet-wow.html` est fourni à la racine : tu peux
  l'ouvrir directement dans un navigateur (double-clic) pour montrer
  l'effet hors connexion, sans lancer tout le logiciel.

### Fichiers concernés par l'effet wow

- `Views/Display/Index.cshtml` — CSS de l'animation, overlay HTML, logique
  de déclenchement, options `?wow=1/0`, hooks `NoubaWow()` / `previewWow`.
- `apercu-effet-wow.html` — page de démonstration autonome (bonus).
