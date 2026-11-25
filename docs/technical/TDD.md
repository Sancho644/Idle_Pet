# Technical Design Document — 3D Cozy Tamagotchi

- Version: 0.1 | 25.11.2026
- Status: technical design proposal. The scope approved for T00 is recorded in [TD-001–TD-006](../decisions.md). The rest of this design remains open.
- Design baseline: GDD v1.1 (GD-001–010). Notifications are a proposed extension, pending approval; Windows scheduling requires a platform spike.
- Target: Windows (Steam), Android (Google Play), free-to-play, single active pet, 3D cozy room.

## 1. Architectural goals

Modular Clean Architecture adapted to Unity; no ECS at MVP scale. Domain is pure C# (no UnityEngine, Unity lifecycle, I/O or wall clock reads). Application orchestrates use cases. Infrastructure implements application ports. Presentation converts input into commands and read models into visuals. Bootstrap wires dependencies. Do not duplicate rules for live, offline and forecasting. Prefer composition and configuration over inheritance by species. Balance and open GDD rules remain configurable or explicitly unresolved.

## 2. Stack (proposed)

Pinned tools: Unity 6000.6.7f1, URP 17.6.0, Unity CLI and Pipeline 1.0.1, and official Unity Agent Plugin 0.1.10-beta. See [setup.md](setup.md) for verification results.

Use constructor injection for actual dependencies. VContainer is planned for composition once Bootstrap exists. Proposed stack for later work: ScriptableObject authoring, C# events for committed state changes, JSON saves, AI Navigation, Utility AI with an action FSM, Animator, blend shapes and procedural animation. Use the installed Unity Test Framework, Input System and UGUI where needed. UniTask and Addressables are deferred until a concrete need. Git LFS follows TD-004. Blender and Mixamo usage follows the [art pipeline proposal](../art/blender-pipeline.md). Real-money payments are outside MVP. Notification packages and adapters are proposals pending separate approval, with Windows Toast feasibility scheduled for T04.

## 3. Assemblies and folders

T00 creates `IdlePet.Domain` (`noEngineReferences: true`), `IdlePet.Application` (references Domain), and `IdlePet.Tests.EditMode` (Editor-only, references Domain and Application). All project assemblies and namespaces use `IdlePet.*`. The tree below preserves the planned architecture. Add its other assemblies and folders as the corresponding code arrives.

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

Dependencies (A -> B means A references B): Application -> Domain; Infrastructure -> Application, Domain; Presentation -> Application, Domain (read-only contracts preferred); Authoring -> Domain; Bootstrap -> all needed runtime assemblies. Domain has no engine references; application should remain pure C#. No circular references. Split platform-specific plugins/adapters with asmdef platform filters where appropriate; editor stub implements notification port. Unity assets are mapped to validated runtime definitions at boot, not mutated as live state. Test assemblies reference only relevant targets.

## 4. Runtime state and ownership

GameState is the single authoritative aggregate: schema version in save envelope, simulation cursor UTC, persisted deterministic RNG state, active pet (zero before first egg or after archival; never more than one living pet), room layouts, inventory, currency, collection, settings, generation history. PetState: instance ID, species ID, form ID, personality ID, life stage, birth time, health/needs, age/weight metrics, care mistakes/history, illness, current logical action and next due events. RoomState: room ID, placements, cleanliness, light settings, available food/other resources. FurnitureInstance: instance ID, definition ID, grid coordinates, rotation, mutable contents. Player progression is separate from pet state; inheritance policy is pending GDD approval. Save stores IDs and serializable values, not GameObjects, asset paths, Transform or Animator state. Runtime definitions immutable, stable IDs never reused; version configuration when changing simulation rules.

Commands mutate state only through application/domain operations. UI receives snapshots/read models, not writable references. Each action validates availability, costs, lifecycle and preconditions before mutation; rejected command has no side effects. Publish events after successful state commit. Use a single serialized command/simulation queue on main thread initially; never let UI, AI or notification callback mutate state concurrently.

## 5. Time and simulation

Two clocks: SimulationClock uses elapsed real UTC time for needs/life; WorldClock derives accelerated day/night from a stored epoch and configured day duration, so visuals and logical routine can advance offline consistently. Use injectable IClock, monotonic elapsed time during a running session where possible, UTC timestamps for persistence. Negative elapsed due to device rollback must not move the simulation backwards; record anomaly, do not punish the pet arbitrarily. Without a trusted server, local clock manipulation cannot be fully prevented.

AdvanceTo(state, targetUtc) processes all elapsed time (GD-003); no offline cap, slowdown or grace solely because the app was closed. Split by meaningful event boundaries: need threshold, sleep/wake, action completion, disease, stage transition, death, world phase if relevant. Integrate rates over stable intervals; clamp bounded needs. Store partial progress and deterministic random state. Resolve simultaneous events in documented stable order; death terminal, no subsequent pet-life actions; rules for ties and exact illness/death thresholds to be approved. Ensure forward progress, guard invalid configuration, use chunks with UI progress if expensive (never silently discard elapsed time). For long absences avoid per-frame/per-second loops; aggregate constant spans and proven equivalent cycles where possible. Age and history update consistently; after death other permitted world state can advance, but do not auto-create the next egg. User explicitly starts next generation. Exact inherited progress remains open.

Live, offline and forecast use the same engine with different callers. Live updates at configured cadence and before every command. Offline catch-up on load/resume. Forecast clones state including RNG, suppresses external effects, returns predicted events without mutating real state. In-room visuals are projections of logical actions, not a separate simulation. Offline actions are abstracted (no NavMesh/physics); if exact parity is required, traversal duration and resource accessibility must be represented in logical state and derived room metadata. Define consistent interruption semantics; visual failures must not cause double rewards. Test equivalent initial state + timestamps + commands yields equivalent results across partitions.

```csharp
public interface IPetSimulation
{
    SimulationResult AdvanceTo(GameState state, DateTimeOffset targetUtc);
}
public interface IClock { DateTimeOffset UtcNow { get; } }
public interface ISaveRepository
{
    GameState Load();
    void Save(GameState state);
}
```

Illustrative synchronous contracts only; result contains updated state and ordered events; caller commits once. Load failure/first-run distinguished by explicit result in implementation. Save I/O can later be async behind port. Engine does not read wall clock. Forecast operates on a deep copy, not shallow object references.

## 6. Pet needs, care and life cycle

Need definitions: satiety, energy, hygiene, health, happiness, boredom, comfort, discipline, bond; age and weight separately. Comfort derived from room state/modifiers rather than independent mutable duplicate; boredom high is bad, other needs high good. Need rates/thresholds configurable. Rules calculate care mistakes on meaningful missed requests, not each tick. Keep aggregate time-weighted history and activity counters for evolution rather than logging each frame. Initial simulation includes food, treats, sleep, hygiene/cleanup, illness/healing, discipline and attention signals, birth, growth, death, new generation. Mini-game integration via reward command. Never let animations apply effects independently.

Use cases: StartGeneration, FeedPet, GiveTreat, RequestSleep, SetLight, CleanWaste, WashPet, TreatIllness, PlayWithPet, DisciplinePet, AdvanceSimulation, ArchiveGeneration. Validate live pet, resources, action compatibility; consume resources and award effects exactly once at documented logical completion (or command start for instant care). No feeding or evolving a dead pet. No new egg during active life. Random species and personality picked once and persisted; personality immutable for that lifetime. EvolutionRuleEvaluator uses care history + environment history + personality, deterministic tie/fallback; transition once; preserve relevant pet identity and state. Timing, exact death conditions, balance and rarity weights pending.

## 7. Behaviour, interaction and animation

Utility AI chooses an available action by need urgency, personality, world phase and available resources. Action FSM executes: Idle -> Choose -> Move -> Interact -> Complete/Cancel -> Idle. Domain action stores logical start/end times, target and effects. Presentation navigation and animation are adapters, report feasibility, and reflect logical actions; failure has explicit cancel/replan policy. Access validation computed when room layout changes, so offline simulation can use persisted or reconstructed accessibility facts. A pet can request food if bowl empty; autonomy must not create infinite free resources. Use cooldown, minimum action duration and deterministic tie handling to avoid thrashing. Sleep and wake follow configurable routine and energy, not unconditional day/night. Animation events may synchronize visuals only, not grant resources or decide outcomes.

Interfaces: IPetActionSelector, IPetActionExecutor (presentation), IPetMovement, IPetAnimation, IInteractionTarget. Profiles: humanoid (Humanoid/Mixamo where compatible), quadruped (Generic rig), blob (blend shapes/procedural), flying (custom movement/rig). Logical actions such as Eat/Sleep/Play shared; definitions provide capabilities, animations, interaction offsets, fallback motions. Initial art target proposed: two anatomy families (humanoid and quadruped), graybox first. Each essential care action needs valid reachable fallback or explicit restriction with equivalent alternative. Avoid switch(speciesId) in core.

## 8. Room, grid, furniture

RoomDefinition has size, valid cells, fixed obstacles, spawn, surface/anchor rules. Grid uses integer coordinates and 90-degree rotations initially; 0.5m cell proposed, not fixed. GridPlacementValidator checks footprint, bounds, occupied cells, reserved access, necessary care reachable. Build mode: select -> preview -> rotate -> validate -> commit or cancel. Inventory transfer and placement atomic. Floor overlay/wall placement separate future layers; initially solid floor objects only. Navigation adapter rebuilds or updates after successful layout edit; verify interaction points reachable and reject layout or roll back safely if blocked. Ground navigation NavMesh; flying uses different adapter later.

FurnitureDefinition: stable ID, footprint, supported actions, interaction slots, effect definitions, price, view prefab reference in authoring. ModifierResolver computes flat and percentage effects with explicit stacking, caps and rounding; proposed additive within category then multiply categories; tune separately. Passive effects only from eligible placed objects; active effect only after successful logical interaction. Avoid unlimited stacking exploit and double count. Room comfort derived from placements and cleanliness, can affect decay or evolution where rules define. Per-room layouts; room switching preserves only one active pet. New room unlock rules and payments later.

## 9. Save and load

SaveEnvelope: schemaVersion, content/rules version, lastSimulatedUtc, payload, optional integrity checksum (not anti-cheat). JSON file in persistentDataPath. Write temp file, flush/close, replace with platform-appropriate atomic strategy where supported, retain previous valid backup. Validate on read, recover backup, migrate sequentially, do not overwrite corrupt data silently. A save timestamp represents state actually simulated to that instant, not arbitrary current time. Save after critical actions (death, new egg, purchases, placements, evolution), periodic autosave, pause/focus lifecycle as appropriate, and quit best effort. Android pause/resume and process kill tested; do not rely only on OnApplicationQuit. Prevent duplicate load/catch-up on multiple lifecycle callbacks. Cloud saves/cross-platform synchronization not in MVP; local saves independent across devices. No automatic data deletion on migration failure.

## 10. Notifications (proposed extension)

NotificationPlanner advances a cloned snapshot through future events (finite scheduling horizon, not a limit on simulation). Derive warnings for hunger, boredom, hygiene, health, critical conditions, growth; avoid messages after predicted death. Deduplicate by generation and event; cancel stale scheduled IDs and recompute after state/config/settings changes or backgrounding. Persist current schedule metadata if useful; reconcile on launch. In-game notices while active, system notices while backgrounded/closed. User controls categories, permissions and optional quiet hours; quiet hours do not pause life. No server required for MVP. Android local notifications via package, runtime permission and delivery restrictions. Windows native toast adapter: validate registration/app identity, scheduled delivery without game process, activation, cancellation and uninstall behavior in unpackaged Steam build. If scheduling unsupported, evaluate opt-in lightweight helper/Task Scheduler or revise platform requirements; do not claim it works until tested. Delivery is best effort (power off, do-not-disturb, OS limits); never drives state. Forecast horizon independent of full elapsed simulation. Settings and concrete notification types need approval.

## 11. Game flow and scene layout

Bootstrap -> Load/Validate -> OfflineCatchUp -> DeathSummary or EggSetup or RoomGameplay. RoomGameplay has CareMode and BuildMode; pause menu does not pause real-time simulation under GD-003 unless design explicitly changes. Background saves and schedules; resume cancels stale notices, catches up then restores presentation. Scene plan: Bootstrap (persistent composition root), Room (room prefab, pet view, lighting, grid, camera, UI), mini-game scene later/additive only when needed. Scene changes do not reset game state. No duplicate persistent managers. Room view owns rendering objects, not canonical state. Evolution replaces visual prefab using form ID; runtime state remains consistent.

## 12. Configuration and content production

ScriptableObjects: PetDefinitionSO, PetFormSO, PersonalitySO, EvolutionRuleSO, NeedBalanceSO, RoomDefinitionSO, FurnitureDefinitionSO, ItemDefinitionSO, NotificationConfigSO. Map to immutable domain definitions, references by IDs. Validate ID uniqueness, missing refs, evolution graph, valid footprints and essential capabilities. Editor tools later. Blender output FBX or selected interoperable format; consistent unit scale, origin, naming, rig family, import preset, collider, animation profile, texture budgets. Mixamo only on compatible humanoids; no expectation of universal animation reuse. Optimize art on actual Android test device, define budgets after profiling. Do not mass-produce art before graybox loop and two anatomy adapters validated.

## 13. Tests and acceptance

EditMode: rate integration, clamping, threshold crossing, deterministic RNG, same outcome for one long vs multiple short advances, offline death, no replacement while alive, evolution history, forecast immutability, inventory/grid validation, modifier stacking, migration, corrupt save recovery, clock rollback and duplicate lifecycle calls. Include 8h/24h/7d/365d scenarios; compare simulation results, no elapsed cap. Performance acceptance after benchmarking target device; no unjustified numeric budget. PlayMode: input, placement/navigation reachability, interrupted interaction, animation adapter, scene recreation, save-resume, UI state. Device/build tests: Android permission denial, background kill, reboot/power modes; Windows unpackaged toast registration, scheduling/cancel, click activation, shutdown/restart; no guaranteed delivery. Verify same save-state semantics regardless of notification availability.

## 14. Implementation backlog (ordered)

T00: Apply TD-001–TD-006. Verify the minimal assembly graph, EditMode discovery, LFS and both Development builds. VContainer is deferred until Bootstrap.
T01: Domain state, stable definitions, fake clock/RNG, bounded needs, simulation engine; pure tests.
T02: Care commands and basic logical actions, life stages, illness/death/new egg, history; tests including offline equivalence.
T03: Save envelope, migration/backup, app pause/resume, game flow; tests for restore and full offline catch-up.
T04: Windows Toast feasibility in an unpackaged Steam-compatible build. Notification planner, editor mock and Android feasibility after separate notification approval. Add delivery adapters after verification.
T05: Graybox room and pet, camera/input, UI, day/night, ground navigation, action presentation.
T06: Grid, inventory, furniture interactions, modifiers, validation, accessibility.
T07: Evolution rules, collection, persistent generation progress once rules approved, two anatomy adapters and test content.
T08: Shop/economy, mini-game, full care polish, notification settings, art pipeline; no real payments yet.
T09: MVP balance, profiling, platform QA, release decisions.

First technical milestone T00–T03: create pet, advance time, apply care, save and resume after a simulated multi-day absence, record terminal death and allow a new generation. Deterministic tests pass. Next vertical slice T04–T06: one room with three functional objects, pet can eat/sleep/play, layout works, UI updates and state saves and restores. Record notification feasibility separately under T04.

## 15. Open decisions and risks

See [open-items.md](../open-items.md) for open design and technical decisions and [setup.md](setup.md) for the current project status. The preliminary MVP content target is two base species with two adult forms per species, four adult forms total. The quantities remain open.

| Risk | Proposed mitigation and verification |
|---|---|
| Simulation differs across partitions or events share a timestamp | Define stable ordering, numeric rounding, clock rollback handling and persisted RNG semantics before T01. Compare state, ordered events and RNG after one advance versus several advances over 8 h, 24 h, 7 d and 365 d. Aggregate only spans or cycles with tested equivalence. |
| Unity navigation or animation becomes authoritative | Derive accessibility and action durations as domain inputs, define cancellation semantics, and commit effects once. Give each anatomy profile an accessible interaction or fallback for essential care. |
| Resume applies offline time twice or an Android process kill interrupts a save | Serialize commands and resume processing. Save state, UTC cursor and RNG together. Test interrupted writes, backup recovery and duplicate callbacks. Verify the selected serializer on Windows Mono and Android IL2CPP. |
| Changes to content or rules alter existing saves | Version save schema and simulation rules independently. Preserve stable IDs and define migration and missing-ID behavior before T03. |
| Forecast consumes live RNG or platform delivery influences state | Forecast on an independent snapshot with its own RNG. Keep notification delivery outside simulation. Verify registration, scheduling and activation in the Windows T04 spike. |
