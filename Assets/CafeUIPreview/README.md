# Cafe guide preview

Both preview scenes now use GuideModelNeutral.png and GuideModelTalking.png,
the purple low-poly portraits based on the actual game cat. The talking frame
is a local mouth-only composite: all alpha values and pixels outside the mouth
match the neutral frame exactly. Earlier orange and glossy purple portraits
are retained as alternatives, but no longer assigned to either preview scene.

## Cafe backdrop trial

Open CC_Greybox_UITrial.unity and press Play to preview the floating guide in
a copy of the current cafe. Original scene content and references are copied
unchanged, with one guide bootstrap added. The guide hides the pickup feeders and
the old pickup-instruction canvas until dismissed. Existing player input, tracking,
game manager, spawners, audio and cameras remain running normally.
Press A / X or click the gold button to advance. LET'S PLAY releases the feeders
after the dismiss buttons are released; picking them up starts the original countdown.
The guide never starts or cancels the manager's countdown. It stays world-anchored
at its initial placement rather than following head movements.
The broad tutorial backing panel is hidden; the cat, bubble and cards float
in world space. Final placement and occlusion need a Unity visual check.
No shared prefab changes or build-settings additions. Original CC_Greybox untouched.
Existing score and timer text retain their original positions, references and
game-controlled visibility. Only their colours change at runtime in the trial.
There is no sample HUD in this scene. The clock model is still pending.

## Standalone desktop prototype

Open CafeGuidePreview.unity and press Play. The board is built at runtime.
This is a standalone desktop interaction prototype, not a replacement for CC_Greybox.
Click the gold target to reveal/advance dialogue and simulate the start shot.
Space reveals the current sentence. R restarts. The countdown does not start gameplay.

Included: two-expression guide cat, four player-paced pages, power-up reference
cards, a practice countdown, static score/bonus and future-clock placement samples.
Palette: plum, cream, peach and warm yellow. Built-in Arial is a temporary font.
Cards are text placeholders, not final pickup icons. The orange source copies are
preserved. The preview now references generated purple neutral/talking variants
with actual alpha transparency. Unity visual inspection is still needed.

Purple art used built-in image generation, not the CLI fallback.
Prompt 1: Recolour the original rounded cat head lavender with violet stripes,
dark plum outline, cream muzzle, golden eyes and pink nose/ears. Preserve a
closed smile. Centre the full head on actual transparent alpha, no white fringe,
glow, external shadow, checkerboard or text.
Prompt 2: Edit the purple neutral frame to open the smiling mouth with a pink
tongue. Keep canvas, head outline, placement, fur, eyes, nose and transparent
background unchanged; alter only the mouth for a matching talking frame.

No existing scripts, scenes, prefabs, build settings or manager connections changed.
No edits or direct calls to GameManager, PlayerInput or Treat. The cafe trial
uses GrabbableLaunchers and InstructionsText to gate pickup availability only.
This preview is deliberately not added to build settings.

Pending: Unity import/play test, actual pickup illustrations, chosen team fonts,
cafe placement and headset readability/controller testing. Current start interaction
in the standalone scene is a mouse simulation, not a VR projectile target.
The cafe trial uses a button-dismissed guide BEFORE pickup, preserving the normal
pickup/countdown flow without a GameManager edit. Neither mode has been play-tested
in Unity after this change. Test pickup gating, controller dismissal, countdown,
live score/bonus/timer and headset placement before promoting anything.

Score inspection: the current code has a pickup-based multiplier, not a hit streak.
Shootable power-ups: double score, infinite ammo, defense shield, nearby-cat stun.
Ammo refill moves towards the player. Do not advertise a combo or dance as implemented.
