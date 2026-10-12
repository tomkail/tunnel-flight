# Tunnel Flight

Unity game prototype (working title). A roll-and-pitch flyer in a twisting tunnel, with a heat meter that forces boosting. Primary mode is time attack on fixed levels.

## Source of truth

- **Design:** `docs/design.md`. Read it before proposing or implementing any gameplay system. Items are tagged [Decided], [Proposed] or [Open]. Don't reopen [Decided] items without flagging it. When a design decision changes in conversation, update `docs/design.md` in the same change.
- **Working state:** `docs/working-state.md` tracks where the conversation stands: history, what's waiting on Tom, open threads. Read it at the start of a session and update it at the end.
- **Tunnel package:** `com.tomkail.tunnel` is built in Vectagon (`/Users/tom/Documents/Unity/vectagon/Packages/com.tomkail.tunnel`; plan at `docs/.solution-designs/2026-10-09-tunnel-system.md` there) and consumed here as a local UPM package via `file:../../vectagon/Packages/com.tomkail.tunnel`. Don't port or fork tunnel code into this project. Raise needed changes with the Vectagon session. It's referenced from `Packages/manifest.json`. That path resolves to whatever branch is checked out in Vectagon (the package currently lives on `unity6-upgrade`, unpushed).

## Current status

Pre-production, build step 1 (tunnel + fly) is playable. The project is Unity 6000.5.1f1 (6.5), URP (`urp-blank` template), targeting macOS, with `com.unity.splines` and `com.tomkail.tunnel` installed. Source control is local git with no remote.

Gameplay code is in `Assets/Scripts` (namespace `TunnelFlight`) and talks to the tunnel only through `ITunnel` / `TunnelProvider` (`Assets/Scripts/Tunnel/ITunnel.cs`). The only code that touches the package is `Assets/Scripts/TunnelAdapter` (its own assembly): `PackageTunnelProvider` adapts the package, and `SplineLevel` restarts the tunnel from a SplineContainer with roll and superellipse keys. `Assets/Scenes/Flight.unity` plays the spline test level. The menu item **Tunnel Flight/Create Flight Scene** rebuilds that scene, the test level and the tuning assets in `Assets/Settings/Gameplay`. Hand edits to the scene are lost when you re-run it.

Tests: EditMode in `Assets/Tests/EditMode`, PlayMode smoke tests of the flight scene in `Assets/Tests/PlayMode`. Set `TF_CAPTURE_DIR` to save game-camera frames from the PlayMode run.

Gotcha: URP Lit materials created from editor scripts lose the `_EMISSION` keyword on load here. Use the `TunnelFlight/Additive Glow` shader for glow instead of Lit emission.

## How to work

- Follow the build order in `docs/design.md`. Each step ends with a feel test, so get a playable version of each step quickly instead of building systems ahead of time.
- Keep tuning values (camera, heat, boost, collision bands, slow-mo factor) as serialized fields or ScriptableObjects, editable at runtime in Play mode.
- One wall-distance query (polar radius against the superellipse profile at spline position s) feeds collision, damage, proximity light, audio and HUD. Don't duplicate it.
- Visual direction (Vectagon plus Sayonara Wild Hearts / Rez / Wipeout / CSmash) is deferred. Use placeholder visuals until it's defined.

## Working with Tom

Senior developer with a Unity and game design background. Be direct and concise; skip basic explanations. Flag uncertainty explicitly. Present genuine trade-offs as options, and say plainly when something is wrong.
