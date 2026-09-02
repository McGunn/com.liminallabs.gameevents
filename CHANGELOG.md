# Changelog

All notable changes to this package are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.6.0] - 2026-09-01

### Added

- **Listener priority.** `Subscribe(listener, priority)` on every event type: higher runs
  first, equal priorities run in subscription order, and plain `Subscribe` is priority 0. The
  listener and receiver components carry a `Priority` field. For the one listener that must see
  an event before the rest - a guard that changes state the others read, an analytics tap - not
  for ordering everything. A subscription made mid-raise still waits for the next raise however
  high its priority, so the in-flight raise is never shifted or joined; the Board shows a
  non-zero priority beside the listener.
- **`GameEventRegistry`: one place to resolve a stable id.** A `GameEventCatalog` answered for
  the project's assets and could not know about scene events, which do not exist until their
  scene loads; a bridge had to collect hosts itself. Now a catalog `Activate()`s into the
  registry - a `GameEventCatalogActivator` in the bootstrap scene does it with no code - and a
  `SceneGameEvent` registers its event while its host is enabled. `GameEventRegistry.TryResolve`
  answers for both, exactly while the event is alive. Two different events with one id is
  refused with an error naming both; the first keeps answering.

### Tests

- 38 (was 26): priority order across plain and typed events, a mid-raise high-priority
  subscription waiting for the next raise, add-and-remove within one raise, a guard
  unsubscribing a later listener, priority in listener descriptions; registry resolution
  through a catalog, an activator and a scene host, duplicate-id refusal, no-id refusal,
  `Changed`.

## [0.5.0] - 2026-09-01

### Changed

- **Listener and receiver components allocate nothing to enable or disable.**
  `GameEventListener` rows, the typed listeners and both `GameEventReceiver` bases subscribed
  with a method group, which is a new delegate every time it is written - two allocations per
  enable/disable cycle, which on a pooled object is every spawn. Each now builds its delegate
  once and keeps it, and unsubscribes from the event it actually joined rather than whatever
  the field says at disable time.
- **`GameEventRaiser` set to `Enabled` fires its first raise from `Start`.** A scene enables
  its objects in an order nobody controls, so a raise from `OnEnable` could go out before a
  listener two objects down had subscribed - and an event with no listeners is not an error,
  so nothing said so. `Start` runs after every object's `OnEnable`, so the first raise reaches
  the whole level. Being re-enabled later fires at once, as before.
- **A scene event host knows in a build whether it owns its event.** `IsSceneStored` was
  answered off the asset database, which a player does not have, so a player treated every
  hosted event as a project asset and the runtime release in `OnDestroy` never ran. The host
  now records ownership when it adopts an event, `OnValidate` keeps the record true in the
  editor, and a player reads it. Save a scene once after upgrading so the record is stored.
- **An adopted scene event gets its stable id on adoption**, not on its first inspection. An
  event made in code is inspected when someone happens to click it; a bridge or a save that
  named it before then had nothing to name it by. `OnValidate` and `Adopt` share one
  `EnsureStableId`.

### Tests

- 26 (was 25): an adopted event has a stable id.

## [0.4.0] - 2026-08-31

### Added

- **`GameEventRaiser`** — the other half of a wire. The package shipped listeners in nine
  flavours and nothing at all that raises, so every wire a designer made had to end at a
  listener and begin in somebody's code. Fine for `PlayerDied`, which a health script raises;
  useless for a switch opening a door, which is the archetypal thing a level designer wants and
  had no component for.

  It also left `IGameEventRaiserInfo` with no implementors, so the scene-view wiring tool could
  only ever draw dashed lines — it had nothing that had declared itself a raiser, only things
  that happened to hold a reference.

  Raises when called (the default, so nothing fires by surprise), on enable, or on a trigger
  entering or leaving, with an optional one-shot. `Raise()` is public and parameterless so a
  UnityEvent on a button, an interaction or an animation event can drive it without knowing
  what a game event is.

  Payload-less on purpose: level wiring is a signal, not a value. A raiser per payload type
  would be eight more components serving a case that wants none of them.

## [0.3.0] - 2026-08-31

### Added

- **Scene events.** `SceneGameEvent` hosts an event that lives in a level rather than in the
  project. It is deliberately **not a new kind of event**: the thing it holds is an ordinary
  `GameEventBase`, with the same raise contract and the same listener list, stored in the scene
  file instead of `Assets/`. Every field that points at an event is unchanged, so a listener
  still declares `[SerializeField] GameEvent` and a designer drags either kind into it — no
  interface, no `UnityEngine.Object` field, no second implementation of the raise semantics to
  drift from the first.

  A ScriptableObject has no transform and never appears in the Hierarchy, so the component
  exists to give the event a *place*: something to select, name, and draw a wire to.

- **Game Event Wiring**, a scene-view tool. Selection-scoped, because drawing every wire at once
  is what makes tools like this unusable. Raisers warm, listeners cool, the channel a point in
  space between them. Drag between two objects to connect them — reusing a channel when either
  end already has one, which is what keeps many-to-many from becoming a channel per pair. Wires
  light up in play mode as the event fires.

- **`IGameEventRaiserInfo`**, the mirror of `IGameEventListenerInfo`. Listeners are discoverable
  because they hold a serialized reference; raisers are not, since any code may call `Raise()`.
  A component that declares itself is drawn solid; one that merely holds a reference is drawn
  dashed. The difference between "raises this" and "mentions this" is exactly the difference a
  wiring diagram must not blur.

- **A self-check that proves the design.** `Window > Liminal Labs > Game Events > Verify Scene
  Events Persist` writes a scene containing a hosted event and a listener pointing at it,
  reopens it from disk, and reports what survived. The whole feature rests on Unity writing a
  path-less ScriptableObject into a scene file — a documented technique, still an assumption,
  and one that would fail silently. Run it after a Unity upgrade.

- 25 tests covering hosting, role discovery, and assignment — including that a field refuses an
  event of the wrong type, which is the nastiest failure available here: Unity discards a
  mismatched object reference silently, so a wiring tool would report a connection it never
  made.

### Changed

- The **Events Board** now lists events hosted in the open scenes alongside project assets. It
  previously scanned only the asset database, which meant it showed a level designer none of
  their own wiring.
- Every game-event field gains a second, smaller picker for events hosted in the open scenes.
  The main dropdown is backed by the shared project-asset search, which by construction cannot
  find something that has no asset path.

### Notes

`SceneGameEvent.Adopt` refuses at runtime rather than accepting quietly. An event created while
playing is never written to the scene, so it would work for a session and be null on the next
load, taking every reference with it — a silent failure worth being loud about.
