# Tunnel Flight — Game Design

Working title. Living document; this file is the source of truth for design decisions.
Status tags: **[Decided]**, **[Proposed]** (default plan, open to change), **[Open]**.

Published copy (may lag behind this file): https://claude.ai/artifact/XbcKH18YD2kTGWXxZBkbVb

## Pitch

A rollercoaster through a twisting tunnel. You can't turn, only roll and pitch. A heat meter forces you to boost more than you want to, and the walls punish you for it.

- Engine: Unity
- Tunnel system: shared local UPM package `com.tomkail.tunnel`, built in the Vectagon project (`vectagon/Packages/com.tomkail.tunnel`) and referenced from here by `file:` path. Tunnel Flight consumes it and does not port code. Tunnel Flight must also add `com.unity.splines` to its manifest, or the package's spline assemblies won't compile.
- Primary mode: time attack on fixed levels

## Pillars

1. **Vertigo is the point.** It's a rollercoaster. The camera rolls with you and the world spins. Disorientation is a feature to tune, not a bug to remove.
2. **Boost more than you want.** Heat forces you to boost. Straights are where you drain it, and twisty sections are where you pay for the momentum.
3. **Nothing steers for you.** The ship has a real heading. Your angle into the wall is what decides whether you scrape it or die.

## Flight model

- **[Decided] Keyboard steering ramps.** A held key reaches full deflection over 0.3 s, and releasing it stops at once. Full pitch from keys is 70% of full stick. Sticks are unaffected.
- **[Decided] Inverted pitch by default.** Stick up is nose down, flight-sim style. It's a toggle in the flight settings.
- **[Decided] Aircraft model.** Roll rotates the ship around its forward axis. Pitch rotates the heading around the ship's local right axis. To reach the left wall, you roll 90° and pitch.
- **[Decided] Real heading.** The ship's forward vector can deviate from the tunnel tangent. No auto-alignment or auto-steer.
- **[Decided] Wide, short ship.** Rolling deliberately lines your wings up with rows of pickups. Collision uses the wingtips, so rolling flat near a wall is a hazard in its own right.
- **[Decided] Wingtip trails.** Cyan ribbons off each tip mark the ship's widest points and brighten with speed.
- **[Decided] Manta hull.** A broad crescent planform with a short, scalloped rear and wingtips swept back into points. The span is 3 m, and it's about 1.9 m from nose to tips. The mesh is procedural (`MantaShipMesh`): low-poly, flat-shaded, with glowing tip accents. The collision probes come from the mesh's tips and nose.
- **[Proposed] Steering authority falls with speed.** Pitch and roll rates are fixed per second, so you get less turn per metre at boost speed. Boosting into a bend becomes a commitment.

## Camera

- **[Decided] On the spline, behind the player.** The camera sits on the tunnel's centreline spline, a fixed arc length behind the player's spline position. It looks along the spline and rolls with the player. The ship moves around the viewport as it drifts off-centre, so the view itself shows where you are in the cross-section.
- **[Decided] The ship always stays in frame, by pulling back.** The camera keeps looking along the spline and pulls back along it as far as needed to keep the wingtips and nose inside a circular safe area. That's 80% of the vertical half field of view, and being circular, roll doesn't change the framing. It pulls out fast (0.12 s), eases back in slowly (0.8 s), and stops at 16 m or the bend-radius line-of-sight limit. Only past that does it turn toward the ship. Before the track starts, the camera extends straight back from the first frame. The trade-off is that hugging a wall pulls the camera back and makes the ship smaller on screen.
- **[Proposed] Camera lift, for feel testing.** A slider (0–3 m, in camera settings and on the debug HUD) raises the chase camera along the ship's up, so the wings are seen from slightly above instead of edge-on. Lift aim (0–1) tilts it back down toward the ship. Lift shrinks near a wall to keep 0.5 m of room. The default is 0, which keeps the centreline camera above; any lift we keep changes that [Decided] item.
- **[Decided] Toggleable first person.** C or gamepad Select switches to an eye over the front of the canopy, looking along the ship's real heading. Speed FOV, Panini and shake still apply. The hull stays visible by default as a reference.
- **[Decided] Panini projection.** One of the first things to tune. It keeps the centre rectilinear and compresses the periphery, where the walls are.
- **[Decided] FOV and distortion scale with speed.** Boosting widens the view and increases Panini strength, so the distortion reads as a speed effect.
- **[Proposed] Check URP's built-in Panini Projection volume override first** (distance + crop-to-fit). It may be enough when driven from speed. If it can't handle high FOV well, write a custom pass.

**Line of sight on tight bends.** Following on the spline handles the general case. A curved tube isn't convex, though: where the bend radius is close to the follow distance, the straight line from the camera to a ship hugging the inner wall can still cut through that wall. Either cap the follow distance against the minimum bend radius as a level-design rule, or fade or clip wall geometry between the camera and the ship.

### Tuning parameters (starting ranges)

| Parameter | Range | Notes |
|---|---|---|
| Follow distance (arc length) | 2–6 m | Shorter is more visceral and keeps line of sight. Longer is more readable. |
| Roll follow damping | 0–0.25 s | 0 snaps for maximum vertigo. Some lag adds sway. |
| FOV at normal / boost max | 90° → 120° | Driven by speed, not by the boost button. Treated as horizontal FOV by default (a toggle in camera settings); [Open] whether that's the intent. |
| Panini strength normal / boost | 0.2 → 0.7 | Check ship readability at the screen edges at high strength. |
| Speed → effect curve | ease-in | Effects should come in mostly above normal max speed. |
| Camera position blend (spline ↔ ship) | 0–0.3 | Optional. Moves the camera toward the ship for more of a rollercoaster feel. |

## Heat & boost

- **[Decided] Heat rises continuously.** It runs on real time and never pauses.
- **[Decided] Boost drains heat.** Hold to boost: strong acceleration toward boost speed while held, and heat drains in real time.
- **[Decided] Heat is boost fuel.** You can't boost at 0%. Heat is both the threat and the resource.
- **[Decided] GX-style release.** On release, speed decays exponentially toward normal max (time constant about 1.5–3 s). Drag applies only above normal max, so it feels like coasting, not braking.
- **[Decided] Forced boost lock.** When heat hits 100%, boost engages and locks until heat reaches 0%. No explosion. The punishment is losing control at a bad moment. Because boost needs fuel, a forced lock also leaves you with nothing to boost with straight afterwards. Watch whether that's too harsh in time attack.
- **[Decided] Boost is shown on the ship.** A jet from a rear engine pod idles at cruise and roars on boost, in the spirit of F-Zero. It has a white-hot core, a blue plume, a nozzle flare that reads from the chase camera, and a light that throws glow on the walls behind. Sparks ramp up with the square of boost intensity, with a burst when a boost starts. As in F-Zero GX, sparks hit the tunnel wall and skitter along it behind the ship, using the wall query rather than colliders. Flying near a wall puts more sparks on it, which doubles as a proximity cue. Forced boost tints it orange-red.
- **[Proposed] Minimum heat to start a boost** (default 5%). A held boost runs down to 0%, but starting one needs a little heat, so boost doesn't flicker on and off at empty.
- **[Proposed] Heat fills faster at higher speed.** The post-boost coast then has a cost, so you can't chain boosts indefinitely.

The fill-to-drain ratio sets how much of each level you're forced to spend boosting. It's the main difficulty dial. Levels should be authored around it: put a drain straight before every twisty section, and make it a little too short.

Illustrative model (placeholder numbers):

```
held:     v += (vBoost - v) * kUp * dt          // vBoost = 1.7 × vMax, kUp ≈ 4
released: if v > vMax: v = vMax + (v - vMax) * exp(-dt / tau)   // tau ≈ 2 s
heat:     +6 %/s base, −25 %/s while boosting, clamp 0–100
```

## Walls & health

- **[Decided]** Hitting a wall isn't instant death. Skimming costs health, and a head-on crash kills you. Both come from one number: the velocity component along the wall normal, `vn = speed × sin(incidence)`. Boosting makes the same angle more dangerous without any special-casing.

| Band | Result | Status |
|---|---|---|
| vn < A | Scrape. Health drains proportional to vn per second of contact. Deflect along the wall. Sparks and grinding audio. | Decided |
| A ≤ vn < B | Heavy hit. Large chunk of health, speed loss, a bounce and camera shake. | Proposed |
| vn ≥ B | Death. | Decided |

### Proximity feedback

Every cue below comes from the same wall-distance query used for collision.

- **Ship light.** A point light with tight falloff, so nearby walls flare.
- **Audio.** A whoosh panned toward the nearest wall, louder, higher-pitched and less muffled (low-pass opens) as you close in. A generated wind-noise loop stands in until there's a real clip.
- **Rumble.** A low rumble builds within 1.5 m of a wall, before contact.
- **HUD: wall readout [Decided].** A glowing line from the ship's closest probe to the nearest wall, with a vector-drawn distance readout ("1.6m"). It shows within 6 m, coloured cyan to amber to red as the gap closes, and pulses when closing on the wall fast. The rest of the HUD is [Open].
- **[Proposed] Readability aids, on trial.** Each can be switched on or off from the debug HUD (F1) for A/B tests:
  - *Cross-section ring.* A white outline of the tunnel profile at the ship, just inside the walls (6 m ahead in first person). The arc facing the nearest wall widens and takes the danger colour.
  - *Wall reaction.* The grid reacts where the ship is closest: lines take the danger colour and the cells fill with a rippling glow. It starts at 5 m; the spot tightens, brightens and ripples faster as the gap closes. This replaces a projected ship shadow, at Tom's call.
  - *Cross-section map.* A screen overlay at the bottom right: the profile at the ship, a dimmer profile 40 m ahead (showing coming shape and twist), the ship as a wingtip bar with an up fin, the nearest-wall line and a floor tick. It's aligned to the view by default.
  - *Wall streaks.* Speed streaks fixed in the world just inside the walls, with length set by speed and brightening near the ship, for parallax close to the walls.
- **Contact.** Sparks, scrape audio, controller rumble.

## Tunnel

- **[Proposed] Readability (placeholder visuals).** A grid of 32 lines along the tunnel (every fourth brighter) and rings every 4 m (brighter every 8 m). One orange floor stripe follows the frame's roll, so twist and orientation read. Grid lines fade with distance and brighten around the ship.
- **[Decided] Mostly non-circular.** A circle is dull. It has no visible twist and gives no orientation reference. Ovals and rounded shapes are the default.
- **[Decided] Shape changes along the level, and the tunnel twists.** Twist only has a gameplay effect when the profile isn't round.
- **[Proposed] Superellipse profiles.** `|x/a|^n + |y/b|^n = 1`. Three parameters cover circle, oval, rounded square and diamond. Shape changes interpolate (a, b, n) along the spline. Twist is the frame's roll channel, so θ is measured in the rolled frame.
  - Polar radius: `r(θ) = (|cos θ / a|^n + |sin θ / b|^n)^(-1/n)`
- **[Proposed] Polar collision query.** Compare each wingtip's polar radius (in the local spline frame) to `r(θ, s)`. Clearance is `r(θ, s) − |tip|`. One query drives collision, damage, light, audio and HUD.
- **[Decided] Levels authored as splines.** Built on the package's `SplinePathSource` (`com.unity.splines`). The track coordinate s is arc length in metres. Frames are rotation-minimising, so they don't flip on vertical sections or loops. Twist is an authored roll channel along the spline, eased. Profile parameters (a, b, n) are authored along the spline too. Roll and profile keys can be keyed by knot, so they follow knot edits. Knots must use continuous tangent modes, because broken or linear tangents create kinks that the camera shows. Splines beat rotation commands here because editing a knot only changes the nearby track, and levels are laid out directly in space.
- **[Decided] Fallback: Local command mode.** The package's `CommandPathSource` also has a Local integration mode (local quaternion turns, forward equals tangent). Use it for procedural tunnels, such as a later endless mode. Vectagon's default Parity mode sums world Euler angles and isn't suitable here.
- **[Decided] Package queries used here:** generic profile channels (superellipse a, b, n), `Project(pos, hintS) → (s, localXY)`, a profile wall query (r(θ, s), clearance, gradient normal), tangent and curvature along with min bend radius over a range, a smooth-shaded surface, a material-instance property sink (URP), and followers with local (x, y) or wall-inset placement.

## Pickups

- **[Decided] Placed near walls.** The reward is a reason to fly close to them.
- **[Decided] Pickups in lines.** Rows matched to the ship's wingspan. Roll to the row's angle to sweep the whole line in one pass.
- **[Decided] Heat pickups.** They add heat, which is fuel: more boost to spend, but less margin before the forced lock. Dangerous on purpose; for skilled players chasing times.
- **[Decided] Slow-mo pickups.** Charge the slow-mo pool. Added after the core loop is proven.

## Slow-mo

- **[Decided]** This is an addition, built only once the core loop works without it.
- **[Decided] Raise turn per metre, with a partial rotation factor.** Real slowed time changes nothing about control relative to the track. Forward speed scales by the world timescale. Rotation rates scale by a partial factor between that timescale and 1 (a tuning value). The player gets more control than physics would allow, but not full real-time rotation.
- **[Proposed] Analogue pool.** Trigger depth sets strength and drain rate. Players can ease in and feather it through a section instead of toggling.
- **[Decided] Heat stays on real time.** Under slow-mo, a forced or held boost drains heat while covering little track. That gives you a recovery window when you're locked into boosting in a tight spot.
- **[Proposed] Time attack timer runs on real time.** Slowing down then costs time, which balances the mechanic without extra rules.

## Modes

- **[Decided] Time attack on fixed, authored levels.** Primary mode. You want to go fast as well as being forced to, so boosting is partly voluntary.
- **[Decided] Ghost replays.** Record spline position, offset, roll, heading (relative to the track frame, never world space) and boost state per tick, then play them back as a translucent ship. The data is small, and boost state lets a ghost show where a faster player boosted.
- **[Proposed] Endless distance mode, later.** Built on a procedural tunnel generator that reuses the same profile and spline system.

## Build order

Each step ends with a feel test. If the earlier steps are fun, later systems may not be needed.

1. **Tunnel + fly.** Integrate the `com.tomkail.tunnel` package. Author a test level as a spline with superellipse profiles and a roll (twist) channel. *Playable: a 1.4 km spline test level.* Roll and pitch at constant speed, no hazards.
   *Test: is steering through the shape interesting at a fixed speed?*
2. **Camera.** Spline follow, roll damping, Panini, speed-driven FOV.
   *Test: vertigo versus readability; can you tell where the ship is?*
3. **Heat + boost.** Acceleration, exponential decay, fuel, forced lock.
   *Test: does the boost feel like GX? Do straights become drain zones?*
4. **Walls.** Polar query, vn bands, health, proximity light and audio.
   *Test: are scrapes readable and fair?*
5. **One time attack level.** Line pickups, heat pickups, ghost recording.
   *Test: is there a skill ceiling, and do faster runs look different?*
6. **Slow-mo.** Only if steps 1–5 are missing something.

## Open questions

- **Name.** "Tunnel Flight" is a placeholder.
- **Health regeneration.** None, slow regen, or pickups only?
- **Death in time attack.** Restart the level, or respawn at a checkpoint with a time penalty?
- **Circles as set pieces?** Possibly rare round sections used deliberately to strip away orientation.
- **HUD form.** Cross-section indicator, edge markers, or nothing beyond the scene itself.
- **Visual direction.** Vectagon base, with Sayonara Wild Hearts, Rez, Wipeout and CSmash influences. To be defined at implementation.
