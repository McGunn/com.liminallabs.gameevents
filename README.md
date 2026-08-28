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
  project with live listener and raise counts, a per-event raise history and
  test-raise button, an all-events activity feed, and **Scan References** —
  which scenes, prefabs, and assets point at each event, flagging orphans.
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
- Everything resets correctly for Enter Play Mode without domain reload.
- The demo scene's materials use URP; the runtime is render-pipeline-agnostic.
