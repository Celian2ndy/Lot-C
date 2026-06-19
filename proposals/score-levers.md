# Leviers de score détectables, par domaine (inventaire honnête)

> But : savoir ce qu'on peut **noter honnêtement**. Règles : aucun levier non détectable de façon
> sûre, aucune mesure inventée. **FPS** noté jamais (affiché seulement si mesuré) ; **input lag** =
> estimation, jamais notée. Le score mesure l'**optimisation atteinte**, pas la puissance matérielle
> (un PC d'entrée de gamme parfaitement réglé peut atteindre 100).
>
> Statuts : **[Noté]** = noté dès maintenant (champ déjà dans `SystemSnapshot` v1.0.1) ·
> **[Snapshot+]** = détectable sûrement mais **champ à ajouter au contrat** · **[Catalogue]** =
> dépend du vrai catalogue (livrable humain) → *à compléter quand le catalogue est figé* ·
> **[Non noté]** = mesure/outcome, volontairement hors score.

Notation **graduée** : les leviers numériques (timer, services, démarrage, espace, températures)
donnent des **points partiels** (rampe), pas du tout-ou-rien. Un domaine n'atteint 100 que si **tous**
ses sous-réglages sont optimaux ⇒ 100 reste rare.

## GPU
| Levier | Détection sûre | Statut |
|---|---|---|
| Profil pilote de performance appliqué | NVAPI/ADLX ou registre pilote (lecture) | **[Noté]** `settingsState.gpu.driverProfileApplied` |
| Profil de perf constructeur actif | SDK constructeur / registre | **[Noté]** `settingsState.gpu.vendorPerfProfile` |
| HAGS (ordonnancement GPU matériel) | registre `HwSchMode` | **[Catalogue]** valeur optimale **config-dépendante** → non notée tant que le catalogue ne dit pas « bon pour cette config » |
| Mode faible latence, gestion alim « perf max », cache shaders, V-Sync | pilote/registre | **[Snapshot+]** + **[Catalogue]** (chaque réglage = des points une fois figés) |
| Pilote à jour | `hardware.gpus[].driverVersion` existe | **[Catalogue]** « à jour » nécessite une base de versions (cloud/pack) |

## CPU
| Levier | Détection sûre | Statut |
|---|---|---|
| Boost CPU actif | `powercfg` / registre | **[Noté]** `settingsState.cpu.boostEnabled` |
| Plan d'alimentation | `powercfg /getactivescheme` (GUID) | **[Noté gradué]** `settingsState.cpu.powerPlan` (High perf = plein, Balanced = partiel, Économie = 0) |
| PBO actif | SMU/Ryzen Master (AMD) | **[Noté si AMD]** `settingsState.cpu.pboActive` (gating sur `cpu.vendor`) |
| Core parking, % min/max état proc., boost mode | `powercfg` | **[Snapshot+]** + **[Catalogue]** |

## Système / OS
| Levier | Détection sûre | Statut |
|---|---|---|
| Résolution du timer | `NtQueryTimerResolution` | **[Noté gradué]** `settingsState.system.timerResolutionMs` (≤1 ms = plein, 15,6 ms = 0) |
| Services superflus en cours | comptage parmi une **liste validée** | **[Noté gradué]** `settingsState.system.superfluousServicesRunning` — *quels services sont « superflus » = **[Catalogue]*** |
| Programmes au démarrage | Run/Startup/tâches | **[Noté gradué]** `settingsState.system.startupProgramsCount` |
| Réglages registre ciblés (GameDVR, Game Mode, fullscreen opt., réactivité) | lecture registre | **[Catalogue]** chaque tweak validé = des points |

## RAM
| Levier | Détection sûre | Statut |
|---|---|---|
| Profil XMP/EXPO actif | SPD/BIOS | **[Noté si dispo]** `settingsState.ram.xmpExpoActive` (gating sur `hardware.ram.modules[].xmpExpoProfileAvailable`) |
| Taux d'utilisation mémoire | mesure instantanée | **[Non noté]** charge transitoire, pas un levier (`ram.usagePct`/`metrics.ramUsagePct`) |
| Compression mémoire, pagefile, SysMain | services/API | **[Snapshot+]** + **[Catalogue]** |

## Stockage
| Levier | Détection sûre | Statut |
|---|---|---|
| TRIM activé | `fsutil behavior query DisableDeleteNotify` | **[Noté si SSD/NVMe]** `settingsState.storage.trimEnabled` |
| Indexation OFF sur disque système | service/attribut | **[Noté]** `settingsState.storage.indexingOnSystemDrive` (false = bon) |
| Espace libre disque système | API disque | **[Noté gradué]** `settingsState.storage.freeSpacePct` (≥20 % = plein, ≤5 % = 0) |
| Santé S.M.A.R.T. du disque système | SMART | **[Noté gradué]** `hardware.storage[].smartHealth` (OK = plein, Warning = partiel, Critical = 0, Unknown = exclu) |
| Write caching, défrag planifiée (HDD) | registre/service | **[Catalogue]** |

## Thermique
| Levier | Détection sûre | Statut |
|---|---|---|
| Absence de throttling | compteurs / capteurs | **[Noté]** `settingsState.thermal.throttlingDetected` |
| Marge de température CPU/GPU en charge | capteurs (`metrics.*TempLoadC`) | **[Noté gradué]** marge d'autant meilleure que la température est basse |
| Aucun capteur exploitable | — | **Domaine NEUTRALISÉ**, poids redistribué (jamais inventer une température) — `settingsState.thermal.measurable=false` |

## Réseau
| Levier | Détection sûre | Statut |
|---|---|---|
| Réglages TCP optimisés | registre TCP/IP | **[Noté]** `settingsState.network.tcpOptimized` |
| DNS de jeu configuré | config DNS | **[Noté]** `settingsState.network.gameDnsSet` |
| Nagle (TcpAckFrequency/TCPNoDelay), NetworkThrottlingIndex, QoS | registre | **[Snapshot+]** + **[Catalogue]** |

## Volontairement HORS score (outcomes / mesures)
- **FPS** (`metrics.fpsSample`) : affiché **seulement s'il est mesuré**, jamais noté.
- **Input lag** (`metrics.inputLagEstimate`) : **estimation** par indicateurs, jamais une mesure exacte, jamais notée.
- **Charge mémoire** instantanée : transitoire, non notée.
- **Puissance matérielle brute** (cœurs, VRAM, fréquence) : hors score (on note l'optimisation, pas le matériel).

---

### Synthèse pour le barème profond
- **Notable maintenant** (contrat v1.0.1) : ~2–4 sous-réglages par domaine, gradués pour les numériques.
- **Profondeur supplémentaire** : viendra de **[Snapshot+]** (champs à ajouter au contrat, partagé Lot A)
  et surtout du **[Catalogue]** (chaque tweak validé devient un sous-réglage notable). C'est pourquoi
  **le barème profond et les `expectedScoreResult` se figeront AVEC le catalogue réel**, pas avant.
