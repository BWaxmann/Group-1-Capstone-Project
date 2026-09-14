# Cat locomotion

Use the existing `Assets/Scenes/CC_Greybox.unity` scene and its normal game-start
flow. The existing B&W, Black, Ginger and Siamese cat prefabs use this shared rig
and controller. Their original materials, gameplay settings, behaviour Animator,
audio and mood UI are retained. Their head/body hitboxes follow the new skeleton.

The three differently shaped cat variants are unchanged. The current scene,
navigation data, spawn pool and all teammate scripts are also unchanged.

- `Models/CatRig.fbx`: revised mesh, skin weights and skeleton.
- `Clips`: Run and in-place JumpUp/JumpDown animations.
- `CatLocomotion.controller`: shared Run and jump states.
- `Scripts/CatRunAnimation.cs`: plays/pauses Run based on the navigation agent.
- `Scripts/CatLinkJump.cs`: coordinates existing navigation-link traversal and
  jump poses without changing destinations or the gameplay stopped flag.

Stopping holds a run pose until an attack animation is added. An already-started
jump finishes landing before obeying a gameplay stop; disable/re-enable restores
navigation ownership. Nearly level links use a normal-speed crossing. The old
hidden visual hierarchy is retained to avoid breaking existing references; this
is not a completed Quest performance-optimization pass.

The approved Maya animation sources remain outside the Unity project. Preview
scenes, trial pools, duplicate trial cats and unused exported clips are archived
outside Assets rather than retained as production dependencies.

Validation covered prefab references, preservation of existing component settings,
all seven pool entries, and navigation between all 15 current link endpoint pairs
in both directions, including stop and recycle cases. Full VR gameplay and
performance still require a play-through; the navigation tests isolate the
animation components from other gameplay scripts.
