# Nouba Pro v1.0.4

**Solution professionnelle de gestion de file d'attente — 100 % offline.**

Borne tactile · Affichage TV · Interface agent · Console d'administration.
Multilingue : Français · العربية · Tamazight · English.

---

## Architecture

- **ASP.NET Core 8 / MVC** — application web auto-hébergée.
- **SQLite + WAL** — base locale unique, sauvegardes automatiques toutes les 6h.
- **SignalR** — temps réel sur l'écran d'affichage et l'interface agent.
- **Licence RSA-2048** — clés signées, liées à l'adresse MAC physique.

L'application écoute par défaut sur `http://127.0.0.1:5000`. Pour accès LAN,
modifier la clé `Nouba:Urls` dans `appsettings.json` ou via l'interface admin.

---

## Premier lancement

1. Démarrer l'application — un navigateur s'ouvre sur la page d'activation.
2. Communiquer l'**identifiant machine** affiché à votre revendeur.
3. Saisir la clé de licence reçue → l'application redirige automatiquement vers la borne.
4. Aller sur `/Admin/Login` pour créer le premier compte administrateur.

Aucun compte administrateur par défaut. La création se fait au premier accès.

---

## Parcours utilisateurs

| Interface       | URL              | Description                              |
|-----------------|------------------|------------------------------------------|
| Borne client    | `/Borne`         | Sélection service + impression ticket    |
| Affichage TV    | `/Display`       | Numéro appelé + guichet, temps réel      |
| Espace agent    | `/Agent/Login`   | Appel ticket suivant, gestion file       |
| Administration  | `/Admin/Login`   | Configuration, statistiques, rapports    |

---

## Sécurité

- Mots de passe admin et agents hashés (PBKDF2 + sel par utilisateur).
- Limitation des tentatives de connexion (rate limiter en mémoire).
- Protection anti-CSRF sur toutes les actions sensibles.
- Uploads médias filtrés (JPG, PNG, WEBP, MP4, WEBM uniquement).
- Validation RSA-2048 SHA-256 sur la signature de licence.
- Limite stricte du nombre d'agents actifs selon la licence.
- Contrainte UNIQUE SQLite contre les doublons de tickets.

---

## Sauvegardes & maintenance

- Sauvegarde automatique de la base toutes les **6 heures** (20 dernières conservées).
- Dossier visible dans **Admin → Système → Données & Sauvegardes**.
- Recommandé : copier régulièrement ce dossier sur un support externe.

---

## Déploiement

Voir `DEPLOIEMENT.md` pour la procédure complète de mise en production
(publication, hébergement Windows, démarrage automatique).

---

© 2026 Nouba Software — Tous droits réservés.
