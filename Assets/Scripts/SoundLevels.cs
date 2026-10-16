// How loud whole groups of sounds are, on top of each sound's own volume (1 = as set). Cat Cafe Rush and Waves set
// these from the RushDirector's sound mix; every other scene leaves them at 1.
// The music's volume is scaled directly. 3D sounds (the cats, the props) carry further or less far instead (their
// minimum distance is scaled), so turning them up makes them louder across the room without clipping up close.
public static class SoundLevels
{
    public static float music = 1f;
    // Everything a cat says or does: meows, eating, swipes, poofs
    public static float cats = 1f;
    // The cafe's background sounds: cats' footsteps, props being knocked, landing and put back
    public static float world = 1f;
}
