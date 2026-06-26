# Nouba Pro — v2.7.55

Date : 2026-06-24
Type : séparation des rôles administrateur (client / fournisseur) — RBAC

## Objectif

Séparer l'administration en deux rôles, sans créer deux portails distincts
(un seul login, deux niveaux d'accès) :

- **client** : gère le métier — services, guichets, agents, textes & langues,
  affichage, marque, tickets, résumés IA, et son propre mot de passe.
- **fournisseur** : accès total, y compris les réglages techniques que le
  client ne doit pas toucher — imprimante, réseau, licence, diagnostics, et
  la gestion des comptes administrateurs.

## Comportement

- **Premier lancement (Setup)** : le tout premier compte créé est un compte
  **fournisseur** (c'est l'intégrateur qui installe). Il pourra ensuite créer
  le(s) compte(s) **client** depuis l'onglet Système → « Comptes administrateurs ».
- **Installations existantes** : tous les administrateurs déjà présents sont
  automatiquement migrés en **fournisseur** au premier démarrage de cette
  version. Aucun accès n'est perdu (ex. site Air Algérie).

## Sécurité (côté serveur — la vraie protection)

Les endpoints techniques exigent désormais le rôle fournisseur, pas seulement
une session admin :

- `PrinterController` (config imprimante, test, ping, statut, liste) → fournisseur.
- `DiagnosticsController` (statut système) → fournisseur.
- `Admin/UpdateNetworkConfig` et `Admin/DetectIp` (réseau) → fournisseur.
- Nouvelles actions de gestion de comptes (`CreateAdminAccount`,
  `ResetAdminPassword`, `ToggleAdminAccount`, `DeleteAdminAccount`) → fournisseur,
  avec garde-fous : on ne peut pas supprimer/désactiver son propre compte ni le
  dernier compte fournisseur.
- Un client qui force `?tab=printer` ou `?tab=diagnostic` est ramené au tableau
  de bord ; le contenu de ces panneaux n'est pas rendu pour lui.

Les fonctions **métier** restent accessibles au client : services, guichets,
agents, textes, affichage, résumé/rapport IA, réinitialisation de la journée,
changement de son propre mot de passe.

## Interface

- Onglets « Imprimante » et « Diagnostic » masqués pour le client.
- Onglet « Système » : bloc « Identifiants » visible pour tous ; blocs Licence,
  Informations système, Sauvegardes et Réseau réservés au fournisseur.
- Nouveau bloc « Comptes administrateurs » (fournisseur) : créer / activer /
  désactiver / supprimer un compte et réinitialiser un mot de passe, avec
  sélection du rôle.
- Badge de rôle (Fournisseur / Client) dans la barre latérale.
- Le widget de statut imprimante de l'en-tête et le diagnostic automatique ne
  se lancent que pour le fournisseur (plus de requêtes 401 inutiles côté client).

## Base de données

- `AdminUser.Role` (TEXT, défaut « client »).
- `DbMigrator` : ajout de la colonne `Role` + promotion des comptes existants
  en « fournisseur » (exécuté une seule fois, à l'ajout de la colonne).

## Fichiers modifiés

- `Models/AdminUser.cs`
- `Data/DbMigrator.cs`
- `Controllers/AdminController.cs`
- `Controllers/PrinterController.cs`
- `Controllers/DiagnosticsController.cs`
- `Views/Admin/Index.cshtml`
- `Nouba.csproj` (2.7.54 → 2.7.55)

## Vérifications effectuées (analyse statique)

- Équilibrage des accolades C# (analyseur tenant compte des chaînes interpolées) : OK.
- Équilibrage `<div>` de la vue Admin (551/551) et des 6 blocs `@if (isProvider)` : OK.
- Syntaxe JavaScript de toutes les vues : OK.

## À valider sur site

- Installation neuve : 1er compte = fournisseur ; créer un compte client ; vérifier
  que le client ne voit ni Imprimante, ni Diagnostic, ni Réseau/Licence.
- Mise à jour d'un site existant : l'admin actuel reste fournisseur (accès complet).
- Tentative d'accès client à `?tab=printer` → redirigé vers le tableau de bord.
- Le client peut toujours changer son mot de passe et gérer le métier.
