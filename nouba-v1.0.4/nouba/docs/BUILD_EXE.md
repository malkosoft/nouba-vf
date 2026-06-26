# Nouba Pro 1.0.0 — Fabriquer et livrer l'exécutable Windows

Objectif : produire un **`.exe` autonome** que le client lance sans rien installer
(pas de .NET, pas de dépendances). Tout tourne en local sur le PC de l'accueil.

## 1. Pré-requis (sur VOTRE machine de build, pas chez le client)

- Windows 64 bits
- **SDK .NET 8** installé (`dotnet --version` doit répondre `8.x`)
- Les 5 modèles Piper `.onnx` (+ `.onnx.json`) présents dans
  `wwwroot/tts/piper/` — ils sont déjà là dans ce paquet. Si vous repartez d'une
  source allégée, restaurez-les d'abord (voir `docs/PIPER_INSTALL.md`).

## 2. Construire

Depuis le dossier du projet (celui qui contient `Nouba.csproj`) :

```
publish-windows.bat
```

Le script produit `publish\NoubaPro-1.0.0\` contenant :

- `Nouba.exe` — l'application complète (runtime .NET embarqué)
- `wwwroot\` — dont `tts\piper\` avec `piper.exe`, les `.onnx` et les DLL

> **Important** : `Nouba.exe` seul ne suffit pas. On livre **tout le dossier**
> `NoubaPro-1.0.0`, car Piper (`piper.exe` + modèles) et les fichiers web vivent
> dans `wwwroot\` à côté de l'exe.

### Réglages techniques retenus (et pourquoi)
- **self-contained** : aucun .NET à installer chez le client.
- **single-file** + `IncludeNativeLibrariesForSelfExtract` : un seul exe, les
  DLL natives (SQLite, dessin) gérées proprement.
- **ReadyToRun** : démarrage plus rapide sur le PC modeste de l'accueil.
- **Pas de trimming** : l'app utilise la réflexion (version, EF, impression) ;
  le trimming la casserait. Ne pas l'activer.

## 3. Premier lancement chez le client

1. Copier le dossier `NoubaPro-1.0.0` sur le PC (idéalement sur le **disque
   interne**, pas une clé USB — voir note perfs).
2. Double-cliquer `Nouba.exe`. La base SQLite se crée automatiquement au 1er
   démarrage.
3. Ouvrir un navigateur sur `http://localhost:5000` (ou l'URL LAN configurée).
4. Vérifier la voix : page **`/tts-tuning.html`** → tester arabe homme/femme.
   La 1ʳᵉ synthèse de chaque voix prend 2-3 s (chargement du modèle), ensuite
   c'est instantané (pré-chauffage + cache).

## 4. Pour la vente — à savoir

- **SmartScreen / antivirus** : un `.exe` non signé peut déclencher un
  avertissement Windows au 1er lancement (« Éditeur inconnu »). Pour un produit
  commercial, envisagez une **signature de code** (certificat Authenticode).
  En attendant : « Informations complémentaires » → « Exécuter quand même ».
- **Démarrage automatique** : pour une borne, placez un raccourci de `Nouba.exe`
  dans le dossier Démarrage de Windows, ou créez une tâche planifiée « à
  l'ouverture de session ».
- **Performance clé USB** : exécuter depuis une clé USB ralentit le chargement
  des modèles (60-75 Mo chacun) et l'écriture de la base. Installez sur le
  disque interne pour un produit vendable.

## 5. Vérifier la version

Le badge en bas de l'admin doit afficher **`v1.0.0`**. Si ce n'est pas le cas,
le build n'a pas pris le nouveau `Nouba.csproj` (relancez après `dotnet clean`).
