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

Code-side listening is `Subscribe`/`Unsubscribe` with plain delegates.

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

`Demo/GameEvents_Demo.unity` — one broadcaster, sixteen reactors, zero scene
references between them. Press **[1]** to pulse a wave through the cube row
(it also fires on a timer), **[2]** to re-tint the sphere family from one
float payload, **[3]** to toggle the lamps — two subscribe in code, the third
is wired purely with a Bool Event Listener component. A world-space sign
shows live raise/listener counts straight off the event assets, and the
Events Board mirrors it all with full history. Select any object: its
inspector references event assets only.

## Notes

- Adding a project-specific payload type is two one-liner classes — copy any
  pair in `Runtime/PayloadEvents/`.
- Everything resets correctly for Enter Play Mode without domain reload.
- The demo scene's materials use URP; the runtime is render-pipeline-agnostic.
