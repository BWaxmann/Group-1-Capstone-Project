# Cafe Prefabs: how to replace the game's built-in looks with your own

The game's scripts used to build a lot of things in code: the speech bubble over a cat, the tip jar, the splat when a
treat hits a wall, the catch beam. **Cafe Prefabs** is one object in the scene with a slot for each of those things.

- Slot **empty** = the code builds it, exactly as before. Nothing changes.
- Slot **filled** with your prefab = the game uses your prefab instead.

So you replace things one at a time, in the editor, with no code. If a prefab looks wrong, clear the slot: the old look
is back instantly.

## Setup (once)

Menu: **Catastrophe Cafe > Add Cafe Prefabs To Scene**. That makes the object. (Or: empty GameObject named
"Cafe Prefabs", add the `CafePrefabs` component.) Save the scene.

Keep your prefabs in `Assets/Prefabs/Cafe Prefabs/`. Save them as **prefab assets**, not scene objects, so the
originals never draw.

## The slots, and what the prefab needs

| Slot | What to make | What the code still controls |
|---|---|---|
| **Floating Word** | A `TextMeshPro` text object with a `FloatingWord` component on it. Pick the font, outline, style. | the words, their colour and size |
| **Ring Template** | An object with a `LineRenderer`: material, texture, corner/cap settings. | the circle, its radius and colour |
| **Kitten Trail Template** | An object with a `TrailRenderer`: material, width curve, colour gradient, time. | nothing: it's used as authored |
| **Prop Trail Template** | Same, for the trail behind a flying plate or cup. | nothing |
| **Splat** | A `ParticleSystem` that plays once (Play On Awake, not looping). | where it appears, facing out from the wall |
| **Order Bubble** | A prefab with an `OrderBubble` component. On it, drag in: **Bubble** (the art that pops and shakes, at scale 1), **Patience Ring** (a LineRenderer) and its **Ring Radius**, **Icon Anchor** (where the treat icon sits), **Queue Anchor** (where the next treats line up), **Chip Template** (the art behind each queued treat, a disabled child), **Face Renderer** (a disabled SpriteRenderer). All optional. | the mini treat models, the ring running down, the face flashing |
| **Tip Jar** | A prefab with a `TipJar` component. Drag in: **Body** (the jar, which rocks), **Fill** (the gold inside, modelled FULL with its pivot at its base: it's squashed down as it fills), **Lid** (disabled; shown when the jar is set aside full), **Coin Prefab** (one coin), **Height** (how tall, in metres). | coins flying in and spilling, the fill level, the label words |
| **Rope Template** | An object with a `LineRenderer` for the spy's rope. | both ends |
| **Recipe** | Any model: the recipe card a spy makes off with. | where it's held |
| **Reticle** | A prefab with an `AimReticle` component and two `LineRenderer` children drawn at a radius of 1: **Outer Ring** and **Inner Dot**. Any shape (a paw print works). | its size, colour, and where it lands |
| **Beam Template** | An object with a `LineRenderer`: material, width, colours. | the line itself, faded with the beam's strength |

Hover over any field in the Inspector for the same notes.

## Other slots that live elsewhere

- **Launchers** > `GrabbableLaunchers` > **Highlight Effect**: the effect shown while a hand is near (Eleni's).
- **GameManager** > **Startup Boxes**: up to three text boxes the start-up message is spread across (put a blank line
  between the parts of the message).
- **RushDirector** > Tips Board > **Fit To Board**: untick once the chalkboard is laid out by hand, so the code stops
  resizing its texts.

## Worked example: a nicer "Shoo!" (2 minutes)

1. Hierarchy: right-click > 3D Object > Text - TextMeshPro. Name it `Pop-up Word`.
2. Set the font to the game font, bold, centre aligned, add an outline. Any text, the game replaces it.
3. Add Component > `FloatingWord`.
4. Drag it into `Assets/Prefabs/Cafe Prefabs/` to make a prefab, then delete it from the scene.
5. Select **Cafe Prefabs** in the scene, drag the prefab into **Floating Word**. Press Play, feed a cat.
