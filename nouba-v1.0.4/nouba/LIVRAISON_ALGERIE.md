# Nouba Pro - Pack livraison Algerie

Cette archive contient une version corrigee et renforcee pour une installation client.

## Demarrage rapide

1. Extraire le dossier sur le PC serveur, par exemple `C:\Nouba`.
2. Double-cliquer sur `Lancer_Nouba.cmd`.
3. Ouvrir l'administration : `http://127.0.0.1:5000/Admin/Login`.
4. Pour la borne tactile : double-cliquer sur `Borne_Kiosque.cmd`.
5. Pour la TV : double-cliquer sur `TV_Kiosque.cmd`.

Le serveur ecoute aussi le reseau local via `http://0.0.0.0:5000`.
Depuis les autres postes, utiliser l'adresse IP du serveur :

- Borne : `http://IP_DU_SERVEUR:5000/Borne`
- TV : `http://IP_DU_SERVEUR:5000/Display`
- Agent : `http://IP_DU_SERVEUR:5000/Agent/Login`
- Admin : `http://IP_DU_SERVEUR:5000/Admin/Login`

## Reglages recommandes pour le marche algerien

- Langue par defaut : francais, arabe, tamazight ou anglais selon le client.
- Suivi mobile : activer le QR code dans Admin > Suivi mobile.
- TV : utiliser le layout `Standard` ou `TV Wall` pour les salles d'attente.
- Son TV : lancer l'affichage avec `TV_Kiosque.cmd`. Le script force Chrome/Edge a autoriser l'audio; si une Smart TV bloque encore le son, appuyer une fois sur OK / Enter sur le bouton `Son TV`.
- Imprimante : mode `all` pour ESC/POS + fallback navigateur.
- Reseau : autoriser le port 5000 dans le pare-feu Windows.

## Nouveautes de cette version corrigee

- Affichage TV stabilise sur 1280x720, 1366x768 et 1920x1080.
- Nom du site a cote du logo ajuste automatiquement pour ne plus etre coupe.
- Bouton `Son TV` visible tant que le navigateur n'a pas vraiment autorise l'audio.
- Garde-fou automatique contre les chevauchements en cas de zoom TV, sans casser les layouts speciaux.
- QR code general optionnel sur l'ecran TV, place dans un panneau reserve sous le media.
- Presets Algerie ajoutes : APC et Telecom.
- Presets existants renforces : poste, aeroport, tribunal.
- Scripts de lancement rapide pour admin, borne et TV.
- Script QA Display : `node tools\qa-display-layout.mjs`.

## Test logiciel affichage TV

1. Lancer Nouba sur le port voulu, par exemple :
   `dotnet run --no-build --urls http://0.0.0.0:5000`
2. Dans une autre console :
   `node tools\qa-display-layout.mjs`
3. Pour tester un autre port :
   `set NOUBA_QA_BASE_URL=http://127.0.0.1:5055`
   puis relancer le script.

Le script verifie automatiquement les layouts `standard`, `compact`, `large`, `tv-wall`, `video-hero`, `minimal` sur plusieurs tailles : TV HD, TV Full HD, tablette portrait et mobile.

## Avant livraison client

- Tester l'imprimante avec un vrai ticket.
- Tester l'affichage TV en plein ecran.
- Tester le son TV avec un vrai appel ticket; si le navigateur demande une interaction, appuyer sur `Son TV` une seule fois.
- Mettre le logo du client et le texte d'accueil.
- Verifier la sauvegarde automatique dans `C:\ProgramData\Nouba\backups`.
- Noter l'IP du serveur sur une fiche remise au client.
