# Descripteur de réglage (tweak) du catalogue — validé A/C, prêt à figer

> **Statut : VALIDÉ par le Lot A** avec 3 ajustements intégrés (ci-dessous). Contrat **partagé A/C**.
> **Côté C : prêt à figer.** Le gel effectif dans `kings-schemas` (nouvelle version + régénération des
> 3 lots) reste un **acte humain** ; le format exact de `action`/`revert` est **finalisé par A**.
> Le moteur de sélection du Lot C est codé contre ce modèle.

## Ajustements du Lot A (intégrés)
- **(a)** `isOverclocking` ajouté (voir §2). ✔
- **(b)** `action`/`revert` sont **possédés et définis par A** (TweakEngine), **opaques pour C** : C ne
  les lit ni ne les interprète jamais. A en publie le **schéma d'action-données** (types
  `registry`/`service`/`powerPlan`/`vendorSdk` + params) : `kings-core/docs/PROPOSAL_tweak-action-schema.md`,
  à figer dans `kings-schemas` **à côté** du descripteur. ✔
- **(c)** `settingEquals.{domain,field}` (et `effect.{domain,field}`) sont **alignés sur les noms EXACTS
  de `settingsState`** du `SystemSnapshot` (= ce que produit A). Table de référence en §3.1. ✔

## Consensus A/C (round 2, 2026-06-20)
- `action`/`revert` **structurés pour A**, opaques pour C. `revert` = la **méthode** ;
  `revert.strategy = "restoreCaptured"` par défaut ; la **valeur d'origine** est capturée à l'exécution
  et **journalisée par A** (jeton de revert auto-suffisant, rejouable sans relire le catalogue — A3/A4).
  C n'en dépend pas.
- `effect` (champ **C**, lisible, pour simuler le score) reste **distinct** de `action`/`revert` (champ **A**). Pas de fusion.
- `isOverclocking` : **lu par C** pour router (plan 1-clic ↔ `OcProposal`). C **exclut tout tweak
  `isOverclocking` du flux 1-clic** (cohérent **A5**) et ne le propose qu'en OC `veryLow`.
- `requiresStabilityTest` : champ **A-owned** du descripteur ; C garde `OcProposal.requiresStabilityTest = true` en v1.
- Un tweak dont le **type d'action n'est pas encore implémenté/testé par A** est **refusé par A** (jamais
  à l'aveugle) + **deny-list** de clés/services protégés côté A : C ne pré-filtre pas par type (opaque) et
  compte sur ce refus comme défense en profondeur ; le tweak ressort alors en `skipped` du rapport.

## 1. Champs cœur du descripteur

| Champ | Type | Rôle | Qui l'utilise |
|---|---|---|---|
| `id` | string (stable, unique) | Identité du réglage (référencé par `tweakId` dans Plan/OcProposal) | A, C |
| `domain` | enum `gpu·cpu·ram·storage·system·thermal·network` | Domaine du réglage | A, C |
| `condition` | objet (voir §3) | Applicabilité : composant présent, OS, état détecté | C (sélection) |
| `action` | **opaque — format défini par A** | Ce qui est écrit (registre, service, SDK) | **A seul** |
| `revert` | **opaque — format défini par A** | Annulation exacte (valeur d'origine) | **A seul** |
| `riskLevel` | enum `veryLow·low·medium·high·veryHigh` | Niveau de risque | A, C |
| `requiresRestart` | bool | Effet au prochain boot | A, C |
| `source` | enum `internal·vendorSdk` | Réglage interne ou via SDK constructeur | A, C |

## 2. Champs additionnels (validés A)

| Champ | Type | Pourquoi | Défaut |
|---|---|---|---|
| `critical` | bool | `OptimizationPlan.step.critical` : si vrai et échec ⇒ rollback complet | `false` |
| `isOverclocking` | bool (A-owned, **lu par C**) | Sépare le **plan 1-clic** du **flux OC** (`source=vendorSdk` ne suffit pas) | `false` |
| `requiresStabilityTest` | bool (A-owned) | Test de stabilité après OC (A). C garde `OcProposal.requiresStabilityTest = true` en v1. | `true` (OC) |
| `expectedGainPct` | number ≥ 0 | Requis par `OcProposal.step` ; la sélection OC **maximise le gain** | `0` |
| `effect` | `{ domain, field, value }` | C **simule** l'effet sur `settingsState` puis recalcule le score (`estimatedScoreAfter`). `action` reste opaque (A). | — |
| **incompatibilités** | liste séparée `[{ a, b, reason }]` | Plan « sans incompatibilités mutuelles » (calque CDC §13.1), partagée A/C | — |

> **Décidé (b)** : pas de champ unique pour les deux usages. `action`/`revert` (exécution, A) et `effect`
> (simulation de score, C) sont **distincts**. C lit `effect` ; A lit `action`/`revert`.

## 3. Encodage de `condition` (applicabilité, évaluable en données)

Conjonction de clauses typées (toutes vraies ⇒ applicable) ; sinon le réglage est **ignoré** (jamais
appliqué à l'aveugle). Clauses :

```jsonc
"condition": {
  "allOf": [
    { "kind": "storageTypePresent", "value": "SSD" },   // SSD ou NVMe présent
    { "kind": "osName", "value": "Win11" },              // enum hardware.os.name : Win10 | Win11
    { "kind": "chassis", "value": "desktop" },           // hardware.chassis : desktop|laptop|unknown
    { "kind": "cpuVendor", "value": "AMD" },             // hardware.cpu.vendor : Intel | AMD
    { "kind": "settingEquals", "domain": "storage",
      "field": "trimEnabled", "value": false }           // état détecté (voir §3.1)
  ]
}
```

### 3.1 `settingEquals.{domain,field}` — noms EXACTS de `settingsState` (= snapshot de A)

`domain` ∈ `gpu·cpu·ram·storage·system·thermal·network`. `field` ∈ l'un des champs ci-dessous **exactement**
(mêmes noms que `SystemSnapshot.settingsState` dans `kings-schemas`). Idem pour `effect.{domain,field}`.

| domain | fields (type) |
|---|---|
| `gpu` | `driverProfileApplied` (bool) · `hagsEnabled` (bool) · `vendorPerfProfile` (string\|null) |
| `cpu` | `boostEnabled` (bool) · `pboActive` (bool) · `powerPlan` (string) |
| `ram` | `xmpExpoActive` (bool) · `usagePct` (number) |
| `storage` | `trimEnabled` (bool) · `indexingOnSystemDrive` (bool) · `freeSpacePct` (number) |
| `system` | `timerResolutionMs` (number) · `superfluousServicesRunning` (int) · `startupProgramsCount` (int) |
| `thermal` | `measurable` (bool) · `throttlingDetected` (bool) |
| `network` | `tcpOptimized` (bool) · `gameDnsSet` (bool) |

## 4. Exemple complet (seed sûr — TRIM)

```jsonc
{
  "id": "storage.trim.enable",
  "domain": "storage",
  "riskLevel": "veryLow",
  "requiresRestart": false,
  "critical": false,
  "isOverclocking": false,
  "source": "internal",
  "condition": { "allOf": [
    { "kind": "storageTypePresent", "value": "SSD" },
    { "kind": "settingEquals", "domain": "storage", "field": "trimEnabled", "value": false }
  ]},
  "action": "<<opaque — format défini par A>>",
  "revert": "<<opaque — format défini par A>>",
  "effect": { "domain": "storage", "field": "trimEnabled", "value": true }
}
```

## 5. Catalogue de test (Lot C) — seeds sûrs uniquement

Le catalogue de TEST n'utilise que les seeds du socle §8.3 (plan d'alim, TRIM, timer — `veryLow`,
internes, réversibles). Aucun OC ⇒ `OcProposal` vide (cas C4). La maximisation OC est testée via un jeu
OC clairement **synthétique**, isolé aux tests — jamais le vrai catalogue.

## 6. Limites & gel
- C ne crée pas le **contenu réel** du catalogue (livrable humain, socle §8 / CDC §15) ni ne lit `action`/`revert`.
- C ne fige pas dans `kings-schemas` (acte humain). **Côté C, le format est prêt à figer** ; A finalise `action`/`revert`.
- Après gel : les 3 lots régénèrent ; je remplacerai les conditions par délégués (catalogue de test) par un
  évaluateur **piloté par données** (compile `condition`/`effect` depuis le pack).
