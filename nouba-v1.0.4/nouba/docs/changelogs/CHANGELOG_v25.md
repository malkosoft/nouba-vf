# Nouba Pro — Changelog v2.5.0

Date : 6 mai 2026

## 🖨️ Imprimante — 3 modes universels

L'imprimante de la borne supporte désormais **trois types de connexion**
configurables depuis l'admin (onglet « Imprimante » → « Type de connexion ») :

- **🌐 Réseau (TCP/IP)** — port 9100 — universel, recommandé
- **🔌 USB / Windows** — pilote Windows installé (winspool RAW)
- **📡 Série COM** — pour vieilles bornes série / dongles USB-Série

### Détection automatique
- Bouton « Détecter » dans l'admin → liste les imprimantes Windows installées
- Liste des ports COM disponibles auto-remplie
- Plus besoin de saisir manuellement le nom — sélection dans une liste déroulante

### Modes d'impression (déjà existants, conservés)
- `escpos` — impression directe ESC/POS (silencieux, instantané)
- `chrome-kiosk` — Chrome --kiosk-printing (silencieux navigateur)
- `browser` — Boîte de dialogue Ctrl+P (debug)
- `all` — Tout (recommandé) : ESC/POS prioritaire + repli navigateur

## 🌐 Configuration réseau — Refonte premium

L'onglet Système → « Configuration réseau » a été entièrement repensé :

- **Bandeau d'état actuel** avec couleur dynamique selon le mode (Local / LAN / IP fixe)
- **Trois cartes de mode visuelles** au lieu de boutons
- **IPs locales auto-détectées** affichées en chips cliquables
- **Preview live des URLs LAN** par interface (Borne / Display / Agent / Admin)
- **Bouton « Tester »** qui valide le format de l'URL saisie
- Indicateur « Serveur actif » avec animation pulse

## 🤖 Assistant IA Nouba — 100 % offline

Quatre fonctions IA embarquées, sans aucune dépendance externe :

### 1. Résumé quotidien intelligent
Endpoint : `GET /Ai/Summary`
- Total tickets, traités, absents, prioritaires
- Pic d'activité (heure + nombre)
- Service le plus demandé
- Agent le plus rapide (calcul à partir des écarts entre appels)
- Taux d'absence + recommandations actionnables

### 2. Rapport textuel professionnel FR
Endpoint : `GET /Ai/Report`
- Synthèse rédigée en français professionnel
- Sections : Activité, Performance agents, Recommandations
- Prêt à coller dans un email à la direction

### 3. Chatbot d'orientation borne
Endpoint : `POST /Ai/Chat`
- Analyse en langage naturel : « Je veux renouveler ma carte »
- Matching pondéré sur nom, code, mots-clés et synonymes
- Score de confiance retourné (0..1)

### 4. Traduction de services
Endpoint : `GET /Ai/Translate?nameFr=...`
- Dictionnaire embarqué de 22+ termes administratifs algériens
- Langues : FR / AR / TZ / EN
- Fallback : si pas de match, renvoie le terme d'origine (l'admin édite à la main)

### TTS naturel (Piper) — extension future
Le service `NoubaAiService.GenerateNaturalSpeechAsync()` est un point
d'extension prêt à recevoir Piper. Documentation : `docs/ia/PIPER_INSTALL.md`.

Pour le moment, les annonces vocales continuent d'utiliser l'API Web Speech
du navigateur (qui fonctionne déjà très bien hors ligne).

## 📊 Dashboard — Widget IA intégré

Un nouveau panneau « Assistant IA Nouba » apparaît dans le dashboard avec :
- Résumé du jour mis à jour automatiquement
- Recommandations actionnables (taux d'absence, pics horaires…)
- Bouton « Rapport pro complet » (ouvre le texte dans un nouvel onglet)
- **Chatbot intégré testable en direct** depuis l'admin

## 🔧 Détails techniques

### Nouveaux paquets NuGet
- `System.Drawing.Common` 8.0.7 (énumération imprimantes Windows)
- `System.IO.Ports` 8.0.0 (déjà présent — utilisé aussi par les imprimantes série)

### Migrations DB ajoutées (auto-appliquées)
- `PrinterConnection` (TEXT)
- `PrinterComPort` (TEXT)
- `PrinterBaudRate` (INTEGER)

### Nouveaux fichiers
- `Services/NoubaAiService.cs` — moteur IA offline
- `Controllers/AiController.cs` — endpoints /Ai/*
- `docs/ia/PIPER_INSTALL.md` — guide d'activation TTS naturel

### Fichiers modifiés
- `Services/EscPosPrinter.cs` — réécrit pour supporter USB/Série/Réseau
- `Models/UiSettings.cs` — 3 nouveaux champs imprimante
- `Data/DbMigrator.cs` — migrations auto
- `Controllers/PrinterController.cs` — endpoint /Printer/List enrichi
- `Controllers/AdminController.cs` — exposition IPs locales
- `Views/Admin/Index.cshtml` — UI imprimante + UI réseau + widget IA
- `Program.cs` — enregistrement NoubaAiService
- `Nouba.csproj` — version 2.5.0

## ⚠️ Non encore inclus

Ces points seront livrés dans des releases dédiées :
- Réorganisation complète sidebar admin (gros chantier UX)
- Détection USB SMS modem GSM en mode "auto-discovery" (plug-and-play)
- Heat-map d'activité 7 jours sur le dashboard
- Chatbot intégré dans la borne tactile (UI dédiée)
- Bouton « Auto-traduire » dans la création de service
