// Liminal can run the experience several times in one sitting without restarting the app, and static variables keep
// their values between runs (Liminal's requirements: re-initialise them for each run). This puts every static the game
// keeps back to how it starts: lists of things in the scene emptied (they're filled again as the scene's objects wake
// up), shared flags and counters back to their starting values. GameManager calls it first thing, before any other
// object in the scene wakes up.
public static class RunReset
{
    public static void ForNewRun()
    {
        // Lists of things in the scene
        CatMovement.active.Clear();
        CatOrder.all.Clear();
        CatRaid.active.Clear();
        LauncherKitten.active.Clear();
        PopUpCat.active.Clear();
        KittenRun.ResetStatics();
        SpyCat.ResetStatics();
        FlyingProp.ResetStatics();
        PropPile.ResetStatics();
        FloatingWord.ResetStatics();
        SoundBite.ResetStatics();
        Squishable.ResetStatics();

        // Shared settings and counters (the scene sets its own again as it starts)
        CafePrefabs.instance = null;
        MafiaHeist.current = null;
        CatMovement.targetingChanceOverride = -1f;
        PropPile.toppleOnHit = false;
        PropPile.barsWaitForNinja = false;
        GravityBeam.catchStreak = 0;
        SpawnManager.holdSpawns = false;
        Treat.cleanShots = false;
        Treat.aimAssistTurnSpeed = 120f;
        ParticleManager.effectScale = 1f;
        PickupManager.allowed = null;
        SoundLevels.music = 1f;
        SoundLevels.cats = 1f;
        SoundLevels.world = 1f;
    }
}
