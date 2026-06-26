# Nouba v2.7.3 — Admin premium, presets cartes, diagnostic câblé

## Phase 1 — Admin propre & barre Enregistrer toujours visible

Le bouton « Enregistrer » devait être accessible où qu'on soit dans la page.
La barre sticky existait déjà sur l'écran d'affichage ; je l'ai ajoutée aussi sur :

- **Borne kiosque** (`tab-borne`) — barre sticky en haut, ancien bouton bas retiré.
- **Imprimante** (`tab-printer`) — barre sticky en haut. Le bouton délègue le submit au formulaire `printerForm` via JS.
- **SMS notifications** (`tab-sms`) — barre sticky en haut. Le bouton délègue à `smsForm`.

Avantage : aucun input serveur n'est cassé. Les formulaires originaux restent intacts,
on les soumet juste depuis un bouton fixe en haut au lieu d'un bouton enterré en bas.

## Phase 1 (bis) — Nettoyage JS dupliqué

Le fichier `Views/Admin/Index.cshtml` contenait des **doublons exacts** :
`runDiagnostics`, `testVoice`, `testBrowserAudio`, `setMiniCard`, `refreshTtsStatus`
étaient définies 2 fois ; `DOMContentLoaded` était présent 3 fois. Tout est nettoyé,
chaque fonction n'a plus qu'une définition. Cela retire ~50 lignes mortes.

## Phase 3 — Galerie de cartes preset premium

Le `<select>` simple est remplacé par une vraie **galerie de 5 cartes premium** :
🏥 Clinique · 🏛️ Administration · 🏦 Banque · 🏛 Mairie · 🏢 Entreprise.

Chaque carte affiche :
- l'icône du secteur,
- son nom,
- une description courte (« Clair, rassurant, médical »…),
- 4 swatches de couleurs (accent / carte / historique / footer),
- un badge « Actif » sur la carte sélectionnée.

Au clic, la fonction `applyTheme(key)` :
1. met à jour le champ caché `ThemePreset` (compatibilité serveur),
2. applique les couleurs adaptées,
3. ajuste vitesse et pitch de la voix (ambiance audio),
4. surbrille la carte choisie.

Bouton **« Restaurer le thème par défaut »** : revient au preset entreprise après confirmation.

Toutes les valeurs sont prêtes à enregistrer ; cliquer sur Enregistrer en haut applique tout.

## Phase 5 — Diagnostic câblé

Le `DiagnosticsController` était déjà présent dans le zip d'entrée mais le panneau UI
était partiellement câblé. Avec le nettoyage des doublons JS :

- L'onglet **F · Sécurité / maintenance → Diagnostic** appelle `/Diagnostics/Status` au chargement.
- Cartes affichées : Système / Voix IA / Imprimante / Borne, vert si OK, orange sinon.
- Boutons de tests rapides : tester voix FR / audio navigateur / imprimante / ouvrir affichage / ouvrir borne.
- Tout est conditionnel : si le client n'est pas authentifié admin, l'endpoint renvoie 401.

## Conservé tel quel

Tickets, impression, CSV, langues, SMS, IA admin (résumé/rapport), agents, guichets,
services, monitoring imprimante, licence, base de données, audio Piper, fallback navigateur.

## Limite honnête

Pas de SDK .NET dans mon environnement : je n'ai pas pu lancer `dotnet build`. Les
vérifications syntaxiques (54 fichiers, 0 déséquilibre) sont passées, mais si une
erreur de compile sort chez vous, envoyez-la et je la corrige.
