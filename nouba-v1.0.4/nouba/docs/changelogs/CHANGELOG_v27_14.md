# Nouba v2.7.14 — Display vraiment responsive (Smart TV / PC / tablette / smartphone)

## Le vrai bug

Tu m'as dit « les thèmes ne s'affichent pas bien sur la TV à cause de la taille ».
Diagnostic réel : ce ne sont **pas** les thèmes (couleurs) qui sont en cause.
C'est le **layout du Display** qui figeait des tailles en pixels :
- Logo : `120px × 72px` figé → minuscule sur TV 65", écrasant sur smartphone.
- Nom du site : `22px` figé.
- Header centre : `26px` figé.
- Horloge : `30px` figé.
- Date : `13px` figé.
- En-tête table : `28px` figé.
- Hauteur ligne entête : `88px` figé.
- Titre historique : `20px` figé.
- Sous-titre historique : `14px` figé.

Sur Smart TV, ces tailles paraissaient ridiculement petites. Sur smartphone,
elles débordaient. Aucun thème n'aurait pu compenser ça.

## Correctif : tout en clamp() avec viewport units

J'ai remplacé toutes ces tailles figées par `clamp(min, vw-based, max)` pour
qu'elles s'adaptent automatiquement à n'importe quelle taille d'écran :

| Élément | Avant | Après |
|---|---|---|
| Logo | 120×72 px figé | `clamp(56px,8vw,160px) × clamp(40px,6.5vh,96px)` |
| Nom du site | 22px | `clamp(14px, 1.6vw, 32px)` |
| Header centre | 26px | `clamp(15px, 2vw, 40px)` |
| Horloge | 30px | `clamp(18px, 2.2vw, 44px)` |
| Date | 13px | `clamp(10px, .9vw, 18px)` |
| Hauteur header | 110px figé | `clamp(80px, 10vh, 140px)` |
| Hauteur ligne entête | 88px figé | `clamp(56px, 8vh, 120px)` |
| En-tête table | 28px | `clamp(15px, 1.9vw, 40px)` |
| Ticket principal | clamp(60,7vw,96) | `clamp(48px, 7.2vw, 180px)` *(plage étendue pour 4K)* |
| Compteur | clamp(42,4.8vw,70) | `clamp(34px, 5vw, 130px)` |
| Titre historique | 20px | `clamp(13px, 1.4vw, 28px)` |
| Sous-titre historique | 14px | `clamp(10px, 1vw, 18px)` |
| Padding header | 14×22 px | `clamp(10px,1.4vh,18px) clamp(14px,2vw,28px)` |

## Logo : attention particulière

Le logo a maintenant 3 protections :
1. **Min 56×40 px** — jamais minuscule, même sur petit téléphone.
2. **Max 160×96 px** — jamais écrasant, même sur Smart TV 4K.
3. **Tailles intermédiaires en `vw`/`vh`** — proportion stable à toute taille.
4. **`flex-shrink:0`** — le conteneur ne se réduit jamais en cas de manque de place,
   le nom du site fait l'ellipsis avant de toucher au logo.

## Ménage des media queries

Avant, plusieurs `@media` *re-figeaient* des tailles en pixels (ex: smartphone
ré-imposait `width:86px;height:54px` au logo). Ces overrides écrasaient les
nouveaux clamp(). J'ai retiré toutes ces tailles redondantes des media queries
pour ne garder que les ajustements **structurels** (grid → flex en mode portrait,
masquage du logo footer sur petit écran, etc.).

## Ce que ça change concrètement

| Écran | Avant | Après |
|---|---|---|
| Smartphone (375 px) | logo correct, ticket correct | logo correct, ticket correct |
| Tablette (768 px) | un peu petit | tailles ajustées automatiquement |
| PC laptop (1366 px) | OK | OK (légèrement plus généreux) |
| PC bureau (1920 px) | OK | OK |
| Smart TV 32" (1366 px) | OK | OK |
| Smart TV 50" (1920 px) | logo et textes paraissent petits | proportions ajustées au viewport |
| Smart TV 65" (1920 px ou 4K) | tout parait minuscule | proportions ajustées au viewport |

## Limite à signaler

- Pas de SDK .NET dans mon environnement → pas de `dotnet build`.
- Vérifications passées : 60 fichiers, 0 déséquilibre syntaxique CSS/Razor.
- Je ne peux **pas tester** sur une vraie Smart TV / vrai smartphone — il faut
  vérifier sur tes écrans réels et me dire si quelque chose dépasse ou est mal
  proportionné. Les valeurs `clamp()` que j'ai choisies sont raisonnables pour
  le cas général, mais selon ton logo réel ou ton nom de site, on pourrait
  vouloir ajuster une borne ou une autre.

## Conservé tel quel
Tickets, impression, CSV, langues, SMS, IA admin, agents, guichets, services,
monitoring imprimante, voix Piper, presets thème (8 secteurs), 6 mises en page,
sliders voix, QR de suivi mobile, audio TV (v2.7.13).
