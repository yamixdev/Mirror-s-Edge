# Parkour objects and moves

[English](parkour-objects-en.md) | [Русский](parkour-objects-ru.md)

## Place objects

Stop Play Mode and wait for script import. Drag a prefab from `Assets/Prefabs/Parkour` into Hierarchy or Scene. Your scene needs a configured Spawn.

| Prefab | Purpose | Root position |
| --- | --- | --- |
| BalanceBeam | Narrow beam, 4 m long | Middle of the beam's upper surface |
| SwingBar | Bar for grabbing and swinging, 3 m long | Middle of the bar at hand contact height |
| Ladder | Ladder, 4 m high | Middle; Position Y = 2 puts the bottom at Y = 0 |
| Zipline | Sloped cable, roughly 6 m long | Start at local Z = −3, end at +3; the start is 1 m above the end |

**GameObject → Mirror's Edge** also offers the four objects. The labels are in Russian: Балка для баланса (beam), Перекладина для раскачивания (bar), Лестница (ladder), Трос для зиплайна (zipline). Objects appear near the center of the current Scene view; set their Transform position afterward.

For an initial setup, put a beam 1 m above the ground between platforms, a bar around 3 m high, a ladder centered at Y = 2 and a cable rooted at Y = 5. Leave space under bars and cables for the body. Provide a platform at the ladder's top. Keep the ladder's Scale at (1, 1, 1); change its path points to adjust height because its existing volume calculates steps without Transform scale. Place the ladder in front of a wall; its thin collision surface is 15 cm behind the rungs.

**Known issue:** ladder entry can throw an exception when a required animation is missing. The prefab is included for testing, but ladder behavior is not fully fixed. See [TODO](../TODO.md).

## Controls

- Walk onto the beam from a platform. Balance activates automatically; ordinary movement returns when leaving the zone.
- Jump toward a bar with forward movement. Catching activates in its zone. W/S drive swinging, Space requests a jump off at a suitable phase, and Shift releases the bar. A/D move sideways along it.
- Approach a ladder from local **−Z**, facing **+Z**. Space supplies the upward action for grabbing; W/S climb, Shift releases.
- Jump toward a cable while facing its lower end along local **+Z**. Catching happens in the air; Shift releases.

For bar-to-bar movement, swing and press Space during forward motion. Checks covered root distances of 2.4 m and 3.2 m, and a next bar rotated by 35° at 2.4 m. Reach depends on speed and height; catching any arbitrary bar is not guaranteed. Leave room under the next bar for the body.

Existing SwingBar approach zones expand at runtime. Both hands stay within the usable bar length; sideways movement stops near its ends. Swing sound fades when hanging still. Releasing Shift clears crouch requests even while parkour restricts input.

## Change dimensions

Moving or rotating the root moves both geometry and interaction zone. Edit **Spline Controls** on the appropriate volume, or use its Scene handles, to change the beam length, cable path or ladder height.

Then select **Parkour Object Geometry** and press **«Обновить геометрию и зону взаимодействия»** (Rebuild geometry and interaction zone). The component also exposes thickness, material, bar length and ladder width. Ctrl+Z can undo a rebuild. Children inside `Geometry` are rebuilt by this button; put your own extra objects beside `Geometry` under the same root.

To add geometry to an existing Balance, Swing, Ladder or Zipline volume, select the object holding that component and use **GameObject → Mirror's Edge → Прикрепить геометрию к выбранному Volume** (Attach geometry to selected volume).

## Pull up from a ledge

Space while hanging climbs onto the checked surface. Imported root motion no longer pulls the physical body backward. A low ceiling selects a crouched pull-up; a route without body clearance is rejected. Existing grab and pull-up animations are used.

## Jump after a vault

During a vault with **one left hand**, hold **Space until the vault ends**. Once past the obstacle, the character receives one normal-strength upward jump and keeps forward speed. Ordinary gravity and collisions apply, including ceilings. Holding the button gives no further jumps in this flight.

Releasing Space keeps the usual vault. `VaultOnto` and vaults using both hands keep their existing behavior.

Checks use a separate project with the actual world and animations on Unity 6000.6.4f1. Complete map playthroughs and all ladder exits still require testing.
