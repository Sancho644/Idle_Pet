# Technical Design Document — 3D Cozy Tamagotchi

- Version: 0.2 | 25.11.2026
- Status — DESIGN and T01 are complete and accepted by the user. T01 integration into main awaits authorization to merge. [Decision register](../decisions.md)
- Design baseline — [GDD v1.3](../design/GDD.md), SD-01–SD-15, TD-007–TD-017 and the accepted audit. Platform delivery remains open under T04.
- DESIGN finalization — R-01–R-05 are incorporated. TD-015–TD-017 close the last arithmetic, scheduling and PCG32 questions. Version 0.2 is retained. [Simulation engine contract](simulation-engine.md)
- Target: Windows (Steam), Android (Google Play), free-to-play, single active pet, 3D cozy room.

## 1. Architectural goals

Modular Clean Architecture adapted to Unity; no ECS at MVP scale. Domain is pure C# (no UnityEngine, Unity lifecycle, I/O or wall clock reads). Application orchestrates use cases. Infrastructure implements application ports. Presentation converts input into commands and read models into visuals. Bootstrap wires dependencies. Do not duplicate rules for live, offline and forecasting. Prefer composition and configuration over inheritance by species. Balance and open GDD rules remain configurable or explicitly unresolved.

## 2. Stack and package status

Pinned tools: Unity 6000.6.7f1, URP 17.6.0, Unity CLI and Pipeline 1.0.1, and official Unity Agent Plugin 0.1.10-beta. See [setup.md](setup.md) for verification results.

Use constructor injection. VContainer stays deferred until Bootstrap. TD-008 approves ScriptableObject Authoring and Unity Localization String Tables. Localization is specified here and package installation requires separate approval. Utility AI with an Action FSM follows SD-10. C# events for committed state changes, JSON storage, AI Navigation, Animator, blend shapes and procedural animation remain implementation options where relevant. Use the installed Unity Test Framework, Input System and UGUI where needed. UniTask and Addressables remain deferred. Git LFS follows TD-004. Blender and Mixamo follow the [art pipeline proposal](../art/blender-pipeline.md). Real-money payments stay outside MVP. T04 owns platform notification package and adapter evaluation.

## 3. Assemblies and folders

T00 created `IdlePet.Domain` (`noEngineReferences: true`), `IdlePet.Application` (references Domain), and `IdlePet.Tests.EditMode` (Editor-only, references Domain and Application). All project assemblies and namespaces use `IdlePet.*`. The tree below preserves the planned architecture. Add its other assemblies and folders as the corresponding code arrives.

```text
Assets/_Project/
  Runtime/
    Domain/             IdlePet.Domain.asmdef
      Pet/{Needs,Care,Lifecycle,Personality,Evolution,Behaviour}/
      Simulation/
      Room/{Grid,Furniture,Modifiers}/
      Progression/{Inventory,Economy,Collection}/
    Application/        IdlePet.Application.asmdef
      UseCases/ Ports/ ReadModels/ Notifications/ GameFlow/
    Infrastructure/     IdlePet.Infrastructure.asmdef
      Persistence/ Time/ Random/ Notifications/
    Presentation/       IdlePet.Presentation.asmdef
      Pet/ Room/ Building/ UI/ Input/ Lighting/ Audio/
    Bootstrap/          IdlePet.Bootstrap.asmdef
    Authoring/          IdlePet.Authoring.asmdef
  Content/{Pets,Personalities,Evolutions,Furniture,Rooms,Items,Balance}/
  Art/{Characters,Environment,Materials,Animations,Audio}/
  Prefabs/ Scenes/ Plugins/ Editor/
  Tests/
    EditMode/           IdlePet.Tests.EditMode.asmdef
    PlayMode/           IdlePet.Tests.PlayMode.asmdef
```

Dependencies (A -> B means A references B): Application -> Domain; Infrastructure -> Application, Domain; Presentation -> Application, Domain (read-only contracts preferred); Authoring -> Domain; Bootstrap -> all needed runtime assemblies. Domain has no engine references; application should remain pure C#. No circular references. Split platform-specific plugins and adapters with asmdef platform filters where appropriate. An Editor notification stub is a T04 option. Unity assets are mapped to validated runtime definitions at boot, not mutated as live state. Test assemblies reference only relevant targets.

## 4. Runtime state and ownership

`GameState` is the single authoritative aggregate. It contains the logical simulation cursor, persisted RNG streams, the current pet and its schedules, rooms, placements, resources, waste, settings, progression and generation album. `PetState` includes stable instance and definition IDs, life stage or terminal status, needs, age/weight, personality, traits, preferences, habits, illnesses, action and digestion progress, and care history. Room state includes logical accessibility, cleanliness inputs, lighting and source positions. Furniture state keeps instance identity and contents. Comfort and ToiletUrgency are derived from their inputs. Unity objects, view references and Animator state stay outside the save.

There is at most one living pet. The player explicitly starts the next generation after terminal death. Room layouts and state, furniture, resources, inventory, currency, collection, waste and generation records persist as specified by SD-03 and SD-13. The next pet has its own state. [Lifecycle and inheritance](../design/pet-simulation.md)

Application serializes commands and simulation requests. Before validating a player command, advance to its logical time and stabilize events at that timestamp. A valid action consumes resources and applies effects once at its logical event. A rejected command preserves elapsed time and events that naturally occurred before the command. The rejected action changes no state, consumes no resources or action RNG, and grants no rewards. Handle a diagnostic engine failure separately under TD-016. Store the identity or progress needed to prevent repeat effects across resume and save/load. Presentations receive read models and publish input commands. Notifications, animation and navigation have no independent mutation path. [R-04 and command boundaries](simulation-engine.md)

## 5. Event-driven simulation and logical time

The canonical contract for logical time, fixed-point arithmetic, scheduling, boundaries, RNG and Forecast is in the [simulation engine specification](simulation-engine.md). TD-009 uses one monotonic `long` cursor in milliseconds. The three clock projections use this cursor, with separate phases and schedules. Application and Infrastructure translate external time and trust into a logical target. T03 owns the platform adapters and reconciliation. [Circadian rhythm](../design/circadian-rhythm.md) · [Time integrity](time-integrity.md)

Integrate between boundaries where rates or rules change. TD-015 defines the canonical fixed-point numerator, preserved remainder and single rounding of the modified rate. TD-011, TD-012 and TD-016 define causal event ordering, `(currentTime, targetTime]`, stabilization and checkpoint recovery. Equal input, rules and event histories give equal state, ordered effects and RNG across interval partitions. Apply effects exactly once. Full elapsed time is covered under GD-003. At terminal death, eligible world transitions can continue and the next egg requires player action. [Gameplay rules](../design/pet-simulation.md) · [Balance and modifier cases](../design/balance-v0.1.md)

Live and offline call the same engine. TD-017 specifies PCG XSH RR 64/32 version 1, independently persisted streams and reference vectors. Visual randomness stays isolated. Forecast advances a deep copy of GameState, event progress, remainders and RNG without external side effects. The on-disk representation of 64-bit values remains a T03 decision. [Open items](../open-items.md)

## 6. Care, life cycle and terminal state

The nine core characteristics, five lifecycle stages, six MVP illnesses and generation rules are specified in [SD-01–SD-08](../design/pet-simulation.md). The [balance document](../design/balance-v0.1.md) owns initial values and tuning. Free basic care covers ordinary food, cleaning, treatment and the suitable basic night light. Store the reason and time of death. Only player action starts another egg after terminal death.

Care commands validate lifecycle, logical access, availability and conditions. Eating consumes a provisioned portion and applies its effect once. Digestion subsequently adds waste once per portion. Cleaning changes the room waste state. Treatment starts recovery under its own rules. Age, illness and digestion continue during sleep. Healing, modifiers and events use their canonical domain channels. [Digestion and waste](../design/digestion-and-waste.md) · [Lighting and sleep](../design/lighting-and-sleep.md)

## 7. Behaviour, actions and presentation

Utility AI ranks eligible actions using needs, personality, traits, habits and room state. Critical-care actions have priority over entertainment. The Action FSM stores logical start and end, target, progress and effect identity. Minimum durations, cooldown, commitment and repeat penalties are candidate safeguards against thrashing. [SD-10](../design/pet-simulation.md)

Logical position, path/reachability facts, duration and resource accessibility determine outcomes. Unity navigation, physics and animations project those facts. A rendering or animation failure can replan presentation. A layout change or loss of logical access can require domain interruption. Animation callbacks do not apply care effects. Use capabilities and movement/animation adapters for humanoids, quadrupeds and later anatomy families. The planned test families and art pipeline remain proposals. [Art pipeline](../art/blender-pipeline.md)

## 8. Room, furniture, lighting and sleep

The logical grid owns placement, resource accessibility and interaction constraints. Evaluate bounds, occupancy, compatible capabilities and access to essential care. Inventory transfer and placement must conserve contents and capacity. A filled litter tray must be cleaned before removal. Furniture removal cannot create or destroy resources. Emptying or transferring feeder contents, grid size, rotation and layers are open. Ordinary furniture does not wear out in MVP. Same-type passive effects use configured diminishing returns. Active effects apply once during the action that uses the item. [Furniture rules](../design/pet-simulation.md) · [Waste rules](../design/digestion-and-waste.md)

### Proposed data structures for lighting

`PetTraits`, `RoomLightingState`, `LightSourceDefinition` and `LightSourceState` are candidate structure names. Use immutable definitions with stable IDs for source data, and persist runtime switches and placements as room state. `Lighting` is an approved furniture category. Natural light follows `WorldClock`. Main light is global and configured sources can contribute to local light within a radius.

A pure `SleepQualityCalculator` is proposed to combine trait, logical light, furniture, health and cleanliness inputs. The formulas, intensity thresholds, spatial coverage and coefficient composition remain open. Main-light requests come from the authoritative sleep/care logic. Night sleep follows `CircadianClock`, including remaining time after interruption. Forecast reads the same logical inputs. Presentation maps them to Unity lights without Unity renderer or graphics raycasts affecting domain values. [Lighting and sleep](../design/lighting-and-sleep.md)

## 9. Save, restore and versions

Store schema, simulation rules, balance, content and RNG algorithm versions separately. Keep the logical cursor, external time anchors and source trust with the same snapshot as GameState, resources, event progress, deduplication and RNG. Definitions stay immutable. Saves reference stable IDs. Already processed events and intervals are irreversible. [Configuration](configuration-system.md) · [Time integrity](time-integrity.md)

The proposed T03 storage uses a versioned envelope, platform-appropriate atomic write/replace, a validated previous backup and explicit distinction between first run, corrupt data and unsupported versions. JSON in Unity's persistent data directory is a candidate. Sequential migration, missing ID handling and policy for an offline interval spanning a rule change are open. Avoid applying new rules retroactively without an approved migration policy. Use atomic saves at critical state changes and appropriate lifecycle events. Verify pause/resume, repeated callbacks and Android process kill during T03. Cross-device save sync stays outside MVP.

## 10. Forecast and notifications

The approved forecast runs on an independently cloned GameState and RNG, with no external side effects. Significant actions or state changes trigger recomputation, stale notices are canceled, and duplicates are suppressed. Default quiet hours respect the player's night rest and have no effect on simulation. The in-game light request follows GD-012. The exact platform presentation of that request still needs a decision. [Notification specification](../design/notifications.md)

T04 owns Android and Windows adapter feasibility and package approval. Windows Toast in an unpackaged Steam-compatible player needs verification. Delivery is best effort. No platform adapter or delivery test has been completed during DESIGN.

## 11. Game flow and scene layout

Proposed flow: Bootstrap → load and validate → offline catch-up → death summary, egg setup or room gameplay. The room has care and build modes. Continuing life in menus and building is an SD-02 proposal pending a precise decision. Backgrounding saves state. Resume processes the elapsed interval and restores presentation. The notifications workflow is separate.

Bootstrap is the composition root. Room may host the pet view, lighting, grid, camera and UI. Minigame scenes and additive loading are later options. Scene recreation preserves canonical state. Form changes select view assets by stable IDs. Presentation, rooms and scenes do not run independent simulation clocks.

## 12. Data-driven authoring and localization

TD-008 requires authored values for all tunable rules, thresholds, rates, probabilities, timings, content definitions, links to assets, visual settings and audio. TD-014 fixes the six authoring types for T01. `GameConfigSO` links specialized configs and catalogs as root data. Config Mappers build validated, immutable pure C# definitions. Constructor DI gives consumers the definitions. Player settings and mutable runtime state are stored separately. Production has no hidden hardcoded defaults, and test data remains explicit. Adding content within existing mechanics requires configuration only. [Configuration system and T01 scope](configuration-system.md)

Unity Localization String Tables own all user-facing text, including dynamic messages, notifications, errors and tutorials. Content and configuration store references and stable keys. Domain sends event/reason IDs and values. Presentation formats numbers, time and units through the selected locale. Installing the package requires separate approval. [Localization](localization.md)

Automatic validation checks required links, ranges, uniqueness, referential integrity, compatible combinations, localization and rules needed for essential care. Critical errors block builds once the relevant validation exists. Different validation coverage and catalogs are introduced as systems become available. [Coding standards](coding-standards.md)

## 13. Automated verification and Balance Simulation Lab

Use the [T01 acceptance scenarios](simulation-engine.md) for EditMode: logical time, fixed-point precision and reversal at bounds, snapshots and partitions, ordering and stabilization, exactly-once effects, PCG32 streams and Forecast. T01 verifies a minimal terminal state. Full gameplay death rules and their tests belong to T02. Ruleset changes, save migration, resource conservation, sleep and light and digestion are verified as their systems are delivered.

The Balance Simulation Lab runs on the same simulation engine, with structured scenarios, profiles and reproducible seeds. T01.03b adds an Editor-only window under Tools → Idle Pet → Balance Simulation Lab, with XML export and structural comparison of AdvanceTo and Forecast. IdlePet.Editor references IdlePet.Application. [Editor workflow and profiles](balance-simulation-lab.md) `Production`, `FastTesting` and `StressTesting` are separate profiles. Release content uses Production. Measure on target devices, and treat proposed performance budgets as hypotheses until measured. [Balance, scenarios and profiles](../design/balance-v0.1.md)

PlayMode and device checks cover controls, logical-to-visual state, including light and navigation, background/kill, and later platform notifications. Physical Android launch and visual Windows verification remain first-playable tasks. T00's existing results are listed in [setup.md](setup.md).

## 14. Implementation sequence

T00, DESIGN and T01 are complete and accepted. T01 delivered Domain Foundation, Simulation Core, and Forecast & Verification. T02 requires a separate user request after T01 is integrated into main. Complex care, sleep, illnesses, platform time, digestion, furniture, evolution and notifications arrive in later stages. [Roadmap](../roadmap.md) · [Backlog](../backlog.md)

## 15. Open decisions and risks

See [open-items.md](../open-items.md) for open design and technical decisions and [setup.md](setup.md) for the current project status. The preliminary MVP content target is two base species with two adult forms per species, four adult forms total. The quantities remain open.

| Risk | Proposed mitigation and verification |
|---|---|
| Simulation differs across partitions or events share a timestamp | Apply TD-009–TD-013 and TD-015–TD-017. Compare state, ordered events and RNG across one advance and multiple advances. Aggregate only spans or cycles with tested equivalence. [Acceptance cases](simulation-engine.md) |
| Unity navigation or animation becomes authoritative | Derive accessibility and action durations as domain inputs, define cancellation semantics, and commit effects once. Give each anatomy profile an accessible interaction or fallback for essential care. |
| Resume applies offline time twice or an Android process kill interrupts a save | Serialize commands and resume processing. Save state, logical cursor, time anchors and RNG together. Test interrupted writes, backup recovery and duplicate callbacks. Verify the selected serializer on Windows Mono and Android IL2CPP. |
| Changes to content or rules alter existing saves | Version save schema, rules, balance, content and RNG separately. Preserve stable IDs and define migration and missing-ID behavior before T03. |
| Forecast consumes live RNG or platform delivery influences state | Forecast on an independent snapshot with its own RNG. Keep notification delivery outside simulation. Verify registration, scheduling and activation in the Windows T04 spike. |
