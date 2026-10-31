# Catastrophe Cafe: where things live

The game is one scene, `Assets/Scenes/CatCafeWaves.unity`. A round is a list of **phases** on the **Cat Cafe Rush**
object (its Rush Director component). Almost every design knob is in the Inspector, not in code: this page says which
object to click. Values below are the code defaults; what's set in the scene wins.

Scripts marked **(B)** are Brian's, **(J)** are Joshua's.

## The round at a glance (as set in the scene now)

| # | Phase | Seconds | Cats | Banner | Announcer line | Launcher kitten every |
|---|---|---|---|---|---|---|
| 1 | Feed | 20 | 3 | FEED THE CATS! | feed_first | 12 s |
| 2 | Stampede | 20 | 3 | KITTEN STAMPEDE! | stampede | |
| 3 | Feed | 25 | 4 | COMBO ORDERS! | feed_combo | 5 s |
| 4 | Feed | 20 | 4 | | | 4 s |
| 5 | Catch | 15 | 3 | CATCH! | catch | |
| 6 | Spy | 20 | 3 | SPY CATS! | spies | |
| 7 | Remix (Dinner Rush) | 90 | 3 | DINNER RUSH! | finale | 14 s |

Each phase has: Kind (Feed, Catch, Boss, Stampede, Pop Up, Remix, Spy), Seconds, Cats (hungry cats at once), Shortest
and Longest Order, Banner, Line (an id in `Resources/Announcer/lines.txt`), Attack Chance, Raid Every, Latch Every.
A phase's clock stops while the announcer is talking and while a big moment is still playing out, so a round runs a
little longer than the seconds add up to.

## What's on which object

| Object | Where | Components, and what they're for |
|---|---|---|
| **Cat Cafe Rush** | scene | **RushDirector** (J) runs the round phase by phase. **Announcer** (J) plays the manager's voice lines with subtitles. **CatChaosSettings** (J) is how props fly and how cats knock them. |
| **CafePrefabs** | scene | **CafePrefabs** (J): a slot for each thing the scripts spawn (words, rings, splat, bubble, tip jar, rope, recipe, reticle, beam). Empty slot = built in code. See `Assets/Scripts/CatCafeRush/CafePrefabs_HowTo.md`. |
| **Cat Spawn Manager** (x4) | scene | **SpawnManager** (B): the original wave spawners. The director holds them and brings cats in itself. |
| **GameManager** | `Prefabs/GameManager` | **GameManager** (B): the round clock, countdown, score and tips canvases, music, finale ("LAST ORDERS!"), ranks, start-up message boxes, the 9-to-5 shift clock. |
| **[ExperienceApp]** | `Prefabs/[ExperienceApp]` | Liminal's rig. **PlayerInput** (B): the launchers, firing, straight shots, reticle, autofire, grip threshold. |
| **Dual Launchers** | `Prefabs/Dual Launchers` | **GrabbableLaunchers** (B): grab both to start; the Highlight Effect slot (Eleni's hover effect). |
| **Cats** | `Prefabs/Cat/*`, `Resources/SpyCat` | **CatMovement** (B): the cat AI, mood, the fed launch-and-pop. The director adds **CatOrder**, **CatJuice**, **CatWaddle** (J) to pooled cats at runtime. |
| **Treats** | `Prefabs/Treats/*` | **Treat : Launchable** (B): flight, clean shots through furniture, splats and squish on the cafe. |
| **Prop piles** | scene | **PropPile** and **FlyingProp** (J): the plates, cups and jars cats knock flying, caught with the grip beam. They sit on **DefendPoint** (B). |
| **Boss Cat Spawner** | `Prefabs/Object Pools` | **BossCatSpawner** (B) and the boss cats (B). The Mafia heist borrows the Mafia cat from here. |
| **Vents** | scene, named `Vent...` with a `...Lid` | **CafeVent** (J) is added at runtime: lid clanks open, cats and kittens pour out. |
| **Plants, cushions, mugs** | scene | Add **Squishable** (J) by hand to anything that should wobble when hit (untick Batching Static on it). Add **TreatsPassThrough** (J) to a collider that's bigger than its art. |

Built by the director at runtime, so there's no scene object to click: the tip jar (**TipJar**), the chalkboard tally
(**TipsBoard**), the day-to-night sky and light (**ShiftSky**, **ShiftLight**), pop-up words (**FloatingWord**),
the aim ring (**AimReticle**), the grip beam (**GravityBeam**), big sound bites (**SoundBite**). Each takes its look
from a CafePrefabs slot when one is filled.

## Where to tune what

| What | Click | Knobs |
|---|---|---|
| Round length, order of phases | Cat Cafe Rush > Rush Director > **Phases** | the table above |
| Pauses between phases | Rush Director > **Between waves** | Wrap Up Max (12 s), Last Call (3 s) |
| Shooting feel | Rush Director > **Feel** | Treat Speed (25), Clean Shots, Hitbox Generosity (0.9), Aim Assist Angle (6) and Turn Speed (120) |
| Launchers | [ExperienceApp] > **Player Input** | Straight Shots, Straight Shot Speed, Show Aim Reticle, Autofire, Grip Press Threshold |
| Orders and patience | Rush Director > **Order moments** | Patience (10 s + 3 per extra treat), Impatient Cats Raid, Quick Service Coins, Wrong Colour Leaves |
| Raids | Rush Director > **Order moments** > Raid | Wind Up, Sprint Multiplier, Save Bonus, Saved Text; Raid Every on each phase |
| Kitten stampede | Rush Director > **Kitten Stampede** | Herd Roam Min/Max, Max Kittens (24), Hit Radius, Flood Chance, First Herd Floods |
| Launcher kittens | Rush Director > **Launcher kittens** | Shakes To Free (3), Shake Speed, Bored After (8 s), Stack Chance, Hint ("SHAKE!"); Latch Every on each phase |
| Spy cats | Rush Director > **Spy cats** | Approach Seconds (8), Swing Period, Scare Seconds (2.2), Swings Needed (3), Look Angle, Steal Coins, spy music volumes |
| Mafia heist | Rush Director > **Mafia heist** | Heists Per Phase (2), Heist Gang (2), Walk Speed, Treats (3), Steal (10), Pillage Seconds (5), theme volume |
| Catch phase | Rush Director > **Ninja and the piles**, **Catching** | Drag Props Home, Prop Drop Margin (0.35, Dinner Rush 0.6), Restock Piles, Catch Aim Angle, Tidy Bonus |
| How props fly | Cat Cafe Rush > **Cat Chaos Settings** | Max Airborne (4), launch speeds, drift, Respawn Delay, knock chance and reach, prop sounds |
| The grip beam | GravityBeam (on the launchers at runtime) | Aim Angle (20), Range, Hold Distance (1.3), Flick To Return, Flick Speed (2.2) |
| Dinner Rush finale | Rush Director > **Remix finale** | Remix Breather (6 s), Flood At (0.45), Ninja Sit Min/Max (7 to 10 s) |
| Vents | Rush Director > **Vents** | Cats Use Vents, Vent Chance (0.35), Kitten Vent Chance (0.5), Vent Arc (120) |
| Tips and the board | Rush Director > **Cat Cafe Waves**, **Tips board** | Coins Per Treat, Boss Coins, Raid Spill Coins, Tidy Bonus Coins; Coin Value (0.5), Currency, labels, Always Line, Fit To Board |
| Shift clock, sky, light | Rush Director > **The shift** | Shift Clock, Start/End Hour (9 to 17), Shift Sky (start 38, end 88, dusk from 78), Shift Light, Warm Up At Start, Shader Variants |
| Sound mix | Rush Director > **Sound mix**; GameManager > **Sounds** | Music Level (0.8), Cat Level (1.3), World Level (1.3); the music clips and intro length |
| Announcer | Rush Director > **Cat Cafe Waves**; Cat Cafe Rush > Announcer | Announcer on, line ids; Speaker Name, Subtitle Drop and Distance. Lines: `Resources/Announcer`, sounds: `Resources/CafeSounds` |
| Start-up and end screen | GameManager > **UI**, **Finale**, **End Screen** | Startup Message and Boxes, Countdown, Finale Duration (30 s), Ranks |
| Looks | CafePrefabs slots; Rush Director > **Look** | your prefabs; Effect Scale (0.5), Catch Glow |

## Scripts by job

**The round:** RushDirector (J), Announcer (J), GameManager (B), SpawnManager (B), RunReset (J, puts every static back
for Liminal's re-runs), Warmup (J, fonts, shaders and sounds loaded up front), SoundLevels (J), SoundBite (J),
CafePrefabs (J).

**Cats:** CatMovement (B), CatFootsteps (B), CatOrder (J), OrderBubble (J), CatRaid (J), CatJuice (J, squish on hit),
CatWaddle (J, phat cats), BossCatSpawner and the BossCat family (B), CafeVent (J).

**Phases:** KittenRun (J, stampede), LauncherKitten (J), SpyCat (J), MafiaHeist (J), NinjaRushCat (J,
the catch phase ninja).

**Player:** PlayerInput (B), GrabbableLaunchers (B), Treat and Launchable (B), AimReticle (J), GravityBeam (J),
Haptics (J), PcHeadsetPicker and OpenVRHeightFix (J, PC headsets), ChangePointerMaterial (B).

**Props and the cafe:** FlyingProp (J), PropPile (J), CatChaosSettings (J), FlutterPiece (J), Squishable (J),
TreatsPassThrough (J), DefendPoint (B), ObjectSpawner (B, the pools).

**Tips and words:** TipJar (J), TipsBoard (J), FloatingWord (J), ParticleManager (B), ScoreParticle (B),
BillboardCanvas (B), InstructionsText (B).

**Time of day:** ShiftSky (J, Farland Skies in `Resources/ShiftSky`), ShiftLight (J).

**Pickups (B):** `Scripts/Pickups`. Cat Cafe Waves only allows x2 points (Rush Director > Allowed Pickups).

**Editor menus (`Assets/Editor`):** Catastrophe Cafe > Editor Headset (Index or mouse), Add Cafe Prefabs To Scene,
Make Pop-Up Word Prefab, Save Shader Variants From The Last Play, Build for Valve Index. QuestBuild makes the Quest APKs.

**Not used by Cat Cafe Waves** (kept until closer to release, Brian's call): CatChaosDirector, the two scene
generators, SpaceCafeVisuals, WallWalkingTestScript, NinjaWaypoints, WaypointManager, Speen, Yarnball, TrajectoryPreview.

## Stutters on the Quest

The warm-up (Rush Director > The shift > Warm Up At Start) builds the fonts' letters and decodes the sounds in the
lobby, a little each frame. A shader is compiled the first time something is drawn with it, which is the hitch at the
first boss cat; to pay that in the lobby too: Edit > Project Settings > Graphics > Shader Loading > Clear, play a whole
round in the editor, stop, Catastrophe Cafe > Save Shader Variants From The Last Play, save the scene. Never call
Shader.WarmupAllShaders: on the Quest it compiles every variant in the build and freezes the splash screen for minutes.

## Working together

- Pull before you start. Commit small and often, with a message that says what changed.
- One person in `CatCafeWaves.unity` at a time, and say so in Discord first. Two people saving the scene is how work
  gets lost in a stash.
- Commit scene changes on their own, not mixed in with scripts or art.
- Don't commit `Assets/Art/Fonts/Salted Caramel SDF.asset` (it rewrites itself every run) or `unityProject.vrmanifest`.
- The Console says which CafePrefabs slots are in use when the game starts. A prefab that isn't showing up usually
  isn't in its slot.
