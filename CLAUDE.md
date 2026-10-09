# Tunnel Flight

Unity game prototype (working title). A roll-and-pitch flyer in a twisting tunnel, with a heat meter that forces boosting. Primary mode is time attack on fixed levels.

## Source of truth

- **Design:** `docs/design.md`. Read it before proposing or implementing any gameplay system. Items are tagged [Decided], [Proposed] or [Open]. Don't reopen [Decided] items without flagging it. When a design decision changes in conversation, update `docs/design.md` in the same change.
- **Tunnel package:** `com.tomkail.tunnel` is built in Vectagon (`/Users/tom/Documents/Unity/vectagon/Packages/com.tomkail.tunnel`; plan at `docs/.solution-designs/2026-10-09-tunnel-system.md` there) and consumed here as a local UPM package via `file:../../vectagon/Packages/com.tomkail.tunnel`. Don't port or fork tunnel code into this project. Raise needed changes with the Vectagon session. Step 1 is blocked until the package is built.

## Current status

Pre-production. The Unity project is created: Unity 6000.5.1f1 (6.5), URP (`urp-blank` template), targeting macOS. `com.unity.splines` is installed. The tunnel package isn't referenced yet because it hasn't been built. Source control is local git with no remote.

## How to work

- Follow the build order in `docs/design.md`. Each step ends with a feel test, so get a playable version of each step quickly instead of building systems ahead of time.
- Keep tuning values (camera, heat, boost, collision bands, slow-mo factor) as serialized fields or ScriptableObjects, editable at runtime in Play mode.
- One wall-distance query (polar radius against the superellipse profile at spline position s) feeds collision, damage, proximity light, audio and HUD. Don't duplicate it.
- Visual direction (Vectagon plus Sayonara Wild Hearts / Rez / Wipeout / CSmash) is deferred. Use placeholder visuals until it's defined.

## Working with Tom

Senior developer with a Unity and game design background. Be direct and concise; skip basic explanations. Flag uncertainty explicitly. Present genuine trade-offs as options, and say plainly when something is wrong.
