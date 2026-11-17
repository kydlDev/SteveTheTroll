# Changelog

## 2.0.3

- Changed: the menus of this mod take their text sizes, text colours and font from KydlCore. Needs KydlCore 1.1.1 or newer.
_Not released yet: this version now also has a real change (the riding fix below), so it is tested with the full DrakesTestLab suite before release._

- Changed: the category switches in `[General]` are written with capital letters, like all KydlDev mods: `Enable Slippers`, `Enable Costume`, `Enable Trolls`, `Enable Troll Hub` and `Enable Riding` (were `Enable slippers`, `Enable costume`, `Enable trolls`, `Enable trollHub` and `Enable riding`). The values you had set are taken over.
- Changed: `Enable Mod` is now the first setting of `[General]`, in the F1 menu and in the F6 hub (your value is taken over).
- Fixed: a short stutter for the host (or a single player) about 20 seconds after joining a world and then every 30 seconds: the mod looked through every saved object of the world in one frame to check its troll list (12 to 78 ms). It now does that in small slices in the background, so no single frame is held up.
- Fixed: when the game closed (or you logged out) while riding a troll, the troll stayed marked as ridden by you, so other players could not mount it, and it did not go back to work. Now, when you log in again, that troll is freed once it is near: the marker is removed, it stops following and goes back to work from its hub. You start on foot; the Troll Halter and Seat was never taken from your inventory.
- Fixed: while riding, the rider's back could sink a little into the back of the troll's chair after a while (the sitting animation moves the hips after the rider was centred when mounting). The rider's back is now kept just in front of the chair back, and if it still goes 2 cm into it, the rider is centred again.

### Testing
- Unit tests: 1491 passed. Integration tests: 74 passed.
- Property-based tests (FsCheck, included in the unit tests above): 27 property checks with generated inputs.
- Coverage of the testable code: 99.4%. Mutation testing (Stryker): 92% of the mutants are caught by the tests.
- Performance benchmarks (BenchmarkDotNet): 10.
- Static analysis (SonarQube): no Medium or High issues.
- In game with DrakesTestLab: the full suite of 104 tests per run, run 3 times in a row on the release build, all green (the release requirement).

## 2.0.2

- Changed: all the **Enable** switches are now together in `[General]` and have new names: `Enable Mod` (was `[General] EnableAll`), `Enable slippers`, `Enable costume`, `Enable trolls`, `Enable trollHub` and `Enable riding`. The values you had set are taken over; the old entries are removed from your config file. `[TrollHub] AutoAssign` stays in `[TrollHub]`.
- New: `[Trolls] Two star trolls break fortress walls` (on by default) and `[Trolls] Trolls mine Petrified Gammeltroll` (on by default). Off: your trolls leave the Ashlands fortress walls / the Petrified Gammeltroll alone.
- Changed: the developer console commands (`stt_...`) exist only when the developer's own KydlTools mod is loaded. The `[General] UnlockWord` setting and the `stt_unlock` command are gone; players do not see any of these commands.
- Fixed: the name tag of the troll you ride stays on top of the troll instead of drifting around it.
- Fixed: when you get on a troll, you are moved to the middle of the chair and in front of its back, so you no longer sit sideways or with your back inside the chair.
- Fixed: a farming troll that steps around a friendly troll standing in its way is no longer sent on a 20 m detour of its own a second later (the sideways step counted as being stuck), so it passes the friend and goes on to the tree.

## 2.0.1

- New: **ride your troll.** Craft the **Troll Halter and Seat** (workbench level 3: 15 Wolf pelt, 15 Linen thread, 10 Iron, 10 Round log), keep it in your inventory and press the alternative interact key on one of your tamed trolls. The halter and a seat basket go on the troll and you sit in the basket. You steer with the movement keys and the camera, Shift (or autorun) makes it run, the left mouse button swings its weapon and the right one does the overhead chop; the same key (or the Use key) gets you off. A ridden troll takes no farm, guard or hub orders, your other trolls still guard you. The item is never used up; after a death it is in your tombstone. Other players do not see the halter and seat yet. Config `[Riding] Enable`.
- While you ride, the rider sits on the seat in every frame: the seat hangs on the troll's back and moves with its breathing, walking and swings, and the rider moves and leans with it. A ridden troll is moved only by its rider: its own farming, way-finding and idle behaviour are off until you get off, then it takes up its work again.
- Trolls no longer swing at anything within 15 m of a Troll Hub's outer edge (trees, rocks and creatures there are left alone, like around the bed).
- Trolls walk around plain rocks that give nothing instead of smashing them.
- Trolls no longer pick up Stone and Grausten into their backpack.
- Names of targets in the troll messages are in English.
- Fixed: with lower graphics settings, a troll far away from you no longer stops moving. The mod now follows the game's own simulation distance and keeps every zone the game does not run around your far trolls.
- New: no wild creature spawns within 50 m of a Troll Hub's outer edge, the same protected base area a workbench gives (config `[TrollHub] NoSpawnRadius`, 0 = off, applies after a restart).
- A troll that stands still on its way somewhere is now noticed after 10 s: you get a message, its map pin shows "stuck", and the mod tries to free it (keeps its area loaded, rebuilds it, then moves it along its way). The trolls are also listed in `BepInEx/config/SteveTheTroll/<world name>_<world uid>/trolls.json`.
- Trolls now walk down and up cliffs: when the game finds no path to a tree, rock, far farm place or the way home, the troll walks the straight line itself over the terrain (up to 80 m, up to 40 m height difference) instead of giving the target up. It works in every walk a troll makes, not only on the way home.
- A troll with a hub that has farmed everything around it now walks on to look for more (150 m at a time, the zones load around it) until it is 950 m from its hub; when nothing is left it tells you once and suggests planting trees or building a new hub elsewhere.
- A troll you are looking at (standing still on purpose) is no longer treated as stuck.
- A troll no longer stands guard over a creature it cannot hit inside the bed no-hit zone; it goes back to farming after 5 s.
- Added a flight recorder to the diagnostics: when a troll stalls, the facts of every troll are saved to a file next to the log (needs KydlCore 1.1.0).
- The developer console commands (`stt_...`, test helpers) now stay locked until you type `stt_unlock <word>`; the word is the new setting `[General] UnlockWord` in your config (empty = they stay locked). The `stt_freeze` / `stt_unfreeze` commands are gone.
- Changed: every Troll Hub distance (the 500 m circle a hub serves, the 950 m farm limit, the 1000 m reach, the 1250 m half-speed limit and the 1000 m between two hubs) now counts from the hub's outer edge instead of its middle. Each one reaches about 15 m further out; two hubs need about 1031 m between their middles. The depot ring of a hub (new depots 15 - 50 m away) counts from the hub's outer edge as well; depots that already stand nearer to a hub are still used.
- The "trolls from your last visit are missing" message is shown once for each missing troll, not again at every login.
- Fixed: a full troll that gets within 10 m of its depot now unloads at once, also while it is still walking down a cliff towards it (before, a depot built at the foot of a cliff could keep the troll standing next to it for up to 40 s).
- Performance: unloading a full backpack into the depot no longer stops the game for a moment (96 stacks took about 0.05 s instead of about 1.5 s in the test world), and the search for trees and rocks drops far-away objects earlier.
- Fixed: the game log is no longer flooded with "Physics.ClosestPoint can only be used with ..." warnings while a troll works its way past rocks (none in five full lab runs in a row of this version).
- Every error the mod catches is now written to the log with the exact exception type and what the mod did instead, so the next bug report names the exception.

## 2.0.0

- Tested in game with the full TestLab suite (63 tests): 3 green runs in a row on 2026-11-05, plus the ecosystem run with all 12 KydlDev mods loaded together.
- After a fight (a creature attacking you, or a counter-attack) a troll goes back to the tree or rock you ordered it to break, instead of the nearest one.
- A troll whose swings miss a creature at the edge of its reach steps closer instead of swinging at the air.
- A troll on its way to a tree steps sideways around a friendly troll in its path instead of pushing it ahead.
- The Troll Hub piece shows in the build menu only after the Steve The Troll set bonus has been active once, and it is then
  learned with the game's normal "new piece" message.
- The F6 tabs open as you progress: **Costume** once the Steve The Troll set bonus has been active for the first time,
  **Troll Hub** once your first hub is built, **My trolls / Troll work / Trolls** once your first troll is tamed. This is
  remembered on the character (survives a relog).
- After your first hub is built, the Trolls tab shows the distance "from hub", not "from your bed".
- The Fluffy Slippers have a thicker, rounder cream rim around the opening (it was a thin ribbon before); new mod icon.
- Multiplayer: every player needs exactly the same SteveTheTroll version (2.0.0 joins only 2.0.0) and the same KydlCore
  version.
- The feature switches now apply while you play: turning `[Slippers]`, `[Costume]`, `[Trolls]` or `[TrollHub]` `Enable`
  off removes their F6 tabs at once (and, for the trolls, the background troll work). Turning a part ON that was off
  when the game started still needs a restart.
- The slippers, the costume pieces, the Troll Club and the Troll Hub are listed in KydlCore's KY-Items registry.
- Troll Hub: new build menu icon (the word HUB on a wooden sign).
- The troll costume recipes (head, suit, trousers, Troll Club) now appear only after you have put on the Fluffy Slippers
  once: craft the slippers, wear them, then the costume. Characters that already know a costume recipe keep it.
- Inside: the mod's code was reorganised (KydlDev mod standard). Nothing else should change.

- Breaking: SteveTheTroll now needs the KydlCore mod (hard dependency). Install KydlCore as well.
- The menu is now tabs (Slippers, Costume, My trolls, Troll work, Trolls, Settings) in the KY hub window of KydlCore (
  F6); the own menu window and
  the `[General] MenuKey` setting are gone. The troll tabs show a note until you have a living tamed troll.
- New config switches: `[General] EnableAll`, `[Slippers] Enable`, `[Costume] Enable`, `[Trolls] Enable` (taming,
  farming, backpack, depots, map pins).
  A switched-off part is not set up at all: restart the game after changing it.
- The attack key moved from `[Controls] AttackKey` to `[Trolls] AttackKey` (set it again if you changed it).
- `[DebugLog] Enable` writes detailed lines to the BepInEx log.
- **Troll Hub**: a big building you make with the hammer (KY-Items; 50 wood, 20 copper ore, 20 tin ore, 20 resin, 50
  stone) once the Steve The Troll set bonus has been active.
  Up to 10 per player, at least 1000 m apart, each serves a 500 m circle. Trolls go there when you log out, unload there
  and heal there like at the bed;
  a troll uses the hub you assigned in the new **Troll Hub** tab (F6) or the nearest one (`[TrollHub] AutoAssign`). Each
  hub has a sign (HUB 1, 2, 3 ...)
  you can rename and style with the pen in the tab or with E on the sign. With hubs on and the set bonus active, trolls
  with no hub within 1250 m work at half speed
  and trolls inside a hub circle are 50 % faster than vanilla (instead of 25 %). Removing a hub with the hammer gives
  back what it cost. `[TrollHub] Enable` switches it off.
- Hub ranges: a troll uses any of your hubs within 1000 m, and a troll with a hub does not farm further than 950 m from
  it.
- Walk back to the hub: a troll you have not told to follow you that ends up more than 1000 m from its hub walks back
  until it is inside 950 m and then farms again.
  Only a new follow order, or an attack on the troll or on you, interrupts the walk.
- The Trolls tab shows "Distance from <hub name>" for a troll that has a hub (the bed otherwise); messages name the hub
  they talk about.
- A troll with a full backpack now unloads first, even while it is farming. A depot that could not be removed because a
  chest window was open is retried; a troll that finds no depot spot tries again after 20 s.
- The red hub notice is centred on the screen and is no longer cut off at the left on wide screens.
- Stuck trolls: after 3 s without progress a troll rages against what blocks it (trees, rocks, logs, creatures; never
  anything a player built, never a tamed creature), then turns 90 degrees and walks up to 10 m, then an L-shaped detour.
  Two failed rounds make it give that one tree up for 120 s and take another; a hub is never given up, and the walk back
  to the hub has the same help now. Two trolls meeting head on both turn right.
- Mini trolls on the slippers lean with the slipper (at most 50 degrees from upright) and turn smoothly instead of
  snapping when the slippers are put on.
- Troll Mascot trousers: the legs no longer stretch between the thighs when you walk, run or sit.
- Hub window 15 % wider; the Slippers / Costume / My trolls / Troll work / Trolls tabs use the Settings tab's text size.
- Far-troll farming costs much less: the zones around far trolls are searched once a second instead of on every game
  object update (fewer stutters with many trolls).
- Safer little trolls on the slippers: when the skin reading goes wrong they follow the foot instead of ending up under
  the sole.

## 1.0.0

- First release.
- **Fluffy Slippers** (utility slot, workbench 1): armor 5 (+1 per quality, up to 4), frost resistance, a little plush
  troll on each foot.
- **Troll taming** with the slippers: a wild troll near you for a minute becomes yours (Steve 1, Steve 2, ... with a
  club, Spencer
  without). One tamed troll near you with the slippers, 5 with the whole costume, at most 15 living tamed trolls per
  player in total.
- **Tamed trolls:** 3x health and damage, move and swing 25 % faster, immune to fire, frost, lightning, poison and
  spirit, resistant to
  pierce and slash. They wade 3x as fast from 1.5 m deep water and swim in very deep water up to three times their
  running speed
  (always a little faster than you when following, and catching up when far behind; faster acceleration and turning in
  water).
  In water a following troll keeps 15 m from you (room to steer the boat); tamed trolls never damage ships.
- **More trolls near home:** with the slippers, trolls spawn 200 - 500 m from your bed in every land biome (Ashlands and
  Deep North
  included), 2 at a time, 6 with the costume. Without the set power all of them carry a club; with it half come unarmed.
- **Farming:** trees, logs, rocks, ore veins and other breakables, Yggdrasil shoots, scorched and Timberwood trees,
  giant remains and
  petrified Gammeltrolls included. Focus per troll in the Troll work tab; Blackwood, Black marble, Timberwood and Gold
  ore are offered
  while the troll is in the Ashlands, Mistlands or Deep North. Wood, ores and scrap within 50 m go into a 4-chest
  backpack.
- **Depots:** full trolls walk home and unload into troll depots 15 - 50 m from your bed (chest stacks up to 5 high,
  ground levelled).
- **Protection:** trolls defend you within 50 m, rage when you are hit with a troll within 10 m, answer hits with ground
  slams.
  Trolls with 2 stars break Ashlands fortress walls in their way, on your order, or when you hit a wall near them.
- **Orders:** Mouse 4 (`[Controls] AttackKey`) sends your nearest troll at a creature, a breakable or to farm.
- **Home:** slippers off or logout - trolls wait 15 m from the bed; no troll swings within 15 m of it; quick healing
  near the bed.
- **Troll Mascot costume** (head, suit, trousers) and the **Troll Club** (150 blunt / 50 chop / 50 pickaxe, +35 / +20 /
  +20 per
  upgrade, wide swing for double damage as the secondary attack, breaks Yggdrasil shoots, scorched and Timberwood trees,
  giant remains
  and petrified Gammeltrolls at any quality). Set bonus **Steve The Troll**: +50 % movement speed, +25 % carry weight,
  -50 % stamina
  use, 75 % less stagger, up to 5 tamed trolls.
- **F6 menu:** Slippers, Costume, My trolls (distance, Follow, Kill), Troll work and Trolls tabs; map pins for your
  trolls.
