# Liminal Labs Game Events

Things react to things without direct references. An event is a ScriptableObject
asset — raisers and listeners point at the same asset and never at each other,
so a door, a quest system, and a music layer can respond to the same moment
without knowing the others exist.

Requires `com.liminallabs.core`.

## Five-minute quickstart

1. Create an event: **Assets > Create > Liminal Labs > Game Events > Game
   Event** (or a typed one — Float, Bool, Vector3, …).
2. Listen without code: add a **Game Event Listener** component, pick the
   event, wire the UnityEvent response. Typed listeners pass the payload
   through (`FloatGameEventListener` → `UnityEvent<float>`).
3. Raise it — from code:

   ```csharp
   using LiminalLabs.GameEvents;

   [SerializeField] private GameEvent doorOpened;
   [SerializeField] private FloatGameEvent healthChanged;

   doorOpened.Raise();
   healthChanged.Raise(0.75f);
   ```

   …or from anything that invokes UnityEvents (buttons, animation events,
   timeline signals) by calling the asset's `Raise` method — or from the
   event's own inspector with the Raise button and test payload.

Code-side listening has two levels. For a component whose whole job is
reacting to one event, inherit a receiver base and the subscription lifecycle
is already done:

```csharp
public class HealthBar : GameEventReceiver<float, FloatGameEvent>
{
    protected override void OnEventRaised(float value) { /* react */ }
}
```

(`GameEventReceiver` is the payload-less version; leaving the event field
empty is valid when something else — like a listener component — drives the
component directly.) For everything else, `Subscribe`/`Unsubscribe` with
plain delegates works anywhere.

## Picking events

Every game-event field draws as a **searchable dropdown** of the project's
events of that type — type to filter, hover for each event's description,
ping button beside it — with a **Create New…** entry that creates the asset
in place (defaulting next to its siblings) and assigns it immediately. No
duplicating and renaming, no object-picker spelunking. Fields typed to a
concrete event only offer that payload; fields typed to `GameEventBase`
offer everything with payload labels.

## Scene events

An event does not have to be a project asset. A **Scene Game Event** component hosts one that
lives in the level instead — same `GameEvent` type, same raise contract, same listener list,
stored in the scene file rather than in `Assets/`.

Nothing else changes. A listener still declares `[SerializeField] GameEvent` and a designer
drags either kind into it. There is no interface, no `UnityEngine.Object` field, and nothing in
the package that has to ask which sort it is holding.

**Why it is worth having.** A project asset per door per level is clutter that outlives the
level, and it does not survive the thing level designers do most — duplicating a scene. Copy a
level and its scene events copy with it, independently wired, because they live in the file
that was copied. They also cannot accumulate the stale-subscriber bug an asset can: an asset
persists between play sessions, a scene event is rebuilt every load. A scene event gets its
stable id the moment it is created, and its host knows - in a build too - that it owns it, so a
host destroyed mid-session releases its event.

Use a **global asset** for things the whole game shares — `RedAlert`, `PlayerDied`,
`AnySwitchActivated`. Use a **scene event** for wiring inside one level — this switch to that
door. One raiser can do both, from fields of the same type, which is the point.

### Wiring them in the scene view

Pick the **Game Event Wiring** tool from the scene-view toolbar.

- Select something and it draws only what *that* object is part of, one hop out. Nothing is
  drawn otherwise — a level with fifty wires visible at once is a level you cannot see.
- Raisers are warm, listeners are cool, and the channel is a point in space between them, so
  many-to-many reads as a shape rather than an inference.
- **Drag from one object to another to connect them.** If either end already has a channel, the
  drag joins it; only when neither does is a new one created. That rule is what keeps
  many-to-many from degenerating into a channel per pair.
- A raiser that implements `IGameEventRaiserInfo` is drawn solid. One that merely holds a
  serialized reference is drawn dashed, because "raises this" and "mentions this" are not the
  same claim.
- In play mode a wire lights up as the event actually fires. That is also the only way to see a
  raiser that lives in code and declares nothing — a wire nobody drew, lighting up.

### Verify it on your Unity version

Scene events rest on one Unity behaviour: a ScriptableObject with no asset path, referenced by
a scene component, is written into the scene file. That is a documented technique and it is
still an assumption — and one that would fail *quietly*, giving you a session of working wires
and nulls the next morning.

So it is checked by doing it. Run **`Window > Liminal Labs > Game Events > Verify Scene Events
Persist`**: it writes a scene with a hosted event and a listener pointing at it, reopens it from
disk, and reports whether both survived. Worth running again after any Unity upgrade.

## The raise contract

These semantics are fixed and pinned by the test suite:

- Listeners fire in **subscription order** — deterministic, always.
- A **throwing listener is isolated**: the exception is logged with the event
  and listener names, and the remaining listeners still fire.
- **Unsubscribing during a raise** takes effect immediately; **subscribing
  during a raise** takes effect from the next raise.
- **Recursive raises** (a listener chain that re-raises the same event) are
  cut off at depth 8 with an error naming the event — never a stack overflow.
- Duplicate subscriptions are rejected with a warning. The raise path
  allocates nothing.

Events carry no game state — an event says *something happened*. There is
deliberately no `CurrentValue`.

## Tooling

- **Events Board** (`Window > Liminal Labs > Game Events`): every event in the
  project **and every event hosted in the open scenes**, with live listener and
  raise counts, a per-event raise history and test-raise button, an all-events
  activity feed, and **Scan References** — which scenes, prefabs, and assets
  point at each event, flagging orphans.
- **Game Event Wiring** tool: see and make scene wiring in the scene view. See
  above.
- **Event inspector**: description, test payload, Raise button, live
  listeners, recent raises.
- **Setup and Validation** rows: listener components with unassigned event
  slots, duplicate event names.

## Demo

Import the **GameEvents Demo** sample from the Package Manager —
one broadcaster, sixteen reactors, zero scene
references between them. Press **[1]** to pulse a wave through the cube row
(it also fires on a timer), **[2]** to re-tint the sphere family from one
float payload, **[3]** to toggle the lamps — two subscribe in code, the third
is wired purely with a Bool Event Listener component. A world-space sign
shows live raise/listener counts straight off the event assets, and the
Events Board mirrors it all with full history. Select any object: its
inspector references event assets only.

## Multiplayer

Events are a **local-process** message bus by design — raising an event never
touches the network. Bridging is deliberately easy, though: every event
carries a **stable id** (a GUID minted at creation, shown in its inspector),
and a **Game Event Catalog** asset resolves ids back to events at runtime.
A network bridge is ~20 lines with any netcode:

```csharp
// send:    lever.Interacted -> SendToAll(gameEvent.StableId)
// receive: if (catalog.TryGet(id, out var e)) { remoteRaise = true; ((GameEvent)e).Raise(); remoteRaise = false; }
```

The `remoteRaise` flag is the one trap: mark remote-originated raises so your
bridge doesn't re-forward them in a loop. Stable ids also serve save systems
and analytics; the Setup window flags missing or duplicated ids.

## Notes

- Adding a project-specific payload type is two one-liner classes — copy any
  pair in `Runtime/PayloadEvents/`.
- Listener and receiver components build their delegate once; enabling and
  disabling them allocates nothing, so they are safe on pooled objects.
- A `GameEventRaiser` set to *Enabled* fires its first raise from `Start`, after
  every object in the scene has enabled, so the listeners in the same level all
  hear it. A re-enable later fires at once.
- Everything resets correctly for Enter Play Mode without domain reload.
- The demo scene's materials use URP; the runtime is render-pipeline-agnostic.
