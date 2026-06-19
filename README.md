# King's Optimization — Lot C (cloud & score)

Lot C du logiciel **King's Optimization**. Deux moitiés :

1. **`KingsScore`** — bibliothèque PURE : moteur de score déterministe + sélection des réglages
   (plan d'optimisation, proposition d'overclocking). Aucune dépendance infra (ni PostgreSQL,
   ni Docker, ni HTTP).
2. **`KingsCloud.Api`** *(à venir)* — backend ASP.NET Core 8 + PostgreSQL : API REST, leaderboard
   anti-triche, licences, packs signés, RGPD.

> Source de vérité du contrat : le sous-module **`schemas/` épinglé sur `kings-schemas v1.0.1`**
> (le v1.0.0 était une ébauche cassée, abandonnée). On ne réécrit jamais un schéma à la main :
> les types C# sont **générés** depuis `schemas/json-schema/objects/`.

## Prérequis

- **.NET 8 SDK** (`dotnet --version` ⇒ 8.0.x). Installé en user-local sous
  `%LOCALAPPDATA%\Microsoft\dotnet` ; ajouter ce dossier au PATH si `dotnet` n'est pas trouvé.
- Docker (uniquement pour la partie backend / PostgreSQL, étape ultérieure).

## Build & tests

```bash
dotnet build KingsCloud.sln
dotnet test  tests/KingsScore.Tests/KingsScore.Tests.csproj
```

Le **premier test** est la reproductibilité du score contre les fixtures partagées
(`schemas/fixtures/`), suivi du test clé de redistribution des poids (neutralisation thermique).

## Génération des types (depuis le contrat)

Les objets pivots sont générés via **NJsonSchema** par l'outil `tools/SchemaGen` :

```bash
dotnet run --project tools/SchemaGen -- "schemas/json-schema/objects" "src/KingsScore/Contracts/Generated"
```

Sortie : `src/KingsScore/Contracts/Generated/*.g.cs` (un namespace par objet). À relancer après
toute évolution **versionnée** du sous-module `schemas/`. On ne modifie jamais les `*.g.cs` à la main.

## Structure

```
schemas/                     sous-module kings-schemas v1.0.1 (contrat, source de vérité)
proposals/                   propositions à figer côté humain (descripteur de tweak, leviers de score)
src/KingsScore/
  Contracts/Generated/       types C# générés depuis les JSON Schema
  Json/                      convertisseur STJ honorant EnumMember (valeurs exactes du contrat)
  Scoring/                   barème, fonction de points (graduée), modèle, moteur de score
  Catalog/                   modèle de catalogue (descripteur proposé, ICatalog)
  Selection/                 moteur de sélection (plan d'optimisation + proposition OC veryLow)
tests/KingsScore.Tests/      tests xUnit (reproductibilité, redistribution, échelle, sélection)
tools/SchemaGen/             générateur de types (NJsonSchema) — outil de dev
```

## Décisions & invariants (Lot C)

- **Déterminisme (C1)** : mêmes entrées + même `weightsetVersion` ⇒ même score. `scoreId` et
  `computedAt` sont des métadonnées NON déterministes, **injectées par l'appelant** (jamais générées
  dans le moteur). La comparaison de reproductibilité les exclut (cf. fixtures).
- **Redistribution (C2)** : poids en `decimal`, pleine précision ; la somme reste **100** par
  l'identité `Σ base_d (mesurables) = S`. Arrondi **uniquement à l'affichage** (poids/normalized à
  1 décimale en sortie). Aucun domaine n'« absorbe » l'écart d'arrondi.
- **Barème séparé de la fonction de points (C8)** ; versionnés ensemble sous `weightsetVersion`
  (changer l'un OU l'autre bump la version, sinon la reproductibilité serait rompue).
- **Score gradué** : chaque domaine est noté sur plusieurs sous-réglages (points partiels), 100 par
  domaine seulement si tout est optimal. Leviers détectables : `proposals/score-levers.md`.
- **Sélection (Étape 2)** : `OptimizationPlan` (réglages applicables, sans incompatibilités, ordonnés,
  `estimatedScoreAfter` par re-score) ; `OcProposal` zone `veryLow` uniquement, gain maximisé, sinon
  vide ou « non éligible » motivé (C4). Codé contre le descripteur proposé + un catalogue de test.
- **Anti-triche (C3)** : *(backend, à venir)* le score du leaderboard est recalculé côté serveur à
  partir du snapshot brut ; jamais la valeur cliente.

## En attente de validation / livrables humains

- **Fonction de points v0** (`PointsFunctionV1`) : proposition. Les `expectedScoreResult` des
  fixtures restent **illustratifs** tant qu'elle n'est pas validée, puis figés par l'équipe (C7).
  Scores v0 actuels : no-thermal `global=15`, high-end `global=27` (achievable provisoire = 100).
- **Catalogue de réglages** : non fourni. Nécessaire pour `achievable` précis, le plan
  d'optimisation, la sélection OC et le payload des packs.
- **`rawMetrics`** (leaderboard) : sous-ensemble du SystemSnapshot à figer (point ouvert O2).
- Précision d'affichage du `weight` retenue : **1 décimale** (alignée sur les fixtures).
```
