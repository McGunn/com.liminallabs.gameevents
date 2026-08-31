# Changelog

All notable changes to this package are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
