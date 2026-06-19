# PROPOSITION — Descripteur de réglage (tweak) du catalogue

> **Statut : PROPOSITION (Lot C) — à figer côté humain, partagé avec le Lot A.**
> Ce fichier n'est PAS le contrat. Le descripteur n'existe pas encore dans `kings-schemas`.
> Je code le **moteur de sélection** contre ce modèle ; je n'invente pas le **contenu** du catalogue
> (livrable humain validé sur matériel réel — socle §8). Une fois ce format figé dans `kings-schemas`,
> les trois lots régénèrent.

## 1. Champs demandés (ta liste) — le cœur du descripteur

| Champ | Type | Rôle | Qui l'utilise |
|---|---|---|---|
| `id` | string (stable, unique) | Identité du réglage (référencé par `tweakId` dans Plan/OcProposal) | A, C |
| `domain` | enum `gpu·cpu·ram·storage·system·thermal·network` | Domaine du réglage | A, C |
| `condition` | objet (voir §3) | Applicabilité : composant présent, OS, état détecté | C (sélection) |
| `action` | objet/string opaque | Ce qui est écrit (clé registre, état service, paramètre SDK) | **A seul** |
| `revert` | objet/string opaque | Annulation exacte (valeur d'origine) | **A seul** |
| `riskLevel` | enum `veryLow·low·medium·high·veryHigh` | Niveau de risque | A, C |
| `requiresRestart` | bool | Effet au prochain boot | A, C |
| `source` | enum `internal·vendorSdk` | Réglage interne ou via SDK constructeur | A, C |

## 2. Champs SUPPLÉMENTAIRES nécessaires à la sélection (à arbitrer)

Le moteur de sélection ne peut pas remplir le contrat (`OptimizationPlan`, `OcProposal`) sans ces
informations. Je les **signale** (garde-fou #5/C6) plutôt que de les inventer :

| Champ proposé | Pourquoi indispensable | Défaut proposé |
|---|---|---|
| `critical` (bool) | `OptimizationPlan.step.critical` : si vrai et échec ⇒ rollback complet de la transaction. Impossible à remplir sans lui. | `false` |
| **Incompatibilités** | Le plan doit être « sans incompatibilités mutuelles » (Étape 2). Proposé comme **liste séparée** du catalogue `incompatibilities: [{ a, b, reason }]` (calque CDC §13.1), partagée avec A. | — |
| `expectedGainPct` (number ≥ 0) | `OcProposal.step.expectedGainPct` est **requis** par le contrat, et la sélection OC **maximise le gain**. Requis pour les tweaks OC (`source=vendorSdk`, `riskLevel=veryLow`). | `0` |
| `effect` (objet structuré) | Pour calculer `estimatedScoreAfter` / `achievable`, C doit **simuler** l'effet du tweak sur `settingsState` puis recalculer le score. `action` est opaque (interprété par A seul). Proposé : `effect = { domain, field, value }` (ex. `{cpu, powerPlan, "High performance"}`). | — |
| `isOverclocking` (bool) | Distingue les réglages du **plan 1-clic** (optimisation) de ceux du **flux OC opt-in séparé** (`OcProposal`). `source=vendorSdk` ne suffit pas (un profil pilote constructeur n'est pas de l'OC). | `false` |

> **Question d'encodage clé** : `action`/`revert` doivent être interprétables par **A** (exécution) ; or
> C a besoin de connaître l'**effet sur le score** d'un tweak. D'où le champ `effect` séparé (lisible par
> C, déterministe). À valider : un seul champ structuré servant aux deux, ou `action`/`revert` (pour A) +
> `effect` (pour C) distincts (ma proposition).

## 3. Encodage proposé de `condition` (applicabilité)

`condition` doit être **évaluable en données** (le catalogue est distribué en packs, pas en code).
Proposition : une conjonction de clauses typées (toutes vraies ⇒ applicable) ; sinon le réglage est
**ignoré** (jamais appliqué à l'aveugle). Jeu de clauses minimal pour démarrer (extensible) :

```jsonc
"condition": {
  "allOf": [
    { "kind": "storageTypePresent", "value": "SSD" },         // un disque SSD/NVMe existe
    { "kind": "osName", "value": "Win11" },                    // OS requis
    { "kind": "settingEquals", "domain": "storage",
      "field": "trimEnabled", "value": false },                // état détecté à corriger
    { "kind": "chassis", "value": "desktop" }                  // ex. pour l'éligibilité OC
  ]
}
```

## 4. Exemple complet (un des seeds sûrs — TRIM)

```jsonc
{
  "id": "storage.trim.enable",
  "domain": "storage",
  "riskLevel": "veryLow",
  "requiresRestart": false,
  "critical": false,
  "source": "internal",
  "condition": { "allOf": [
    { "kind": "storageTypePresent", "value": "SSD" },
    { "kind": "settingEquals", "domain": "storage", "field": "trimEnabled", "value": false }
  ]},
  "action": { "/* opaque, Lot A */": "fsutil behavior set DisableDeleteNotify 0" },
  "revert": { "/* opaque, Lot A */": "restaure la valeur d'origine constatée" },
  "effect": { "domain": "storage", "field": "trimEnabled", "value": true }
}
```

## 5. Catalogue de test (Lot C) — uniquement des seeds sûrs et réversibles

Conformément à ta consigne, le catalogue de TEST n'utilise que les exemples seed du socle §8.3 :
**plan d'alimentation hautes performances**, **TRIM SSD**, **résolution du timer** — tous `veryLow`,
internes, trivialement réversibles. (Aucun tweak OC dans ce catalogue ⇒ `OcProposal` **vide** = cas
de test C4. Pour tester la **maximisation** du gain OC, j'utiliserai un jeu OC de test clairement
synthétique, isolé dans les tests — jamais présenté comme le vrai catalogue.)

## 6. Ce que je NE fais pas

- Je ne crée pas le **contenu réel** du catalogue (livrable humain, socle §8 / CDC §15).
- Je ne fige pas ce descripteur dans `kings-schemas` (acte humain, partagé avec Lot A).
- Je ne contourne pas l'absence de champ : tant que `effect`/`critical`/incompatibilités ne sont pas
  arbitrés, le moteur reste codé contre ce modèle proposé, ajustable sans coût.
