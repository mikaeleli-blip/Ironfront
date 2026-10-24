# Ironfront — Design Doc (v0.1)

## Pillars
1. **War first.** No politics/industry layer at launch. Everything serves operations.
2. **Real order of battle.** Historical battalions, regiments, divisions, corps, armies, with real names.
3. **Commanders matter.** Generals are on the map, have a command radius and traits, and can be killed or captured.
4. **Plan, then execute.** Draw frontlines, attack arrows and phase lines, then watch the plan play out and adapt.
5. **Realism over abstraction.** Terrain, weather, supply, fuel, ammo, organization, morale, fog of war.

## Core decisions
| Topic | Decision |
|---|---|
| Engine | Unity, 2D map |
| Slice | France 1940 (~650 × 550 km: Netherlands, Belgium, Luxembourg, N/NE France) |
| Map | Free-form continuous positions (km world coords) |
| Time | Real-time with pause; fixed sim tick = 15 game-minutes, speeds 1–5 |
| Visuals | 2D terrain map + APP-6 / NATO counters |
| Unit grain | Battalion = smallest simulated entity |

## Architecture
- **Sim core**: plain C# (`Ironfront.Sim` assembly), no `UnityEngine` references. Deterministic, testable, headless.
- **View layer**: Unity MonoBehaviours read sim state and render it (map, counters, arrows, UI).
- **Data**: OOB, generals, equipment and scenario as JSON/ScriptableObjects in `Assets/Data`.

## Map (free-form continuous)
- Unit positions are floats in km.
- **Terrain raster** (~250 m cells): terrain type, elevation, forest, urban. Used for movement cost and combat modifiers.
- **Vector layers**: roads (graph), rails (graph), rivers and canals (polylines, used for crossing checks), bridges (nodes that can be destroyed or built).
- **Pathfinding**: A* on the terrain-cost raster, plus road and rail graph shortcuts. Results are cached per unit type.
- **Control / frontline**: an influence field computed from unit footprints; the frontline is the contour between sides. Gaps appear naturally when units are spread thin.

## Units
- Battalion stats: men, equipment (by type), organization, morale, experience, fuel, ammo, entrenchment, posture (attack / defend / move / reserve / retreat).
- **Frontage**: each battalion holds a width of line depending on terrain and posture. Overstretching lowers defense.
- Hierarchy: battalion → regiment → division → corps → army → army group. Orders can be given at any level and cascade down.
- **Counters**: APP-6 frame (friend/hostile), type symbol (infantry, armour, motorized, artillery, recon, engineer, AT, AA, HQ), size marker, plus strength and organization bars.

## Commanders
- Assigned to HQs (division and above). An HQ counter carries the general's portrait.
- **Command radius**: units outside it lose org recovery, reaction speed and coordination.
- **Traits**, e.g. Panzer Leader, Defensive Expert, Cautious, Reckless, Logistician, plus skill ratings (attack, defense, logistics, initiative).
- **Order delay**: how long orders take to propagate depends on the comms tech of each side. French command was slow in 1940; German command was fast.

## Combat
- Engagement starts when hostile units are in contact range. It is resolved each tick.
- Inputs: soft attack, hard attack, defense, breakthrough, armour vs piercing, terrain, river crossing, entrenchment, weather, artillery in range, air support, command, organization and supply.
- Outputs: casualties (men and equipment), organization loss, morale shifts, retreat or rout, encirclement checks.
- **Combined arms**: bonuses when infantry, armour, artillery and engineers fight in the same engagement.

## Supply
- Sources are depots and railheads. Supply moves along rail, then road (trucks), then cross-country (much more slowly).
- Each battalion consumes fuel, ammo and rations each tick. Shortages hit speed, attack and organization.
- Units cut off from supply are **encircled**: they degrade over time and may surrender.

## Planning tools
- **Frontline order**: assign a division or corps to a drawn line; units spread to hold it.
- **Attack arrow**: an axis of advance with objectives and phase lines.
- **Phase lines and timings**: e.g. "H-hour 05:00, reach line Bravo by D+1".
- **Reserves**: held back and committed automatically when a breakthrough is detected (optional).
- **Recon** is needed to reveal enemy counters. Unidentified contacts show as "?" markers.

## Fog of war
- Each side has its own knowledge state: last-seen position, estimated size and confidence.
- Recon units, air recon and contact reveal the enemy.

## Weather & time
- Day/night: night lowers attack and movement, but reduces air threat.
- Weather: clear, rain, mud and fog affect movement, air and combat.

## Later
Air war (CAS, interdiction, air superiority), naval/amphibious (Dunkirk!), more theatres (Norway, North Africa, Barbarossa, Normandy), multiplayer.
