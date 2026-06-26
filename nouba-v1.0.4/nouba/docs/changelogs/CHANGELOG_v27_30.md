# Nouba v2.7.30 — Règle métier Agent + harmonisation visuelle complète

## 1. Correction métier : appel du suivant verrouillé

Tu as identifié un vrai défaut logique : un agent pouvait appeler le ticket
suivant alors que le ticket en cours n'était ni terminé, ni marqué absent,
ni transféré.

Désormais :

- **Côté serveur** (la vraie sécurité) : `CallNext` refuse l'appel si le
  guichet de l'agent a déjà un ticket au statut « Appelé » non clôturé. Un
  message invite à terminer / marquer absent / transférer d'abord. Ce contrôle
  est côté serveur exprès : masquer le bouton ne suffirait pas (rechargement,
  double-clic, requête forgée).
- **Côté interface** : le bouton « Appeler le suivant » est désactivé tant
  qu'un ticket est en cours, avec un message d'explication clair, traduit en
  français, arabe, anglais et tamazight.

Le verrou se base sur le **guichet** de l'agent : deux guichets d'un même
service ne se bloquent donc pas mutuellement.

## 2. Harmonisation visuelle : tout le produit en or / bleu nuit

Objectif atteint : il n'existe plus AUCUNE couleur violette résiduelle dans
le produit. Toutes les vues partagent désormais la même identité OR (#e8b84b)
sur BLEU NUIT, la même police (Sora + Manrope) et les mêmes composants.

Vues harmonisées dans cette version :

- **Suivi mobile** (`Suivi/Track.cshtml`, `Suivi/Index.cshtml`) : badge
  « en attente » et encadrés passés du violet à l'or, police Manrope chargée.
  C'est l'écran que voit le client en scannant le QR — vérifié sur format
  téléphone.
- **Activation de licence** (`License/Activate.cshtml`) : accent et bouton
  d'activation passés en or pur (fini le dégradé or→violet), texte lisible.
- **Confirmation borne** (`Borne/Confirmation.cshtml`) : accents harmonisés.
- **Console Admin** (`Admin/Index.cshtml`) : derniers violets secondaires
  (KPI « durée », graphiques) passés en or.

Déjà premium et inchangés : Borne, Affichage TV, écran Agent, connexion
admin, connexion agent.

## État final de l'harmonisation

- ✅ Affichage TV (effet wow) — v2.7.27
- ✅ Écran Agent — v2.7.28
- ✅ Console Admin — v2.7.29
- ✅ Suivi mobile, Licence, Confirmation, derniers violets Admin — v2.7.30
- ✅ Connexion agent : déjà au design system
- ✅ Borne & connexion admin : déjà premium

**Plus aucune couleur violette dans le produit. Identité unique sur toutes
les vues.**

## Ce qui n'a PAS changé

- Aucune base de données, aucun modèle modifié.
- Hors la nouvelle règle de `CallNext`, aucune logique métier touchée.
- Tout le travail précédent (son fiable, effet wow) reste en place.

## Fichiers modifiés

- `Controllers/AgentController.cs` — verrou d'appel du suivant.
- `Views/Agent/Index.cshtml` — bouton désactivé + message multilingue + style.
- `Views/Suivi/Track.cshtml`, `Views/Suivi/Index.cshtml` — harmonisation.
- `Views/License/Activate.cshtml`, `Views/Borne/Confirmation.cshtml` — harmonisation.
- `Views/Admin/Index.cshtml` — derniers accents violets → or.
- `Nouba.csproj` — version 2.7.30.

## Vérification

- Suivi mobile rendu et vérifié visuellement (format téléphone).
- Échappement Razor contrôlé sur toutes les vues touchées.
- JavaScript Admin et Agent revalidés.
- Recherche exhaustive : zéro violet résiduel dans tout `Views/`.
- À faire de ton côté : `dotnet build` (le SDK .NET n'est pas disponible
  dans l'environnement de préparation).
