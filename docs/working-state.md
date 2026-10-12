# Working state

Where the Tunnel Flight conversation with Claude stands: what's been asked, what's done, what's waiting on whom. Update this at the end of each working session. Design decisions live in `docs/design.md`; this file tracks progress and open threads.

Last updated: 2026-10-12.

## Where we are

Build step 1 (tunnel + fly) is playable on the spline test level (`Assets/Scenes/Flight.unity`, rebuilt by **Tunnel Flight → Create Flight Scene**). We're in a feel-testing loop on step 1: Tom plays, gives feedback, Claude adjusts.

The latest round added six readability aids for judging position relative to the walls. They're on trial and Tom hasn't played them yet.

## History, in order

1. **Tunnel package.**
   - Audited Vectagon's tunnel design doc for this project's needs and worked them through with the Vectagon session.
   - Vectagon built `com.tomkail.tunnel` (spline path source, superellipse profiles, wall queries). We consume it as a local UPM package.
   - Vectagon also fixed a bug where the tunnel mesh didn't rebuild when the focus moved backwards (commits c4faab7, 89063c8).
2. **Project setup.** Unity 6000.5.1f1, URP, macOS, local git.
3. **Gameplay ahead of the tunnel.** Built the game-side code before the package existed:
   - flight model, heat/boost, wall sensing and response, health
   - camera, feedback, debug HUD
   - the `ITunnel` seam everything talks to the tunnel through
4. **Ship.**
   - Jammed on the shape over several rounds. Tom picked the "Broader" manta: swept-back wingtips and a short back. It's a procedural mesh (`MantaShipMesh`).
   - Wingtip trails.
   - An F-Zero-style boost jet with sparks. The sparks bounce off the walls.
5. **Integration.**
   - Adapter `PackageTunnelProvider`, in its own assembly.
   - Spline test level, 1.4 km, with roll and shape keys.
6. **Feedback round 1** (inverted Y, tunnel missing after respawn, ship leaving the frame). Fixed:
   - pitch inverted by default
   - the tunnel rebuilds around the ship on respawn
   - the camera pulls back along the spline to keep the ship in frame
7. **Feedback round 2** (camera swingy, hard to judge wall distance, tunnel hard to read).
   - The camera now frames to a circle, pulls out fast and eases back in slowly.
   - Sci-fi wall-distance readout.
   - Tunnel readability: a denser grid, a floor stripe, distance fade, and the walls brighten near the ship.
8. **Feedback round 3** (keyboard pitch twitchy, still hard to judge wall position, first person wanted).
   - Keyboard steering ramps in over 0.3 s, at 70% of stick pitch.
   - Toggleable first person on C or gamepad Select.
   - Six readability aids, all switchable from the debug HUD (F1):
     - a cross-section ring at the ship
     - a wall reaction (the grid glows and ripples where you're closest). This replaced a projected ship shadow, at Tom's request.
     - a camera lift slider
     - a cross-section minimap
     - wall speed streaks
     - proximity audio (a generated wind loop) and rumble

## Waiting on Tom

- Feel test of round 3:
  - keyboard ramp
  - first person
  - each readability aid, on and off
  - camera lift at a few values
- Which aids to keep, and the camera lift value. Any lift above 0 changes the [Decided] centreline camera.
- A real wind clip, scrape loop and heavy-hit clip. The scrape and impact AudioSources have no clips yet.

## Open threads

- From the chase camera the ship reads edge-on. The camera lift trial addresses this.
- First person was last checked with the eye at (0, 0.3, 0.7); in that capture no nose was visible. Tom's call whether it needs a sliver of nose as a reference.
- Wall streaks are subtle by default. Tune `alphaBySpeed` and `brightness` after a feel test.
- The minimap sits at the bottom right, and Panini bends it slightly at boost. The bottom centre clashes with the floor stripe.
- Our provider still rebuilds the tunnel mesh itself on respawn. Vectagon has since fixed this in the package, so the workaround can be dropped.
- The tunnel package lives on Vectagon's `unity6-upgrade` branch, unpushed. Our manifest path resolves to whatever branch Vectagon has checked out.
- Not started: build steps 2+ in `docs/design.md`, and the HUD's form beyond the trial aids.

## How Claude verifies changes

Tom's Editor usually has the project open, which blocks batch mode. So Claude verifies on a scratch copy instead:

1. Rsync the project to the copy, excluding Library, Temp, Logs, UserSettings and .git.
2. Point the manifest at the absolute package path.
3. Run `FlightSceneSetup.BatchCreate`.
4. Run `unity test` in PlayMode and EditMode, with `TF_CAPTURE_DIR` set to save game-camera frames.

Last run: PlayMode 6/6, EditMode 20/20.
