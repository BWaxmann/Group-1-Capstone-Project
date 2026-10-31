using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Cat Cafe Waves: runs the round as a string of phases (the playlist on the Rush Director), one job at a time.
// Feed: hungry cats come in by the holes with colour orders (one colour per launcher); the Mafia cat and the launcher
//       kittens make trouble partway through.
// Stampede: herds of kittens gallop across the room (and pour out of the vents); any treat feeds them.
// Catch: the cats run off, and the ninja blinks about knocking the piles into the air for the player to catch.
// Spy: spy cats lower down on ropes from the ceiling vents after the secret recipe; waving your arms scares them off.
// Remix: the finale, a bit of everything at once, then the tidy-up bonus.
// Boss: the older finale (the ninja is fed, or makes a mess), kept for scenes that still use it.
// Brian's wave spawners are off in this scene; this spawns the cats itself.
public class RushDirector : MonoBehaviour
{
    // The numbers are what the scene saves for each phase, so a kind keeps its number for good (4 was the pop-up
    // cats, now gone); new kinds go on the end
    public enum Kind
    {
        Feed = 0,
        Catch = 1,
        Boss = 2,
        Stampede = 3,
        Remix = 5,  // the finale: a bit of everything at once (see the Remix finale settings)
        Spy = 6     // spy cats lower down from the ceiling vents after the recipe; the player waves them off
    }

    [Serializable]
    public class Phase
    {
        public Kind kind = Kind.Feed;
        [Min(1)] public float seconds = 35f;
        [Tooltip("Feed and Boss: how many hungry cats to keep about.")]
        [Min(0)] public int cats = 3;
        [Tooltip("Feed and Boss: shortest and longest order (treats per cat).")]
        [Min(1)] public int shortestOrder = 1;
        [Min(1)] public int longestOrder = 1;
        [TextArea] public string banner = "";
        [Range(0f, 1f)]
        [Tooltip("Feed: chance, at each stop while wandering, that a hungry cat goes to attack a pile. 0 = they just wander.")]
        public float attackChance = 0f;
        [Tooltip("Announcer line called out as this phase starts (its id in Resources/Announcer/lines.txt). Empty = none.")]
        public string line = "";
        [Min(0)]
        [Tooltip("Raids: about how often a hungry cat makes a run at the tip jar or a pile, in seconds. 0 = none.")]
        public float raidEvery = 0f;
        [Min(0)]
        [Tooltip("Feed: about how often a kitten bolts at the player and leaps onto a launcher, in seconds. 0 = none.")]
        public float latchEvery = 0f;
    }

    [Header("Between waves")]

    [Min(0f)]
    [Tooltip("When a phase's time is up the clock waits (up to this many seconds) for what's still going on to finish " +
             "(customers being served, props in the air, kittens running) before the next phase starts. 0 = straight on.")]
    public float wrapUpMax = 12f;
    [Min(0f)]
    [Tooltip("Nobody new comes in (customers, launcher kittens, the Mafia cat) for this many seconds at the end of a feeding phase.")]
    public float lastCallSeconds = 3f;
    [Min(0f)]
    [Tooltip("The ninja waits (up to this many seconds) for the player to deal with what it last knocked flying before blinking on.")]
    public float ninjaWaitsForCatches = 6f;

    [Header("Sound mix")]

    [Range(0f, 1.5f)]
    [Tooltip("How loud the music is, compared with how it's set on the Game Manager (1 = as set).")]
    public float musicLevel = 0.8f;
    [Range(0.5f, 2f)]
    [Tooltip("How far the cats' own sounds carry (meows, eating, swipes), compared with how they're set up (1 = as set).")]
    public float catLevel = 1.3f;
    [Range(0.5f, 2f)]
    [Tooltip("How far the background sounds carry (footsteps, props knocked, landing and put back) (1 = as set).")]
    public float worldLevel = 1.3f;

    [Header("Round")]

    public List<Phase> phases = new List<Phase>
    {
        new Phase { kind = Kind.Feed, seconds = 35, cats = 3, shortestOrder = 1, longestOrder = 1, banner = "FEED THE CATS!" },
        new Phase { kind = Kind.Catch, seconds = 12, banner = "CATCH!\n<size=55%>Grip to grab what flies</size>" },
        new Phase { kind = Kind.Feed, seconds = 35, cats = 4, shortestOrder = 1, longestOrder = 2, banner = "COMBO ORDERS!" },
        new Phase { kind = Kind.Catch, seconds = 14, banner = "CATCH!" },
        new Phase { kind = Kind.Feed, seconds = 39, cats = 4, shortestOrder = 2, longestOrder = 3, banner = "THE CAFE IS PACKED!" },
        new Phase { kind = Kind.Boss, seconds = 45, cats = 1, shortestOrder = 1, longestOrder = 2, banner = "NINJA BOSS!\n<size=55%>Feed it before time's up!</size>" },
    };

    [Min(0.5f)]
    public float bannerSeconds = 2.5f;

    [Header("Cats")]

    public ObjectSpawner catPool;

    [Tooltip("Where cats come in, and where they run off to (in front of the player).")]
    public List<Transform> doors = new List<Transform>();

    [Min(0.2f)]
    [Tooltip("Shortest gap between new cats arriving, in seconds.")]
    public float arrivalGap = 1.5f;

    [Min(0.5f)]
    [Tooltip("Order bubbles are scaled up so they read from across the room.")]
    public float bubbleScale = 1.7f;

    [Header("Foods (each hand fires one)")]

    [Tooltip("Treat pool positions: 0 = Blue, 1 = Orange.")]
    public int leftFood = 0;
    public int rightFood = 1;
    public GameObject leftTreatPrefab;
    public GameObject rightTreatPrefab;
    public Color leftColour = new Color(0.3f, 0.6f, 1f);
    public Color rightColour = new Color(1f, 0.6f, 0.15f);
    [Tooltip("The tin hung under each launcher, tinted that hand's colour.")]
    public GameObject tinPrefab;
    [Min(0.01f)] public float tinSize = 0.09f;

    [Header("Ninja")]

    public NinjaRushCat ninjaPrefab;
    public List<Vector3> ninjaStops = new List<Vector3>();
    [Min(1)] public int catchKnocksPerStop = 2;
    [Min(0.2f)] public float catchSitMin = 0.8f;
    [Min(0.2f)] public float catchSitMax = 1.2f;
    [Min(1)] public int bossKnocksPerStop = 1;
    [Min(0.2f)] public float bossSitMin = 2f;
    [Min(0.2f)] public float bossSitMax = 2.8f;
    public string bossFedBanner = "NINJA FED!";
    public string bossLeftBanner = "THE NINJA VANISHED!";

    [Header("Feel")]

    [Min(1f)]
    [Tooltip("How fast treats fly, in m/s (the original game uses 15).")]
    public float treatSpeed = 25f;

    [Tooltip("Which pickups cats can drop (0 = x2 points, 1 = InstaKill, 2 = Shield, 3 = Spread Shot, 4 = Rapid Fire). " +
             "InstaKill makes colour orders pointless and the shield protects nothing here, so only x2 by default.")]
    public List<int> allowedPickups = new List<int> { 0 };

    [Tooltip("Treats fly through furniture and only count when they reach a cat (or a pickup).")]
    public bool cleanShots = true;

    [Range(0.3f, 2f)]
    [Tooltip("Size of the invisible hit area around each cat's body (1 = about the body's size). 0.3 turns it off.")]
    public float hitboxGenerosity = 0.9f;

    [Range(0f, 20f)]
    [Tooltip("Light aim assist: a treat that's already within this many degrees of a cat wanting its colour curves " +
             "towards it. 0 = off.")]
    public float aimAssistAngle = 6f;

    [Min(0f)]
    [Tooltip("How sharply the aim assist can curve a treat, in degrees per second.")]
    public float aimAssistTurnSpeed = 120f;

    [Header("Catching")]

    [Tooltip("Knocked props burst outward in any direction (not just towards the player), then drift back in front as they fall.")]
    [Range(0f, 180f)] public float burstSpread = 180f;
    [Tooltip("How fast the burst throws a prop sideways, in m/s.")]
    [Min(0f)] public float burstOutwardSpeed = 3.5f;
    [Tooltip("How fast the burst throws a prop upwards, in m/s (less = flatter, more horizontal).")]
    [Min(0f)] public float burstUpSpeed = 3.2f;
    [Tooltip("Highest a prop can fly, in metres above the floor (it never goes into the ceiling). 0 = leave as is.")]
    [Min(0f)] public float maxBurstHeight = 0f;
    [Tooltip("Seconds a prop flies away before it can be caught.")]
    [Min(0f)] public float catchableAfter = 0.3f;
    [Tooltip("How slowly props sink once the burst is over, in m/s.")]
    [Min(0.05f)] public float fallSpeed = 0.55f;

    [Header("Trials")]

    [Tooltip("A cat attacking a pile tips its top prop over (not catchable) instead of sending it flying.")]
    public bool attacksTopple = false;

    [Tooltip("In the boss finale, each hand can flick its thumbstick to swap Single / Scatter / Buckshot.")]
    public bool finaleFireStyles = false;

    [TextArea]
    public string fireStylesBanner = "NINJA BOSS!\n<size=50%>Flick the thumbsticks to swap shots!</size>";

    [Header("Player's space")]

    [Tooltip("The ninja never appears closer than this to the player, in metres.")]
    [Min(0f)] public float ninjaMinDistance = 2.5f;
    [Tooltip("Floor area around the player that cats can't walk into (e.g. inside the counter). Width, height, depth in metres; " +
             "kept low so cats can still jump up onto the counter top. Zero width = off.")]
    public Vector3 playerSpace = new Vector3(2.2f, 0.6f, 2.2f);

    [Header("Look")]

    [Range(0.1f, 1f)]
    [Tooltip("Size of confetti and smoke puffs (1 = as made). Smaller keeps the action easy to see.")]
    public float effectScale = 0.5f;

    [Header("Cat Cafe Waves")]

    [Tooltip("The cafe manager calls out each phase on the speaker (each phase's Line), with subtitles.")]
    public bool announcer = false;
    public string welcomeLine = "welcome";
    public string closingLine = "closing";
    public string bossFedLine = "boss_fed";
    public string bossLeftLine = "boss_left";
    [Tooltip("Played before the ninja's catch and boss lines (from Resources/CafeSounds).")]
    public string ninjaSting = "ninja_sting";

    [Tooltip("Fed cats run off to the nearest hole cats come in by and go in through it, instead of launching. The holes " +
             "are modelled into the cafe: each door marker sits in the mouth of one, facing out into the room.")]
    public bool fedCatsUseHoles = false;

    [Range(0f, 180f)]
    [Tooltip("Cats only come in by doors within this many degrees of where the player faces; the others are only ways " +
             "out (for cats sent away). 180 = any door.")]
    public float entryArc = 180f;

    [Min(0)]
    [Tooltip("Coins that fly from a fed cat into a tip jar on the counter, for each treat in its order. 0 = no tip jar.")]
    public int coinsPerTreat = 0;
    [Min(0)] public int bossCoins = 8;
    [Tooltip("Optional: where the tip jar stands. Empty = on the counter beside the cat clock.")]
    public Transform tipJarSpot;
    [Tooltip("Optional: the name of an object the tip jar takes the place of (it's hidden), e.g. a bottle on the counter.")]
    public string tipJarReplaces = "";
    [Min(0.5f)] public float tipJarScale = 1f;

    [Header("Ninja and the piles (Cat Cafe Waves)")]

    [Tooltip("The ninja only goes to the prop piles, and sends a whole pile flying at once. Each pile's bar stays hidden " +
             "until the ninja first blows it up, then fills as its props are caught and put back.")]
    public bool ninjaExplodesPiles = false;
    [Tooltip("Props that hit the floor stay broken for the rest of the round; the piles' bars show what's left.")]
    public bool missedPropsStayDown = false;
    [Tooltip("The finale ninja doesn't want feeding: it just makes a mess (the hungry cats clear out for it).")]
    public bool bossMakesMess = false;
    [Tooltip("In the ninja phases a caught prop has to be dragged back over its pile and let go there (a ring marks the " +
             "spot); let go anywhere else and it drops. Off: letting go sends it home by itself.")]
    public bool dragPropsHome = true;
    [Min(0f)]
    [Tooltip("How far past a pile's table edge (metres) a carried prop still counts as over it when let go.")]
    public float propDropMargin = 0.35f;
    [Min(0f)]
    [Tooltip("The same in the Dinner Rush finale (more forgiving: there's a lot going on).")]
    public float remixPropDropMargin = 0.6f;
    [Tooltip("Each ninja phase after the first starts with the piles restocked (props lost on the floor come back).")]
    public bool restockPiles = true;
    [Min(0)]
    [Tooltip("At the end of the round each pile pays this many coins into the tip jar if it's whole (fewer if not).")]
    public int tidyBonusCoins = 0;
    [Min(0)]
    [Tooltip("Points for each whole pile at the end of the round (fewer if not).")]
    public int tidyBonusScore = 0;
    public string tidyBonusBanner = "TIDY CAFE BONUS!";
    [Range(0f, 45f)]
    [Tooltip("How far off (in degrees) a grip can be pointed and still catch a prop. 0 = leave the gravity beam's own.")]
    public float catchAimAngle = 0f;

    [Tooltip("A cat given the wrong colour pulls an angry face and storms off (no tip).")]
    public bool wrongColourLeaves = false;

    [Header("Order moments (Cat Cafe Waves)")]

    [Tooltip("Cats wander in without an order, then settle, turn to the player and order (a ding and the bubble pops " +
             "up) a few seconds later. Their patience ring then drains.")]
    public bool orderMoments = false;
    [Min(0f)] public float orderDelayMin = 2f;
    [Min(0f)] public float orderDelayMax = 4.5f;
    [Min(0f)]
    [Tooltip("Seconds a cat waits for a one-treat order once it's ordered (0 = forever)...")]
    public float patience = 10f;
    [Min(0f)]
    [Tooltip("...plus this many for each extra treat in its order.")]
    public float patiencePerExtraTreat = 3f;
    [Tooltip("A cat whose patience runs out raids the tip jar (the right treat during its wind-up calms it down); " +
             "without a tip jar it storms off with no tip.")]
    public bool impatientCatsRaid = true;
    [Min(0)]
    [Tooltip("Extra coins for each treat when a cat is served in the first half of its patience.")]
    public int quickServiceCoins = 1;
    public string quickServiceText = "Quick!";

    [Tooltip("Hungry cats make runs at the tip jar and the piles (in phases with a Raid Every time): a \"!\" and a wind-up, " +
             "then a sprint and a swat. The right treat before it leaps stops it.")]
    public bool raids = false;
    public CatRaid.Settings raid = new CatRaid.Settings();
    [Min(0)]
    [Tooltip("Coins a raiding cat knocks out of the tip jar.")]
    public int raidSpillCoins = 3;
    [Range(0.5f, 2f)]
    [Tooltip("How fast cats walk compared with their own speed (1 = as made).")]
    public float catSpeedMultiplier = 1f;

    [Tooltip("Props that can be caught pulse with a bright outline (seen through furniture too).")]
    public bool catchGlow = false;

    [Header("Kitten Stampede (Stampede phases)")]

    [Tooltip("Herds of little kittens gallop across the room in front of the player; any treat feeds one. " +
             "Both launchers fire Scatter for the phase.")]
    public bool stampedeScatter = true;
    [Range(0.3f, 1f)] public float kittenScale = 0.55f;
    [Min(1)] public int kittensPerHerdMin = 5;
    [Min(1)] public int kittensPerHerdMax = 7;
    [Min(0.5f)] public float herdGapMin = 2.2f;
    [Min(0.5f)] public float herdGapMax = 3.2f;
    [Min(0.5f)] public float kittenSpeedMin = 3.2f;
    [Min(0.5f)] public float kittenSpeedMax = 4.2f;
    [Min(0f)]
    [Tooltip("Seconds each herd runs around in front of the player (zig-zagging) before it heads off. 0 = straight across.")]
    public float herdRoamMin = 5f;
    [Min(0f)] public float herdRoamMax = 10f;
    [Min(1)]
    [Tooltip("Most kittens out at once (the Quest's limit: a new herd sends fewer if it would go over).")]
    public int maxKittens = 24;
    [Min(0.1f)]
    [Tooltip("Radius of each kitten's invisible hit area, in metres.")]
    public float kittenHitRadius = 0.35f;
    [Min(0)] public int kittenScore = 2;
    [Min(0)] public int kittenCoins = 1;
    [Range(0f, 1f)]
    [Tooltip("The chance a herd floods in through a vent up high (its lid clanks open and the kittens pour out one after " +
             "another, then tear off across the floor) instead of running in from the side.")]
    public float floodChance = 0.3f;
    [Tooltip("The stampede opens with a flood through a vent, if there's one high up that the player can see.")]
    public bool firstHerdFloods = true;

    [Header("Launcher kittens (Feed phases with a Latch Every time)")]

    [Tooltip("A kitten runs in from a door in front of the player and leaps onto a launcher: that hand can't fire until " +
             "it's shaken off. There's no stopping it: treats go straight through.")]
    public LauncherKitten.Settings launcherKitten = new LauncherKitten.Settings();
    [Tooltip("Announcer line the first time a kitten gets on a launcher. Empty = none.")]
    public string latchLine = "latch";

    [Header("Tips board")]

    [Tooltip("With a tip jar, the chalkboard shows the tips (as money) instead of the score, fits its text to its boxes, " +
             "and ends with a funny line about what the tips could buy.")]
    public TipsBoard.Settings tipsBoard = new TipsBoard.Settings();

    [Header("The shift")]

    [Tooltip("The cat clock shows the time of day through the shift (Waves) instead of seconds left.")]
    public bool shiftClock = true;
    [Range(0f, 24f)] public float shiftStartHour = 9f;
    [Range(0f, 24f)] public float shiftEndHour = 17f;
    [Tooltip("The light goes from morning to evening over the shift (Waves).")]
    public ShiftLight.Settings shiftLight = new ShiftLight.Settings();
    [Tooltip("In the lobby, a little each frame, build every letter into the fonts and decode the sounds, so none of it stutters mid-round (Quest). With a Shader Variants collection below, compile the shaders too.")]
    public bool warmUpAtStart = true;
    [Tooltip("Optional: the shaders the game draws with, compiled in the lobby so the first boss cat (or anything new) doesn't stutter on the Quest. Make it with Catastrophe Cafe > Save Shader Variants From The Last Play, after a whole round in Play.")]
    public ShaderVariantCollection shaderVariants;
    [Tooltip("The sky outside goes from morning to night over the shift (Waves; Farland Skies - Low Poly).")]
    public ShiftSky.Settings shiftSky = new ShiftSky.Settings();

    [Header("Mafia heist (feeding phases after the first)")]

    [Tooltip("The Mafia cat swaggers in and strolls for the tip jar: feed him his whole order before he gets there (a wrong " +
             "treat and he starts it over) or he scoops out a big handful of coins. Needs a tip jar, and a Boss Cat Spawner " +
             "in the scene that has the Mafia cat.")]
    public bool mafiaHeists = true;
    [Min(0)]
    [Tooltip("How many times he comes in each feeding phase (after the first).")]
    public int heistsPerPhase = 2;
    [Min(0)]
    [Tooltip("Kittens he brings with him that charge the player's launchers (one each) while he goes for the jar.")]
    public int heistGang = 2;
    public MafiaHeist.Settings heist = new MafiaHeist.Settings();
    [Tooltip("Announcer line the first time he comes in. Empty = none.")]
    public string heistLine = "mafia";
    [Tooltip("Announcer lines, each said the first time: busting the Mafia cat, him getting away with the coins, the first " +
             "catch that has to be carried home, and the tidy bonus starting. Empty = none.")]
    public string bustedLine = "busted";
    public string stoleLine = "heist";
    public string carryLine = "carry";
    public string tidyLine = "tidy";

    [Header("Spy cats (Spy phases)")]

    [Tooltip("Spy cats (phat cats) lower down on ropes from the ceiling vents (vents high up that the player can see) after a " +
             "secret recipe; looking at one and waving your arms scares it off. With no ceiling vent in view, a Spy phase is ordinary feeding instead.")]
    public SpyCat.Settings spy = new SpyCat.Settings();
    [Min(0.5f)] public float spyGapStart = 4f;
    [Min(0.5f)] public float spyGapEnd = 2.5f;
    [Range(0f, 1f)]
    [Tooltip("The spy music (Resources/CafeSounds/spy_music_intro, then spy_music_loop) all through a Spy phase. 0 = none.")]
    public float spyMusicVolume = 0.5f;
    [Range(0f, 1f)]
    [Tooltip("The cafe music's volume under the spy music (1 = no dip).")]
    public float musicUnderSpyMusic = 0.15f;

    [Header("Remix finale (Remix phases)")]

    [Tooltip("The finale mixes every wave: the ninja making a slower mess, customers ordering (the phase's Cats), launcher " +
             "kittens (its Latch Every), spy cats, the Mafia cat and a kitten flood. Never more than this many big things " +
             "at once (a heist, a kitten on a launcher, a spy, the flood); the rest wait their turn.")]
    [Min(1)] public int remixMaxBusy = 1;
    [Min(0)]
    [Tooltip("Seconds of breather in the finale after a big thing finishes before the next may start (so they come one after another, not in a pile).")]
    public float remixBreather = 6f;
    [Tooltip("Seconds between spy cats in the finale (0 = none).")]
    [Min(0)] public float remixSpyGapMin = 14f;
    [Min(0)] public float remixSpyGapMax = 20f;
    [Min(0)] public int remixHeists = 2;
    [Min(0)] public int remixHeistGang = 1;
    [Range(0f, 1f)]
    [Tooltip("How far through the finale a kitten flood pours out of a vent (0 = none).")]
    public float remixFloodAt = 0.45f;
    [Min(0.2f)]
    [Tooltip("Seconds the ninja sits between piles in the finale (slower than the catch phase: there's a lot else going on).")]
    public float remixNinjaSitMin = 7f;
    [Min(0.2f)] public float remixNinjaSitMax = 10f;

    [Header("Vents")]

    [Tooltip("Cats can come in through the cafe's vents (objects named Vent... with a ...Lid inside): the lid swings open " +
             "with a clank and out they jump.")]
    public bool catsUseVents = true;
    [Range(0f, 1f)]
    [Tooltip("Feed phases: the chance each new cat comes out of a vent instead of a door.")]
    public float ventChance = 0.35f;
    [Range(0f, 1f)]
    [Tooltip("The chance a launcher kitten comes out of a vent.")]
    public float kittenVentChance = 0.5f;
    [Range(0f, 180f)]
    [Tooltip("Only vents within this many degrees of where the player faces are used.")]
    public float ventArc = 120f;

    [Tooltip("The first catch phase starts by knocking up one prop that hangs in the air, with a hint, and the clock " +
             "waits until it's caught (or the time below runs out). Someone who knows what to do catches it straight away.")]
    public bool catchIntro = false;
    public string catchIntroHint = "Alternative trigger!";
    public string catchIntroPraise = "Nice catch!";
    [Min(1f)] public float catchIntroTimeout = 12f;

    readonly List<CatOrder> orders = new List<CatOrder>();
    PlayerInput player;
    NinjaRushCat ninja;
    SphereCollider ninjaHitbox;
    Phase current;
    float lastArrival = -100f;
    bool playing;

    [SerializeField]
    Announcer voice;
    
    TipJar tipJar;
    readonly List<CatOrder.Hole> holes = new List<CatOrder.Hole>();
    readonly List<Transform> entryDoors = new List<Transform>();
    readonly List<DefendPoint> piles = new List<DefendPoint>();
    readonly List<PropPile> propPiles = new List<PropPile>();
    float nextRaid;
    float nextLatch;
    bool windingDown;   // the end of a phase: nothing new starts
    float nextBigAt;     // the remix finale: when the next big thing may start (a breather after each)
    int lastBusy;
    bool flooding;
    SoundBite spyMusic;

    // The waves' own clock: what's scheduled (phase lengths, the next kitten, heist, spy...) runs on this. It stands still
    // while the announcer explains something (the round clock too), so nothing new starts mid-sentence
    float clock;
    bool waves;          // Cat Cafe Waves (a remix finale at the end): the round is a shift, as long as its phases
    float shiftLength;
    bool wrapUpPaused;
    bool introPaused;   // the first catch's intro stops the clock
    bool waveHeld;      // the wave waits (e.g. for the first spy to be dealt with while it's explained)
    bool holding
    {
        get
        {
            return playing && voice != null && voice.holding;
        }
    }

    // Something the announcer explains (its `line`) may start: once it's been explained, any time; until then, not while
    // she's talking (or saying that very line), so the player hears what's new before it turns up
    bool MayIntroduce(string line)
    {
        if (voice == null || string.IsNullOrEmpty(line)) return true;
        if (heardLines.Contains(line) && voice.saying != line) return true;
        return !voice.talking;
    }

    string SpyLine
    {
        get
        {
            return phases.Find(spyPhase => spyPhase.kind == Kind.Spy)?.line;
        }
    }

    IEnumerator WaitClock(float seconds)
    {
        float until = clock + seconds;
        while (clock < until) yield return null;
    }
    BossCat mafiaPrefab;
    int feedPhasesSeen;
    int ninjaPhasesSeen;
    readonly HashSet<string> heardLines = new HashSet<string>();     // said this round (not just queued)
    readonly HashSet<string> pendingLines = new HashSet<string>();   // queued, not said yet
    bool raidJarNext = true;
    bool catchIntroDone;

    void Awake()
    {
        ParticleManager.effectScale = effectScale;
        PickupManager.allowed = index => allowedPickups.Contains(index);
        Treat.cleanShots = cleanShots;
        Treat.aimAssistTurnSpeed = aimAssistTurnSpeed;
        Treat.aimAssistTarget = aimAssistAngle > 0 ? (System.Func<Treat, Vector3?>)AssistTarget : null;
        PropPile.barsWaitForNinja = ninjaExplodesPiles;
        SoundLevels.music = musicLevel;
        SoundLevels.cats = catLevel;
        SoundLevels.world = worldLevel;
    }

    // Where a treat should curve to: a hungry cat wanting its colour, or the ninja boss while it can be fed
    Vector3? AssistTarget(Treat treat)
    {
        Vector3? target = CatOrder.AssistTarget(treat, aimAssistAngle);
        if (target == null && KittenRun.running.Count > 0)
        {
            // Any kitten will do (no colours in the stampede)
            float best = aimAssistAngle;
            foreach (KittenRun kitten in KittenRun.running)
            {
                if (!kitten.isFed && kitten.feedable) Consider(kitten.aimPoint);
            }

            void Consider(Vector3 point)
            {
                float angle = Vector3.Angle(treat.velocity, point - treat.transform.position);
                if (angle <= best)
                {
                    best = angle;
                    target = point;
                }
            }
        }
        if (target == null && ninja != null && ninja.feedable && ninja.gameObject.activeInHierarchy && !ninja.isFullyFed)
        {
            Vector3 point = ninjaHitbox != null ? ninjaHitbox.transform.TransformPoint(ninjaHitbox.center) : ninja.transform.position + Vector3.up * 0.3f;
            if (Vector3.Angle(treat.velocity, point - treat.transform.position) <= aimAssistAngle) target = point;
        }
        return target;
    }

    void Start()
    {
        // The Mafia cat for heists, borrowed from the boss spawner (which may have switched itself off)
        foreach (BossCatSpawner spawner in Resources.FindObjectsOfTypeAll<BossCatSpawner>())
        {
            if (spawner.gameObject.scene.IsValid() && mafiaPrefab == null) mafiaPrefab = spawner.Prefab<MafiaBossCat>();
        }

        // How knocked props fly in this mode
        CatChaosSettings flight = CatChaosSettings.instance;
        if (flight != null)
        {
            flight.launchSpread = burstSpread;
            flight.launchTowardPlayerSpeed = burstOutwardSpeed;
            flight.launchUpSpeed = burstUpSpeed;
            if (maxBurstHeight > 0) flight.maxHeightAboveFloor = maxBurstHeight;
            flight.catchableAfter = catchableAfter;
            flight.driftFallSpeed = fallSpeed;
            flight.catchableGlow = catchGlow;
            flight.missedStayDown = missedPropsStayDown;
        }

        player = FindObjectOfType<PlayerInput>();
        // Cat Cafe Waves (a remix finale at the end): the round is as long as its phases, so a longer phase makes a longer
        // round rather than squeezing the others
        float roundLength = 0;
        foreach (Phase phase in phases) roundLength += phase.seconds;
        waves = roundLength > 0 && phases.Exists(phase => phase.kind == Kind.Remix);
        if (waves)
        {
            shiftLength = roundLength;
            GameManager.instance.SetRoundLength(roundLength);
        }
        GameManager.instance.GameStart += OnGameStart;
        FlyingProp.Grabbed += PropGrabbed;
        GameManager.instance.GameEnd += OnGameEnd;
        // (dressing only, so nothing else hangs on it)
        if (waves && shiftClock)
        {
            try { GameManager.instance.UseShiftClock(shiftStartHour, shiftEndHour); }
            catch (Exception e) { Debug.LogException(e); }
        }
        // The sky and light are up from the moment the scene loads (at the start of the shift: the clock only runs once
        // the round starts), so nothing switches when the launchers are picked up
        if (waves)
        {
            Func<float> shift = () => shiftLength > 0 ? clock / shiftLength : 0f;
            try { ShiftSky.Begin(shiftSky, shift); }
            catch (Exception e) { Debug.LogException(e); }
            try { ShiftLight.Begin(gameObject, shiftLight, shift); }
            catch (Exception e) { Debug.LogException(e); }   // dressing only: the round goes on without it
        }
        // The costs that would otherwise hit as a stutter the first time something is shown, paid in the lobby instead,
        // a little each frame once the scene is up (never here: a wait in Start froze the Quest on the splash screen)
        if (waves && warmUpAtStart)
        {
            StartCoroutine(Warmup.Run(shaderVariants));
        }
        if (ninjaPrefab != null)
        {
            ninja = Instantiate(ninjaPrefab, transform);
            ninja.gameObject.SetActive(false);
        }

        if (announcer)
        {
            if (voice == null)
            {
                voice = GetComponent<Announcer>();
                if (voice == null)
                {
                    voice = gameObject.AddComponent<Announcer>();
                }
            }
            StartCoroutine(Welcome());
        }
    }

    IEnumerator Welcome()
    {
        yield return new WaitForSeconds(1.5f);
        if (!playing) Say(welcomeLine);
    }

    // Queues a line. What's actually been heard is kept track of (a line crowded out by others isn't)
    // `hold`: the waves' clock stands still until it's been said (a wave's callout: the wave gets all its time after it)
    void Say(string line, string sting = null, bool urgent = false, bool hold = false)
    {
        if (voice == null) return;
        if (!string.IsNullOrEmpty(line)) pendingLines.Add(line);
        voice.Say(line, sting, said =>
        {
            if (string.IsNullOrEmpty(line)) return;
            pendingLines.Remove(line);
            if (said) heardLines.Add(line);
        }, urgent, hold);
    }

    // A hint about what's happening right now, said until it's been heard once: it jumps the queue, and if it still gets
    // crowded out (it waited too long), it's said the next time its moment comes round instead
    void SayOnce(string line)
    {
        if (string.IsNullOrEmpty(line) || heardLines.Contains(line) || pendingLines.Contains(line)) return;
        Say(line, null, true);
    }

    void PropGrabbed(FlyingProp prop)
    {
        if (!FlyingProp.dragHome || !playing) return;
        // The catch phase's own line already says to carry things back: no need for both
        Phase catchPhase = phases.Find(p => p.kind == Kind.Catch);
        string taught = catchPhase != null ? catchPhase.line : null;
        if (!string.IsNullOrEmpty(taught) && (heardLines.Contains(taught) || pendingLines.Contains(taught))) return;
        SayOnce(carryLine);
    }

    void HeistOver(MafiaHeist heist, MafiaHeist.Result result)
    {
        if (!playing) return;
        if (result == MafiaHeist.Result.Busted) SayOnce(bustedLine);
        else if (result == MafiaHeist.Result.Stole) SayOnce(stoleLine);
    }

    void OnDestroy()
    {
        ParticleManager.effectScale = 1f;
        PickupManager.allowed = null;
        Treat.cleanShots = false;
        Treat.aimAssistTarget = null;
        PropPile.toppleOnHit = false;
        PropPile.barsWaitForNinja = false;
        FlyingProp.Grabbed -= PropGrabbed;
        FlyingProp.dragHome = false;
        SoundLevels.music = 1f;
        SoundLevels.cats = 1f;
        SoundLevels.world = 1f;
        if (GameManager.instance != null)
        {
            GameManager.instance.GameStart -= OnGameStart;
            GameManager.instance.GameEnd -= OnGameEnd;
        }
    }

    void OnGameStart()
    {
        playing = true;

        // Liminal switches the player rig on after the scene starts, so it isn't always there at Start
        if (player == null) player = FindObjectOfType<PlayerInput>();
        if (player == null)
        {
            Debug.LogError("[Cat Cafe Rush] No player rig (PlayerInput) found when the round started.");
            return;
        }

        player.treatSpeed = treatSpeed;
        player.SetReticleColours(leftColour, rightColour);
        player.blockFire = LauncherKitten.BlocksFire;
        KeepCatsOutOfPlayerSpace();
        PickEntryDoors();
        FindVents();
        if (fedCatsUseHoles) MakeHoles();
        if (coinsPerTreat > 0) PlaceTipJar();
        if (tipJar != null) TipsBoard.Begin(tipJar, tipsBoard);
        piles.Clear();
        piles.AddRange(FindObjectsOfType<DefendPoint>());
        propPiles.Clear();
        propPiles.AddRange(FindObjectsOfType<PropPile>());

        // Catching can be made a little harder (the grip has to point closer to the prop)
        GravityBeam beam = player.GetComponent<GravityBeam>();
        if (beam != null && catchAimAngle > 0) beam.aimAngle = catchAimAngle;

        // Each hand fires its own food, with a tin of that colour hanging under the launcher
        player.LoadFood(false, leftFood);
        player.LoadFood(true, rightFood);
        if (tinPrefab != null)
        {
            SpaceCafeVisuals.TinUnderLauncher(player.Launcher(false), tinPrefab, leftColour, tinSize);
            SpaceCafeVisuals.TinUnderLauncher(player.Launcher(true), tinPrefab, rightColour, tinSize);
        }

        StartCoroutine(RunPhases());
    }

    void OnGameEnd()
    {
        playing = false;
        current = null;
        CatRaid.CancelAll();
        LauncherKitten.EndAll();
        SpyCat.EndAll();
        SoundBite.StopAll(1.5f);
        spyMusic = null;
        if (MafiaHeist.current != null) MafiaHeist.current.Cancel();
        KittenRun.EndAll();
        if (player != null) player.ForceFireStyle(null);
        if (ninja != null) ninja.Leave();
        Say(closingLine);
    }

    IEnumerator RunPhases()
    {
        for (int index = 0; index < phases.Count; index++)
        {
            Phase phase = phases[index];
            if (!playing) yield break;
            current = phase;
            nextRaid = clock + phase.raidEvery * 0.6f;
            nextLatch = clock + phase.latchEvery * 0.5f;
            if (!Feeding(phase.kind) || phase.latchEvery <= 0) LauncherKitten.EndAll();
            if (!Feeding(phase.kind) && MafiaHeist.current != null) MafiaHeist.current.Cancel();

            // Hungry cats only go for the piles in feeding phases (and only if this phase says so)
            CatMovement.targetingChanceOverride = phase.kind == Kind.Feed ? phase.attackChance : 0f;
            PropPile.toppleOnHit = attacksTopple;

            string banner = phase.banner;
            if (phase.kind == Kind.Boss && finaleFireStyles)
            {
                player.UnlockFireStyles(true);
                if (!string.IsNullOrEmpty(fireStylesBanner)) banner = fireStylesBanner;
            }
            if (!string.IsNullOrEmpty(banner)) GameManager.instance.ShowBanner(banner, bannerSeconds);
            bool ninjaPhase = phase.kind == Kind.Catch || phase.kind == Kind.Boss || phase.kind == Kind.Remix;
            // The piles' bars are only up while the ninja's about (each pops up again when it blows that pile up)
            if (!ninjaPhase) PropPile.HideBars();
            FlyingProp.dragHome = dragPropsHome && ninjaPhase;
            FlyingProp.dropMargin = phase.kind == Kind.Remix ? remixPropDropMargin : propDropMargin;
            if (ninjaPhase && ninjaPhasesSeen++ > 0 && restockPiles && FlyingProp.RestockAll() > 0)
            {
                FloatingWord.Show("Restocked!", player != null && player.eyePoint != null ? player.eyePoint.position + player.eyePoint.forward * 2f : transform.position, new Color(0.4f, 1f, 0.5f), 3f, 1.4f);
            }
            Say(phase.line, ninjaPhase ? ninjaSting : null, hold: true);

            switch (phase.kind)
            {
                case Kind.Feed:
                    // After the first feeding phase, the Mafia cat makes his move partway through
                    if (++feedPhasesSeen > 1) StartCoroutine(Heists(phase, heistsPerPhase, heistGang));
                    // Last orders: nobody new comes in for the last few seconds
                    yield return WaitClock(Mathf.Max(0, phase.seconds - lastCallSeconds));
                    windingDown = true;
                    yield return WaitClock(Mathf.Min(lastCallSeconds, phase.seconds));
                    break;

                case Kind.Catch:
                    Haptics.PulseBoth(Haptics.Strength.Strong);
                    CatRaid.CancelAll();
                    SendCatsAway();
                    if (catchIntro && !catchIntroDone) yield return CatchIntro();
                    if (ninja != null && ninja.gameObject.activeInHierarchy && ninja.holdStill)
                    {
                        // The ninja that opened the catch intro gets going
                        ninja.holdStill = false;
                    }
                    else
                    {
                        yield return WaitClock(1f);
                        SummonNinja(false);
                    }
                    yield return WaitClock(Mathf.Max(0, phase.seconds - 1f));
                    if (ninja != null) ninja.Leave();
                    break;

                case Kind.Boss when bossMakesMess:
                    // The finale is all about saving the piles: the hungry cats clear out and the ninja trashes the
                    // place until just before closing, then each pile pays out by how whole it is
                    Haptics.PulseBoth(Haptics.Strength.Huge);
                    CatRaid.CancelAll();
                    SendCatsAway();
                    yield return WaitClock(1f);
                    SummonNinja(true);
                    float messUntil = clock + Mathf.Max(0, phase.seconds - 1f - TidyBonusSeconds);
                    while (playing && clock < messUntil) yield return null;
                    if (ninja != null) ninja.Leave();
                    if (playing) yield return TidyBonus();
                    while (playing) yield return null;
                    break;

                case Kind.Remix:
                    // The finale: a bit of every wave at once, but one big thing at a time. Customers
                    // keep ordering (Update), launcher kittens come by (Latch Every), the ninja makes a slower mess,
                    // spies drop in now and then, the Mafia cat strolls in, and a kitten flood pours out once
                    Haptics.PulseBoth(Haptics.Strength.Huge);
                    yield return WaitClock(1f);
                    SummonNinja(true);
                    if (ninja != null) ninja.Configure(false, bossKnocksPerStop, remixNinjaSitMin, remixNinjaSitMax, ninjaExplodesPiles && ninja.piles.Count > 0);
                    StartCoroutine(Heists(phase, remixHeists, remixHeistGang));
                    if (remixFloodAt > 0) StartCoroutine(RemixFlood(phase));
                    StartCoroutine(RemixSpies(phase));
                    float remixUntil = clock + Mathf.Max(0, phase.seconds - 1f - TidyBonusSeconds);
                    while (playing && clock < remixUntil) yield return null;

                    // Closing up: everyone heads off, then the piles pay out
                    windingDown = true;
                    if (ninja != null) ninja.Leave();
                    if (MafiaHeist.current != null) MafiaHeist.current.Cancel();
                    SpyCat.EndAll();
                    if (playing) yield return TidyBonus();
                    while (playing) yield return null;
                    break;

                case Kind.Boss:
                    Haptics.PulseBoth(Haptics.Strength.Huge);
                    yield return WaitClock(1f);
                    SummonNinja(true);
                    float timer = 1f;
                    while (playing && timer < phase.seconds && ninja.gameObject.activeInHierarchy && !ninja.isFullyFed)
                    {
                        timer += Time.deltaTime;
                        yield return null;
                    }
                    if (playing)
                    {
                        GameManager.instance.ShowBanner(ninja.isFullyFed ? bossFedBanner : bossLeftBanner, bannerSeconds);
                        Say(ninja.isFullyFed ? bossFedLine : bossLeftLine);
                        if (ninja.isFullyFed && tipJar != null) tipJar.AddCoins(ninja.transform.position + Vector3.up * 0.3f, bossCoins);
                    }
                    // If time is left after the boss, the round finishes with ordinary feeding
                    current = new Phase { kind = Kind.Feed, cats = phase.cats + 2, shortestOrder = phase.shortestOrder, longestOrder = phase.longestOrder };
                    while (playing) yield return null;
                    break;

                case Kind.Stampede:
                    Haptics.PulseBoth(Haptics.Strength.Strong);
                    CatRaid.CancelAll();
                    SendCatsAway();
                    yield return Stampede(phase);
                    break;

                case Kind.Spy:
                    Haptics.PulseBoth(Haptics.Strength.Strong);
                    CatRaid.CancelAll();
                    SendCatsAway();
                    if (CeilingVents().Count > 0)
                    {
                        spyMusic = SoundBite.Play("Spy Music", Resources.Load<AudioClip>("CafeSounds/spy_music_loop"), spyMusicVolume,
                                                  musicUnderSpyMusic, Resources.Load<AudioClip>("CafeSounds/spy_music_intro"),
                                                  loop: true, fadeIn: 0.05f);
                        yield return SpyCats(phase);
                    }
                    else
                    {
                        Debug.LogWarning("[Cat Cafe Waves] No ceiling vent the player can see for the spy cats: ordinary feeding instead.");
                        current = new Phase { kind = Kind.Feed, seconds = phase.seconds, cats = phase.cats, shortestOrder = phase.shortestOrder, longestOrder = phase.longestOrder };
                        yield return WaitClock(Mathf.Max(0, phase.seconds - lastCallSeconds));
                        windingDown = true;
                        yield return WaitClock(Mathf.Min(lastCallSeconds, phase.seconds));
                    }
                    break;
            }

            // Time's up: what's still going on gets a moment to finish before the next phase starts
            if (index < phases.Count - 1) yield return WrapUp(phase, phases[index + 1]);
            windingDown = false;
            if (phase.kind == Kind.Stampede)
            {
                KittenRun.EndAll();
                if (player != null) player.ForceFireStyle(null);
            }
            if (phase.kind == Kind.Spy)
            {
                SpyCat.EndAll();
                if (spyMusic != null) spyMusic.Stop(2f);
                spyMusic = null;
            }
        }
    }

    // Time's up for `phase`: the round clock waits (a few seconds at most) while what's still going on finishes, so a
    // wave gets to end before the next one starts
    IEnumerator WrapUp(Phase phase, Phase next)
    {
        if (wrapUpMax <= 0) yield break;
        windingDown = true;
        bool paused = false;
        float waited = 0;
        while (playing && waited < wrapUpMax && StillGoing(phase, next))
        {
            if (!paused)
            {
                wrapUpPaused = true;
                paused = true;
            }
            waited += Time.deltaTime;
            yield return null;
        }
        if (paused) wrapUpPaused = false;
    }

    bool StillGoing(Phase phase, Phase next)
    {
        switch (phase.kind)
        {
            case Kind.Feed:
                // Into another feeding phase it all just carries on
                if (next.kind == Kind.Feed) return false;
                if (MafiaHeist.current != null || LauncherKitten.active.Count > 0) return true;
                return orders.Exists(o => o != null && o.isHungry && o.hasOrdered);
            case Kind.Catch:
            case Kind.Boss:
                foreach (FlyingProp prop in FlyingProp.airborne)
                {
                    if (prop != null && (prop.state == FlyingProp.State.Flying || prop.state == FlyingProp.State.Held)) return true;
                }
                return false;
            case Kind.Stampede:
                return KittenRun.running.Exists(kitten => kitten != null && kitten.feedable && !kitten.isFed);
            case Kind.Spy:
                return SpyCat.active.Count > 0;
        }
        return false;
    }

    // Phases where hungry cats with orders keep coming in
    bool HasOrders(Kind kind)
    {
        return kind == Kind.Feed || kind == Kind.Remix || (kind == Kind.Boss && !bossMakesMess);
    }

    // Feeding-like phases: launcher kittens and the Mafia cat carry on through them
    static bool Feeding(Kind kind)
    {
        return kind == Kind.Feed || kind == Kind.Remix;
    }

    // The remix finale's chaos budget: how many big things are going on right now
    int BusyEvents()
    {
        int busy = 0;
        if (MafiaHeist.current != null) busy++;
        if (LauncherKitten.active.Count > 0) busy++;
        if (flooding) busy++;
        busy += SpyCat.active.Count;
        return busy;
    }

    // Whether something new may start (always, outside the remix finale)
    bool RoomForMore
    {
        get
        {
            return (current == null || current.kind != Kind.Remix || (BusyEvents() < remixMaxBusy && clock >= nextBigAt));
        }
    }

    // In the finale the breather runs from when the last big thing finishes (each one plays out, then a pause, then the next)
    void UpdateBreather()
    {
        if (current.kind != Kind.Remix) return;
        int busy = BusyEvents();
        if (busy == 0 && lastBusy > 0) nextBigAt = Mathf.Max(nextBigAt, clock + remixBreather);
        lastBusy = busy;
    }

    // A big thing just started: in the finale, the next waits a breather
    void BigStarted()
    {
        if (current != null && current.kind == Kind.Remix) nextBigAt = clock + remixBreather;
    }

    // Nothing solid between the player's eye and `point` (cats, props and the player's own rig don't count)
    static bool CanSee(Vector3 eye, Vector3 point)
    {
        Vector3 to = point - eye;
        foreach (RaycastHit hit in Physics.RaycastAll(eye, to.normalized, to.magnitude - 0.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Collider other = hit.collider;
            if (other.GetComponentInParent<CatMovement>() != null || other.GetComponentInParent<FlyingProp>() != null ||
                other.GetComponentInParent<PlayerInput>() != null) continue;
            return false;
        }
        return true;
    }

    // ---------- Cat Cafe Waves: kitten stampede ----------

    // Herds of kittens gallop across the room in front of the player, from alternating sides, for the whole phase
    IEnumerator Stampede(Phase phase)
    {
        float end = clock + phase.seconds;
        if (stampedeScatter && player != null) player.ForceFireStyle(PlayerInput.FireStyle.Scatter);
        yield return WaitClock(1.5f);   // the callout lands first

        bool fromLeft = Random.value < 0.5f;
        bool first = true;
        while (playing && clock < end - 3f)
        {
            // Now and then (and first off) the herd floods in through a vent up high
            CafeVent vent = (first && firstHerdFloods) || Random.value < floodChance ? FloodVent() : null;
            first = false;
            List<Vector3> path = vent != null ? FloodPath(vent.floorSpot) : null;
            if (path != null)
            {
                StartCoroutine(Flood(vent, path));
            }
            else
            {
                path = RoamPath(fromLeft, Random.Range(herdRoamMin, Mathf.Max(herdRoamMin, herdRoamMax)));
                if (path != null) StartCoroutine(SendHerd(path));
            }
            fromLeft = !fromLeft;
            yield return WaitClock(Random.Range(herdGapMin, Mathf.Max(herdGapMin, herdGapMax)));
        }
        while (playing && clock < end) yield return null;
        // (kittens still running get to finish: see WrapUp)
    }

    IEnumerator SendHerd(List<Vector3> path)
    {
        int count = Random.Range(kittensPerHerdMin, Mathf.Max(kittensPerHerdMin, kittensPerHerdMax) + 1);
        count = Mathf.Min(count, maxKittens - KittenRun.running.Count);
        float speed = Random.Range(kittenSpeedMin, Mathf.Max(kittenSpeedMin, kittenSpeedMax));   // a herd keeps together
        for (int i = 0; i < count && playing; i++)
        {
            CatMovement cat = catPool != null ? catPool.GetRandomObjectFromPool<CatMovement>() : null;
            if (cat != null)
            {
                KittenRun.Launch(cat, path, Random.Range(-0.6f, 0.6f), speed * Random.Range(0.92f, 1.08f),
                                 kittenScale, kittenHitRadius, KittenFed);
                if (i == 0) cat.DustPuff(KittenRun.DustSize);   // a puff of dust as the herd bursts in
            }
            yield return new WaitForSeconds(Random.Range(0.12f, 0.3f));
        }
    }

    // A herd pouring out of a vent: the lid clanks open, the kittens drop out one after another and tear off along `path`
    IEnumerator Flood(CafeVent vent, List<Vector3> path)
    {
        int count = Random.Range(kittensPerHerdMin, Mathf.Max(kittensPerHerdMin, kittensPerHerdMax) + 1);
        count = Mathf.Min(count, maxKittens - KittenRun.running.Count);
        float speed = Random.Range(kittenSpeedMin, Mathf.Max(kittenSpeedMin, kittenSpeedMax));
        float spread = 0.6f;
        yield return vent.Open();
        for (int i = 0; i < count && playing; i++)
        {
            CatMovement cat = catPool != null ? catPool.GetRandomObjectFromPool<CatMovement>() : null;
            if (cat == null) break;
            float offset = Random.Range(-spread, spread);
            float kittenSpeed = speed * Random.Range(0.92f, 1.08f);
            vent.StartCoroutine(vent.Drop(cat, kittenScale, (kitten, from) =>
            {
                if (playing && current != null && (current.kind == Kind.Stampede || current.kind == Kind.Remix))
                {
                    KittenRun.Launch(kitten, path, offset, kittenSpeed, kittenScale, kittenHitRadius, KittenFed);
                }
                else
                {
                    kitten.DustPuff(KittenRun.DustSize);
                    kitten.gameObject.SetActive(false);
                }
            }));
            yield return new WaitForSeconds(Random.Range(0.14f, 0.26f));
        }
        yield return new WaitForSeconds(1.2f);   // the last ones land
        yield return vent.Close();
    }

    // ---------- Cat Cafe Waves: spy cats ----------

    // Vents high up (in the ceiling) that the player can see: where spies come down from
    List<CafeVent> CeilingVents()
    {
        return vents.FindAll(vent => vent != null && vent.dropHeight > 1.5f);
    }

    // Spies through the phase, a new one every so often (quicker as it goes on), however many are still dangling
    IEnumerator SpyCats(Phase phase)
    {
        float start = clock;
        float end = start + phase.seconds;
        // The first spy drops in straight away and hangs there while it's explained (a player who knows what to do can
        // wave it off at once). The wave waits until it's been dealt with and the announcer has finished.
        yield return new WaitForSeconds(0.8f);
        SpyCat first = SendSpy();
        if (first != null)
        {
            first.holdBack = () => voice != null && voice.talking;
            waveHeld = true;
            while (playing && (!first.isFinished || (voice != null && voice.talking))) yield return null;
            waveHeld = false;
            yield return WaitClock(Mathf.Lerp(spyGapStart, spyGapEnd, 0f));
        }
        while (playing && clock < end - (spy.lowerSeconds + spy.approachSeconds) * 0.6f)
        {
            // Each spy on its own timer: one still dangling doesn't hold up the next (as many as there are free vents)
            float progress = (clock - start) / phase.seconds;
            SendSpy();
            float gap = Mathf.Lerp(spyGapStart, spyGapEnd, progress);
            yield return WaitClock(gap * Random.Range(0.8f, 1.2f));
        }
        while (playing && clock < end) yield return null;
        // (any still dangling get to finish: see WrapUp)
    }

    // The finale's spies, now and then (when there's room for one)
    IEnumerator RemixSpies(Phase phase)
    {
        if (remixSpyGapMax <= 0 || CeilingVents().Count == 0) yield break;
        float end = clock + phase.seconds;
        while (playing && current == phase && !windingDown && clock < end - (spy.lowerSeconds + spy.approachSeconds))
        {
            yield return WaitClock(Random.Range(remixSpyGapMin, Mathf.Max(remixSpyGapMin, remixSpyGapMax)));
            while (playing && current == phase && !windingDown && (!RoomForMore || !MayIntroduce(SpyLine))) yield return null;
            if (!playing || current != phase || windingDown) yield break;
            if (SendSpy() != null) BigStarted();
        }
    }

    // One spy down a free ceiling vent (null if none could go)
    SpyCat SendSpy()
    {
        List<CafeVent> free = CeilingVents().FindAll(ceiling => !ceiling.busy);
        if (free.Count == 0) return null;
        CafeVent vent = free[Random.Range(0, free.Count)];
        CatMovement cat = SpyToSend();
        if (cat == null) return null;
        return SpyCat.Begin(cat, vent, player, tipJar, spy, null);
    }

    // The spy in its gear (Resources/SpyCat/Spy Phat Cat, a few kept ready); without it, a phat cat from the pool
    const string SpyPrefabPath = "SpyCat/Spy Phat Cat";
    GameObject spyPrefab;
    bool lookedForSpyPrefab;
    readonly List<CatMovement> spies = new List<CatMovement>();

    CatMovement SpyToSend()
    {
        if (!lookedForSpyPrefab)
        {
            lookedForSpyPrefab = true;
            spyPrefab = Resources.Load<GameObject>(SpyPrefabPath);
        }
        if (spyPrefab != null)
        {
            foreach (CatMovement ready in spies)
            {
                if (ready != null && !ready.gameObject.activeSelf) return ready;
            }
            GameObject made = Instantiate(spyPrefab, transform);
            made.SetActive(false);
            CatMovement spyCat = made.GetComponent<CatMovement>();
            if (spyCat != null)
            {
                spies.Add(spyCat);
                return spyCat;
            }
        }
        if (catPool == null) return null;
        int[] phats = catPool.VariationsNamed("Phat");
        CatMovement cat = phats.Length > 0 ? catPool.GetRandomObjectFromPoolFromGivenVariations<CatMovement>(phats) : null;
        return cat != null ? cat : catPool.GetRandomObjectFromPool<CatMovement>();
    }

    // The remix finale's kitten flood, partway through (when there's room for it)
    IEnumerator RemixFlood(Phase phase)
    {
        float at = clock + phase.seconds * remixFloodAt;
        while (playing && current == phase && (clock < at || !RoomForMore)) yield return null;
        if (!playing || current != phase || windingDown) yield break;
        CafeVent vent = FloodVent();
        List<Vector3> path = vent != null ? FloodPath(vent.floorSpot) : null;
        if (path == null) yield break;
        BigStarted();
        flooding = true;
        yield return Flood(vent, path);
        flooding = false;
    }

    // A vent for a flood: the highest free one (kittens raining down from the ceiling) that drops to the floor; null if
    // none is free
    CafeVent FloodVent()
    {
        CafeVent best = null;
        foreach (CafeVent vent in vents)
        {
            if (vent == null || vent.busy || !vent.landsOnFloor) continue;
            if (best == null || vent.dropHeight > best.dropHeight) best = vent;
        }
        return best;
    }

    // Where a flood goes from the spot under its vent: off across the floor in front of the player to one side
    List<Vector3> FloodPath(Vector3 from)
    {
        if (player == null) return null;
        NavMeshPath navPath = new NavMeshPath();
        Vector3 at = player.transform.position;
        Vector3 forward = Flat(player.transform.forward);
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector3 away = at + forward * Random.Range(2.5f, 5f) + right * Random.Range(3.5f, 5.5f) * (Random.value < 0.5f ? 1f : -1f);
            if (!NavMesh.SamplePosition(away, out NavMeshHit finish, 2f, NavMesh.AllAreas)) continue;
            if (Mathf.Abs(finish.position.y - from.y) > 0.3f) continue;
            if (!NavMesh.CalculatePath(from, finish.position, NavMesh.AllAreas, navPath) ||
                navPath.status != NavMeshPathStatus.PathComplete) continue;
            float length = 0;
            for (int i = 1; i < navPath.corners.Length; i++) length += Vector3.Distance(navPath.corners[i - 1], navPath.corners[i]);
            if (length > Vector3.Distance(from, finish.position) * 2f + 2f) continue;   // no big detours round the room
            return new List<Vector3>(navPath.corners);
        }
        return null;
    }

    void KittenFed(KittenRun kitten)
    {
        Vector3 at = kitten.aimPoint;
        GameManager.instance.FeedCat(kittenScore, at);
        if (tipJar != null && kittenCoins > 0) tipJar.AddCoins(at, kittenCoins);
    }

    // A walkable run across the floor in front of the player, from one side to the other (around furniture, never
    // through the player's space), or null if none could be found
    List<Vector3> HerdPath(bool fromLeft)
    {
        if (player == null) return null;
        Transform eye = player.eyePoint;
        Vector3 head = eye != null ? eye.position : player.transform.position;
        Vector3 forward = Flat(player.transform.forward);
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float floorY = player.transform.position.y;
        NavMeshPath navPath = new NavMeshPath();

        for (int attempt = 0; attempt < 8; attempt++)
        {
            float depth = Random.Range(2.5f, 6.5f);
            float width = Random.Range(3.5f, 5.5f);
            Vector3 centre = head + forward * depth;
            centre.y = floorY;
            Vector3 side = right * width * (fromLeft ? 1f : -1f);
            if (!NavMesh.SamplePosition(centre - side, out NavMeshHit start, 2f, NavMesh.AllAreas)) continue;
            if (!NavMesh.SamplePosition(centre + side, out NavMeshHit finish, 2f, NavMesh.AllAreas)) continue;
            // On the floor, not up on a table
            if (Mathf.Abs(start.position.y - floorY) > 0.3f || Mathf.Abs(finish.position.y - floorY) > 0.3f) continue;
            if (!NavMesh.CalculatePath(start.position, finish.position, NavMesh.AllAreas, navPath) ||
                navPath.status != NavMeshPathStatus.PathComplete) continue;

            float length = 0;
            for (int i = 1; i < navPath.corners.Length; i++) length += Vector3.Distance(navPath.corners[i - 1], navPath.corners[i]);
            float straight = Vector3.Distance(start.position, finish.position);
            // Long enough to see, and not a big detour round the room
            if (straight < 4f || length > straight * 1.8f) continue;
            return new List<Vector3>(navPath.corners);
        }
        return null;
    }

    // A herd's run that zig-zags about in front of the player for about `seconds` (the time to chase them down) before
    // heading off out the other side: in from one side, from spot to spot across the floor in front of them, then away.
    // Just across if no spots can be found.
    List<Vector3> RoamPath(bool fromLeft, float seconds)
    {
        List<Vector3> across = HerdPath(fromLeft);
        if (across == null || seconds <= 0 || player == null) return across;
        Transform eye = player.eyePoint;
        Vector3 head = eye != null ? eye.position : player.transform.position;
        Vector3 forward = Flat(player.transform.forward);
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float floorY = across[0].y;
        float wanted = seconds * (kittenSpeedMin + kittenSpeedMax) * 0.5f;

        List<Vector3> route = new List<Vector3> { across[0] };
        Vector3 at = across[0];
        float length = 0;
        NavMeshPath leg = new NavMeshPath();
        for (int tries = 0; tries < 40 && length < wanted; tries++)
        {
            Vector3 spot = head + forward * Random.Range(2.2f, 6f) + right * Random.Range(-3.5f, 3.5f);
            spot.y = floorY;
            if (!NavMesh.SamplePosition(spot, out NavMeshHit floor, 1f, NavMesh.AllAreas) || Mathf.Abs(floor.position.y - floorY) > 0.3f) continue;
            float straight = Flat(floor.position - at).magnitude;
            if (straight < 1.8f) continue;   // a proper dash, not a shuffle
            if (!NavMesh.CalculatePath(at, floor.position, NavMesh.AllAreas, leg) || leg.status != NavMeshPathStatus.PathComplete) continue;
            float legLength = 0;
            for (int i = 1; i < leg.corners.Length; i++) legLength += Vector3.Distance(leg.corners[i - 1], leg.corners[i]);
            if (legLength > straight * 1.6f + 0.5f) continue;   // not the long way round the furniture
            for (int i = 1; i < leg.corners.Length; i++) route.Add(leg.corners[i]);
            length += legLength;
            at = floor.position;
        }
        if (route.Count < 2) return across;
        // ...then away out the far side
        Vector3 exit = across[across.Count - 1];
        if (!NavMesh.CalculatePath(at, exit, NavMesh.AllAreas, leg) || leg.status != NavMeshPathStatus.PathComplete) return across;
        for (int i = 1; i < leg.corners.Length; i++) route.Add(leg.corners[i]);
        return route;
    }

    void Update()
    {
        if (playing)
        {
            if (!holding && !introPaused && !waveHeld) clock += Time.deltaTime;   // in step with the round clock
            GameManager.instance.clockPaused = wrapUpPaused || introPaused || holding || waveHeld;
        }
        if (!playing || current == null) return;
        if (raids) UpdateRaids();
        UpdateBreather();
        if (windingDown) return;
        if (Feeding(current.kind) && current.latchEvery > 0) UpdateLatches();
        if (!HasOrders(current.kind)) return;
        if (Time.time - lastArrival < arrivalGap) return;

        orders.RemoveAll(o => o == null || !o.isHungry);
        if (orders.Count + droppingIn < current.cats) SendInCat();
    }

    void SendInCat()
    {
        if (catPool == null) return;
        // Now and then through a vent
        CafeVent vent = Random.value < ventChance ? FreeVent() : null;
        List<Transform> ways = entryDoors.Count > 0 ? entryDoors : doors;
        if (vent == null && ways.Count == 0) return;
        CatMovement cat = catPool.GetRandomObjectFromPool<CatMovement>();
        if (cat == null) return;
        lastArrival = Time.time;

        if (vent != null)
        {
            droppingIn++;
            vent.StartCoroutine(vent.Release(cat, 1f, OutOfVent));
            return;
        }
        Transform door = ways[Random.Range(0, ways.Count)];
        Vector3 position = door.position;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) position = hit.position;
        Arrive(cat, door, position);
    }

    // A cat that came out of a vent has landed: in it comes (unless its chance has passed)
    void OutOfVent(CatMovement cat, CafeVent vent)
    {
        droppingIn = Mathf.Max(0, droppingIn - 1);
        if (!playing || current == null || !HasOrders(current.kind))
        {
            cat.DustPuff(1f);
            cat.gameObject.SetActive(false);
            return;
        }
        Arrive(cat, vent.exit, vent.floorSpot);
    }

    // A customer walks in at `position` (facing the way `door` does) and is given its order
    void Arrive(CatMovement cat, Transform door, Vector3 position)
    {
        cat.gameObject.SetActive(true);
        cat.Initialise(door, position);

        int length = Random.Range(current.shortestOrder, current.longestOrder + 1);
        List<CatOrder.Food> foods = new List<CatOrder.Food>();
        for (int i = 0; i < length; i++)
        {
            bool left = Random.value < 0.5f;
            foods.Add(new CatOrder.Food
            {
                variation = left ? leftFood : rightFood,
                icon = new OrderBubble.Item(left ? leftTreatPrefab : rightTreatPrefab, left ? leftColour : rightColour)
            });
        }
        CatOrder order = CatOrder.Give(cat, foods, bubbleScale, cleanShots ? hitboxGenerosity : 0f, catSpeedMultiplier);
        order.served = Served;
        order.holeFor = null;
        if (fedCatsUseHoles && holes.Count > 0)
        {
            order.holeFor = NearestHole;
        }
        order.orderSpotFor = orderMoments ? OrderSpot : (Func<CatOrder, Vector3?>)null;
        order.wrongTreat = WrongTreat;
        order.patienceOut = PatienceOut;
        if (orderMoments)
        {
            order.OrderLater(Random.Range(orderDelayMin, Mathf.Max(orderDelayMin, orderDelayMax)),
                             patience > 0 ? patience + patiencePerExtraTreat * (length - 1) : 0f);
        }
        orders.Add(order);
        lastArrival = Time.time;
    }

    // ---------- Cat Cafe Waves: the Mafia heist ----------

    // The Mafia cat's heists in a feeding phase, spread through its middle stretch, one at a time
    IEnumerator Heists(Phase phase, int count, int gang)
    {
        if (!mafiaHeists || count <= 0 || mafiaPrefab == null) yield break;
        float start = clock;
        for (int i = 0; i < count; i++)
        {
            float slot = phase.seconds / count;
            float at = start + slot * i + slot * Random.Range(0.2f, 0.45f);
            while (playing && current == phase && (clock < at || MafiaHeist.current != null || !RoomForMore || !MayIntroduce(heistLine))) yield return null;
            if (!playing || current != phase || tipJar == null || windingDown) yield break;
            StartHeist(gang);
        }
    }

    void StartHeist(int gang)
    {
        List<Transform> ways = entryDoors.Count > 0 ? entryDoors : doors;
        if (ways.Count == 0) return;
        Transform door = ways[Random.Range(0, ways.Count)];
        Vector3 position = door.position;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) position = hit.position;

        List<CatOrder.Food> foods = new List<CatOrder.Food>();
        for (int i = 0; i < heist.treats; i++)
        {
            bool left = Random.value < 0.5f;
            foods.Add(new CatOrder.Food
            {
                variation = left ? leftFood : rightFood,
                icon = new OrderBubble.Item(left ? leftTreatPrefab : rightTreatPrefab, left ? leftColour : rightColour)
            });
        }
        MafiaHeist started = MafiaHeist.Begin(mafiaPrefab, transform, door, position, tipJar, foods, bubbleScale,
                                              cleanShots ? hitboxGenerosity : 0f, heist, HeistOver);
        if (started != null && gang > 0) StartCoroutine(MafiaGang(position, gang));
        if (started != null)
        {
            SayOnce(heistLine);
            BigStarted();
        }
    }

    // The Mafia cat's gang: kittens that burst in with him and go for the player's launchers
    IEnumerator MafiaGang(Vector3 at, int gang)
    {
        yield return new WaitForSeconds(0.6f);
        for (int i = 0; i < gang && playing; i++)
        {
            List<Vector3> path = LatchPath(at);
            CatMovement cat = path != null && catPool != null ? catPool.GetRandomObjectFromPool<CatMovement>() : null;
            if (cat != null)
            {
                LauncherKitten.Send(cat, path, player, launcherKitten, KittenLatched, (kitten, result) => { });
                cat.DustPuff(KittenRun.DustSize);
            }
            yield return new WaitForSeconds(0.35f);
        }
    }

    // ---------- Cat Cafe Waves: vents ----------

    readonly List<CafeVent> vents = new List<CafeVent>();
    int droppingIn;   // cats on their way out of a vent

    // Every vent with a lid ("Vent..." objects with a "...Lid" inside) that has floor to land on
    void FindVents()
    {
        vents.Clear();
        if (player == null) return;
        Transform eye = player.eyePoint;
        Vector3 viewer = eye != null ? eye.position : player.transform.position + Vector3.up * 1.5f;
        float floorY = player.transform.position.y;
        if (NavMesh.SamplePosition(player.transform.position, out NavMeshHit floor, 2f, NavMesh.AllAreas)) floorY = floor.position.y;
        // A spot on the floor in front of the player that cats coming out of a vent must be able to reach
        Vector3 forward = Flat(player.transform.forward);
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        Vector3 room = player.transform.position + forward * 2.5f;
        if (NavMesh.SamplePosition(room, out NavMeshHit roomFloor, 2f, NavMesh.AllAreas)) room = roomFloor.position;
        foreach (MeshFilter lid in FindObjectsOfType<MeshFilter>())
        {
            if (!lid.name.ToLowerInvariant().Contains("lid")) continue;
            // The vent it belongs to: the nearest thing above it called Vent...
            Transform vent = lid.transform.parent;
            while (vent != null && !(vent.name.ToLowerInvariant().StartsWith("vent") && !vent.name.ToLowerInvariant().Contains("lid"))) vent = vent.parent;
            if (vent == null) continue;
            CafeVent made = CafeVent.Make(vent, lid.transform, viewer, floorY, room);
            if (made == null || vents.Contains(made)) continue;
            // Only vents the player can see (a cat out of one hidden behind the bar would seem to come from nowhere). A vent
            // set into the ceiling counts if the space just below it can be seen (its opening is up in the ceiling itself)
            Vector3 below = Vector3.Lerp(made.opening, made.floorSpot + Vector3.up * 0.5f, 0.3f);
            if (CanSee(viewer, made.opening) || CanSee(viewer, below)) vents.Add(made);
        }
        Debug.Log($"[Cat Cafe Waves] Cats can come out of {vents.Count} vent(s).");
    }

    // A vent nobody's coming out of, within reach of the player's view (null if none); `floorOnly` leaves out vents whose
    // cats land up on something
    CafeVent FreeVent(bool floorOnly = false)
    {
        if (!catsUseVents || vents.Count == 0 || player == null) return null;
        Vector3 at = player.transform.position;
        Vector3 forward = Flat(player.transform.forward);
        List<CafeVent> free = vents.FindAll(vent =>
            vent != null && !vent.busy && (!floorOnly || vent.landsOnFloor) &&
            (forward.sqrMagnitude < 0.001f || Vector3.Angle(forward, Flat(vent.floorSpot - at)) <= ventArc));
        return free.Count > 0 ? free[Random.Range(0, free.Count)] : null;
    }

    // ---------- Cat Cafe Waves: ordering where the player can see ----------

    // Floor spots in front of the player with a clear view of a cat standing there (found once, when first needed)
    readonly List<Vector3> orderSpots = new List<Vector3>();
    readonly Dictionary<CatOrder, Vector3> spotTaken = new Dictionary<CatOrder, Vector3>();
    bool lookedForOrderSpots;

    // Where a cat about to order should go first: nowhere (null) if the player can already see it well; otherwise the
    // nearest free spot they can
    Vector3? OrderSpot(CatOrder order)
    {
        Transform eye = player != null ? player.eyePoint : null;
        if (eye == null) return null;
        if (InGoodView(eye, order.transform.position)) return null;
        if (!lookedForOrderSpots)
        {
            lookedForOrderSpots = true;
            FindOrderSpots(eye);
        }

        // Not one another cat is standing at or heading for
        List<CatOrder> gone = new List<CatOrder>();
        foreach (KeyValuePair<CatOrder, Vector3> taken in spotTaken)
        {
            if (taken.Key == null || !taken.Key.isHungry) gone.Add(taken.Key);
        }
        foreach (CatOrder done in gone) spotTaken.Remove(done);

        Vector3? best = null;
        float bestDistance = float.MaxValue;
        foreach (Vector3 spot in orderSpots)
        {
            bool free = true;
            foreach (KeyValuePair<CatOrder, Vector3> taken in spotTaken)
            {
                if (taken.Key != order && Flat(taken.Value - spot).magnitude < 0.9f) free = false;
            }
            foreach (CatOrder other in orders)
            {
                if (other != null && other != order && other.isHungry && Flat(other.transform.position - spot).magnitude < 0.9f) free = false;
            }
            float distance = Flat(spot - order.transform.position).magnitude;
            if (free && distance < bestDistance)
            {
                bestDistance = distance;
                best = spot;
            }
        }
        if (best != null) spotTaken[order] = best.Value;
        return best;
    }

    // In front of the player, not too near or far, and nothing solid hiding it
    bool InGoodView(Transform eye, Vector3 at)
    {
        Vector3 to = Flat(at - eye.position);
        float distance = to.magnitude;
        if (distance < 1.8f || distance > 8f) return false;
        Vector3 forward = Flat(player.transform.forward);
        if (forward.sqrMagnitude > 0.001f && Vector3.Angle(forward, to) > 60f) return false;
        return CanSee(eye.position, at + Vector3.up * 0.25f);
    }

    void FindOrderSpots(Transform eye)
    {
        Vector3 at = player.transform.position;
        Vector3 forward = Flat(player.transform.forward);
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        for (float distance = 2.5f; distance <= 6.5f; distance += 0.8f)
        {
            for (float angle = -50f; angle <= 50f; angle += 10f)
            {
                Vector3 point = at + Quaternion.Euler(0, angle, 0) * forward * distance;
                if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 0.4f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(hit.position.y - at.y) > 0.3f) continue;   // on the floor, not up on the furniture
                if (orderSpots.Exists(spot => Flat(spot - hit.position).magnitude < 0.7f)) continue;
                if (InGoodView(eye, hit.position)) orderSpots.Add(hit.position);
            }
        }
    }

    void Served(CatOrder order)
    {
        if (tipJar == null || coinsPerTreat <= 0) return;
        int coins = coinsPerTreat * order.orderLength;
        // Quick service (in the first half of its patience) tips better
        if (orderMoments && quickServiceCoins > 0 && order.patienceFraction > 0.5f)
        {
            coins += quickServiceCoins * order.orderLength;
            FloatingWord.Show(quickServiceText, order.aboveBubble, new Color(1f, 0.85f, 0.2f));
        }
        tipJar.AddCoins(order.transform.position + Vector3.up * 0.3f, coins);
    }

    // ---------- Cat Cafe Waves: launcher kittens ----------

    // Every so often (one at a time) a kitten bolts in from a door the player can see and leaps onto a launcher
    void UpdateLatches()
    {
        if (LauncherKitten.active.Count > 0 || clock < nextLatch || !RoomForMore || !MayIntroduce(latchLine)) return;
        nextLatch = clock + 1f;   // couldn't send one: try again in a moment
        if (catPool == null) return;

        // Out of a vent sometimes: it drops down, then makes its run
        CafeVent vent = Random.value < kittenVentChance ? FreeVent(true) : null;
        List<Vector3> path = vent != null ? LatchPath(vent.floorSpot) : null;
        if (path != null)
        {
            CatMovement fromVent = catPool.GetRandomObjectFromPool<CatMovement>();
            if (fromVent == null) return;
            nextLatch = float.MaxValue;   // set again when it's over
            BigStarted();
            vent.StartCoroutine(vent.Release(fromVent, launcherKitten.runScale, (cat, landedFrom) =>
            {
                if (playing && current != null && Feeding(current.kind) && current.latchEvery > 0)
                {
                    LauncherKitten.Send(cat, path, player, launcherKitten, KittenLatched, LatchOver);
                }
                else
                {
                    cat.DustPuff(KittenRun.DustSize);
                    cat.gameObject.SetActive(false);
                    LatchOver(null, LauncherKitten.Result.Cancelled);
                }
            }));
            return;
        }

        path = LatchPath(null);
        if (path == null) return;
        CatMovement kitten = catPool.GetRandomObjectFromPool<CatMovement>();
        if (kitten == null) return;
        LauncherKitten.Send(kitten, path, player, launcherKitten, KittenLatched, LatchOver);
        kitten.DustPuff(KittenRun.DustSize);
        nextLatch = float.MaxValue;   // set again when it's over
        BigStarted();
    }

    void KittenLatched(LauncherKitten kitten)
    {
        SayOnce(latchLine);
    }

    void LatchOver(LauncherKitten kitten, LauncherKitten.Result result)
    {
        if (current != null) nextLatch = clock + current.latchEvery * Random.Range(0.8f, 1.2f);
    }

    // From `from` (or a door in front of the player) to the floor just outside the player's space
    List<Vector3> LatchPath(Vector3? from)
    {
        List<Transform> ways = entryDoors.Count > 0 ? entryDoors : doors;
        if (player == null || (from == null && ways.Count == 0)) return null;
        Vector3 at = player.transform.position;
        Vector3 forward = Flat(player.transform.forward);
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float front = Mathf.Max(0.5f, playerSpace.z * 0.5f) + 0.4f;
        NavMeshPath navPath = new NavMeshPath();
        for (int attempt = 0; attempt < 6; attempt++)
        {
            Transform door = from == null ? ways[Random.Range(0, ways.Count)] : null;
            if (from == null && door == null) continue;
            Vector3 spot = at + forward * front + right * Random.Range(-0.5f, 0.5f);
            if (!NavMesh.SamplePosition(spot, out NavMeshHit end, 1.2f, NavMesh.AllAreas)) continue;
            if (Mathf.Abs(end.position.y - at.y) > 0.3f) continue;   // on the floor, not up on something
            if (!NavMesh.SamplePosition(from ?? door.position, out NavMeshHit start, 1.5f, NavMesh.AllAreas)) continue;
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, navPath) ||
                navPath.status != NavMeshPathStatus.PathComplete) continue;
            return new List<Vector3>(navPath.corners);
        }
        return null;
    }

    // ---------- Cat Cafe Waves: the catch intro ----------

    // The first catch of the run, taught by doing: the clock stops, one prop in front of the player pops up and hangs
    // in the air with a hint by it, and play carries on once it's caught (or after a while anyway, no fail)
    IEnumerator CatchIntro()
    {
        catchIntroDone = true;
        introPaused = true;
        yield return new WaitForSeconds(1.2f);

        Transform eye = player != null ? player.eyePoint : null;
        Vector3 viewer = eye != null ? eye.position : (player != null ? player.transform.position : transform.position);
        FlyingProp prop = PropInView(eye);

        // The ninja appears right beside it and flicks it up: its opening move (then it waits until it's caught)
        if (prop != null && ninja != null)
        {
            Vector3 towards = Flat(viewer - prop.centre);
            towards = towards.sqrMagnitude > 0.001f ? towards.normalized : Vector3.forward;
            if (NavMesh.SamplePosition(prop.centre + towards * 0.4f, out NavMeshHit beside, 1f, NavMesh.AllAreas))
            {
                SummonNinja(false, beside.position);
                yield return new WaitForSeconds(0.6f);
                if (ninja.gameObject.activeInHierarchy) ninja.Swipe();
                yield return new WaitForSeconds(0.15f);
            }
        }

        if (prop != null && prop.Knock(new Vector3(viewer.x, prop.transform.position.y, viewer.z), true))
        {
            GameObject hint = FloatingWord.Make(catchIntroHint, new Color(1f, 0.85f, 0.2f), 2.2f);
            float timer = 0;
            float nextBuzz = 0;
            while (playing && timer < catchIntroTimeout && prop.state == FlyingProp.State.Flying)
            {
                timer += Time.deltaTime;
                hint.transform.position = prop.centre + Vector3.up * (0.35f + Mathf.Abs(Mathf.Sin(timer * 4f)) * 0.06f);
                FloatingWord.FacePlayer(hint.transform);
                // A nudge in both hands, the hands that do the catching
                if (timer >= nextBuzz)
                {
                    Haptics.PulseBoth(Haptics.Strength.Light);
                    nextBuzz = timer + 1.5f;
                }
                yield return null;
            }
            Destroy(hint);
            if (prop.state == FlyingProp.State.Held || prop.state == FlyingProp.State.Returning)
            {
                FloatingWord.Show(catchIntroPraise, prop.centre + Vector3.up * 0.3f, new Color(0.4f, 1f, 0.5f));
                yield return new WaitForSeconds(0.6f);
            }
        }
        introPaused = false;
    }

    // A prop at home that's in front of the player and not too far: the one nearest the middle of their view
    FlyingProp PropInView(Transform eye)
    {
        if (eye == null) return null;
        // In front of where the player stands to play (the rig's forward, not wherever they happen to be looking), and
        // off a pile on a table: never a loose prop like a plant on the floor beside them
        Vector3 ahead = player != null ? Flat(player.transform.forward) : Flat(eye.forward);
        if (ahead.sqrMagnitude < 0.001f) ahead = Flat(eye.forward);
        FlyingProp best = null;
        float bestAngle = 50f;
        foreach (FlyingProp prop in FlyingProp.all)
        {
            if (prop == null || prop.state != FlyingProp.State.Home || prop.isLoose) continue;
            Vector3 to = prop.centre - eye.position;
            float distance = Flat(to).magnitude;
            if (distance < 1.2f || distance > 5f) continue;
            float angle = Vector3.Angle(ahead, Flat(to));
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = prop;
            }
        }
        return best;
    }

    // ---------- Cat Cafe Waves: raids ----------

    // Every so often (one at a time) a hungry cat the player can see makes a run at the tip jar or a pile
    void UpdateRaids()
    {
        if (current.raidEvery <= 0 || CatRaid.active.Count > 0 || clock < nextRaid) return;
        nextRaid = clock + 1f;   // nobody could go: try again in a moment

        // With a tip jar, raids are all about the jar (the piles are the ninja's); otherwise they take turns
        bool jarFirst = tipJar != null || raidJarNext;
        for (int attempt = 0; attempt < (tipJar != null ? 1 : 2); attempt++)
        {
            bool useJar = attempt == 0 ? jarFirst : !jarFirst;
            CatRaid.Target target = useJar ? JarTarget() : PileTarget();
            if (target == null) continue;
            foreach (CatOrder raider in Raiders(target.position))
            {
                if (CatRaid.Begin(raider, target, raid, RaidOver) != null)
                {
                    raidJarNext = !useJar;
                    nextRaid = float.MaxValue;   // set again when this raid is over
                    return;
                }
            }
        }
    }

    void RaidOver(CatRaid finished, CatRaid.Result result)
    {
        if (current != null) nextRaid = clock + current.raidEvery * Random.Range(0.8f, 1.2f);
    }

    CatRaid.Target JarTarget()
    {
        if (tipJar == null) return null;
        TipJar jar = tipJar;
        return new CatRaid.Target { position = jar.transform.position, swat = from => jar.Spill(from, raidSpillCoins) };
    }

    CatRaid.Target PileTarget()
    {
        List<DefendPoint> standing = piles.FindAll(p => p != null && p.isActiveAndEnabled);
        if (standing.Count == 0) return null;
        DefendPoint pile = standing[Random.Range(0, standing.Count)];
        return new CatRaid.Target { position = pile.position, swat = from => pile.GetHit() };
    }

    // Hungry cats far enough from the target for the run to be seen, the ones in the player's view first
    List<CatOrder> Raiders(Vector3 target)
    {
        Transform eye = player != null ? player.eyePoint : null;
        List<CatOrder> inView = new List<CatOrder>();
        List<CatOrder> others = new List<CatOrder>();
        foreach (CatOrder order in orders)
        {
            if (order == null || !order.isHungry || order.busy || !order.hasOrdered) continue;
            Vector3 gap = target - order.transform.position;
            gap.y = 0;
            if (gap.magnitude < 2.5f) continue;
            bool seen = eye == null || Vector3.Angle(Flat(eye.forward), Flat(order.transform.position - eye.position)) < 60f;
            (seen ? inView : others).Add(order);
        }
        Shuffle(inView);
        Shuffle(others);
        inView.AddRange(others);
        return inView;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0;
        return v;
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T swap = list[i];
            list[i] = list[j];
            list[j] = swap;
        }
    }

    // ---------- Cat Cafe Waves: holes and the tip jar ----------

    CatOrder.Hole? NearestHole(Vector3 from)
    {
        CatOrder.Hole? best = null;
        foreach (CatOrder.Hole hole in holes)
        {
            if (best == null || (hole.outside - from).sqrMagnitude < (best.Value.outside - from).sqrMagnitude)
            {
                best = hole;
            }
        }
        return best;
    }

    // The doors cats come in by: the ones in front of the player (so they arrive where the player is looking)
    void PickEntryDoors()
    {
        entryDoors.Clear();
        Vector3 facing = player != null ? Flat(player.transform.forward) : Vector3.zero;
        foreach (Transform door in doors)
        {
            if (door == null) continue;
            Vector3 to = player != null ? Flat(door.position - player.transform.position) : Vector3.zero;
            if (entryArc >= 180f || facing.sqrMagnitude < 0.001f || Vector3.Angle(facing, to) <= entryArc) entryDoors.Add(door);
        }
        if (entryDoors.Count == 0) entryDoors.AddRange(doors.FindAll(door => door != null));
    }

    // The holes at the doors cats come in by. They are modelled into the cafe (the couches), with a door marker in the
    // mouth of each, facing out into the room: so a hole is the floor just outside its marker and the way in behind it
    void MakeHoles()
    {
        holes.Clear();
        foreach (Transform door in entryDoors)
        {
            if (door == null)
            {
                continue;
            }
            CatOrder.Hole hole = new CatOrder.Hole();
            hole.outside = door.position;
            if (NavMesh.SamplePosition(door.position, out NavMeshHit floor, 1.5f, NavMesh.AllAreas))
            {
                hole.outside = floor.position;
            }
            hole.inward = -Flat(door.forward);
            if (hole.inward.sqrMagnitude > 0.001f)
            {
                hole.inward = hole.inward.normalized;
            }
            holes.Add(hole);
        }
    }

    // On the counter beside the cat clock (it stands on the counter), on whichever side is nearer the player's front
    // right, so it's in view but out of the way of shots at the room. A Tip Jar Spot, if set, wins.
    void PlaceTipJar()
    {
        Transform eye = player.eyePoint != null ? player.eyePoint : (Camera.main != null ? Camera.main.transform : null);
        Vector3 head = eye != null ? eye.position : player.transform.position + Vector3.up * 1.6f;
        if (tipJarSpot != null)
        {
            tipJar = TipJar.Create(tipJarSpot.position, head, tipJarScale);
            return;
        }

        // In place of something already standing there (e.g. a bottle on the counter), which is put away. Several
        // things can share a name: it's the one nearest the player.
        GameObject replaced = null;
        if (!string.IsNullOrEmpty(tipJarReplaces))
        {
            float nearest = float.MaxValue;
            foreach (Transform thing in FindObjectsOfType<Transform>())
            {
                if (thing.name != tipJarReplaces) continue;
                float distance = Flat(thing.position - head).magnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    replaced = thing.gameObject;
                }
            }
        }
        if (replaced != null)
        {
            Bounds bounds = new Bounds(replaced.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer rend in replaced.GetComponentsInChildren<Renderer>())
            {
                if (!any) bounds = rend.bounds;
                else bounds.Encapsulate(rend.bounds);
                any = true;
            }
            replaced.SetActive(false);
            tipJar = TipJar.Create(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), head, tipJarScale);
            return;
        }
        if (!string.IsNullOrEmpty(tipJarReplaces)) Debug.LogWarning($"[Cat Cafe Waves] No \"{tipJarReplaces}\" in the scene for the tip jar to replace.");

        Vector3 forward = player.transform.forward;
        forward.y = 0;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 wanted = head + forward * 0.8f + right * 0.5f;

        Vector3? spot = BesideTheClock(wanted);
        if (spot == null)
        {
            Debug.LogWarning("[Cat Cafe Waves] No Cat_clock on a counter to put the tip jar beside; it's guessing the counter height.");
            spot = new Vector3(wanted.x, head.y - 0.65f, wanted.z);
        }
        tipJar = TipJar.Create(spot.Value, head, tipJarScale);
    }

    Vector3? BesideTheClock(Vector3 wanted)
    {
        GameObject catClock = GameObject.Find("Cat_clock");
        if (catClock == null) return null;

        // The counter top is where the clock's base sits
        float top = catClock.transform.position.y;
        bool any = false;
        foreach (Renderer rend in catClock.GetComponentsInChildren<Renderer>())
        {
            if (rend.GetComponentInParent<Canvas>() != null) continue;
            top = any ? Mathf.Min(top, rend.bounds.min.y) : rend.bounds.min.y;
            any = true;
        }
        Vector3 centre = catClock.transform.position;
        centre.y = top;

        // Spots around the clock where the counter is clear (a ray down lands on a surface at the counter's height),
        // the one nearest where we'd like it
        Vector3? best = null;
        float bestDistance = float.MaxValue;
        for (float distance = 0.3f; distance <= 0.61f; distance += 0.15f)
        {
            for (int angle = 0; angle < 360; angle += 30)
            {
                Vector3 spot = centre + Quaternion.Euler(0, angle, 0) * Vector3.forward * distance;
                if (!ClearCounterAt(spot, top, catClock.transform)) continue;
                float off = new Vector2(spot.x - wanted.x, spot.z - wanted.z).magnitude;
                if (off < bestDistance)
                {
                    bestDistance = off;
                    best = spot;
                }
            }
        }

        // Nothing to test against (no colliders): just beside the clock, towards where we'd like it
        if (best == null)
        {
            Vector3 towards = wanted - centre;
            towards.y = 0;
            best = centre + (towards.sqrMagnitude > 0.001f ? towards.normalized : Vector3.right) * 0.3f;
        }
        return best;
    }

    static bool ClearCounterAt(Vector3 spot, float top, Transform clock)
    {
        RaycastHit[] hits = Physics.RaycastAll(spot + Vector3.up * 0.6f, Vector3.down, 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float highest = float.MinValue;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(clock) || hit.collider.GetComponentInParent<CatMovement>() != null) continue;
            highest = Mathf.Max(highest, hit.point.y);
        }
        // The highest thing there is the counter top itself (not the floor below it, and nothing standing on it)
        return Mathf.Abs(highest - top) < 0.04f;
    }

    // Every hungry cat heads for the nearest door and leaves
    void SendCatsAway()
    {
        foreach (CatOrder order in orders)
        {
            if (order == null || !order.isHungry) continue;
            Vector3? exit = NearestExit(order.transform.position);
            if (exit.HasValue) order.RunOff(exit.Value);
        }
        orders.Clear();
    }

    // The floor at the door nearest `from` (some door markers sit a little above it)
    Vector3? NearestExit(Vector3 from)
    {
        Transform nearest = null;
        foreach (Transform door in doors)
        {
            if (door == null) continue;
            if (nearest == null || (door.position - from).sqrMagnitude < (nearest.position - from).sqrMagnitude) nearest = door;
        }
        if (nearest == null) return null;
        Vector3 exit = nearest.position;
        if (NavMesh.SamplePosition(exit, out NavMeshHit floor, 1.5f, NavMesh.AllAreas)) exit = floor.position;
        return exit;
    }

    // The wrong colour: an angry face (already shown), a moment to sulk, then it storms off without a tip
    void WrongTreat(CatOrder order)
    {
        if (wrongColourLeaves) StartCoroutine(StormOff(order));
    }

    IEnumerator StormOff(CatOrder order)
    {
        yield return new WaitForSeconds(0.6f);
        if (order == null || !order.isHungry || !playing) yield break;
        CatRaid raid = order.GetComponent<CatRaid>();
        if (raid != null && raid.isRunning) raid.Cancel();
        LeaveWithoutTip(order);
    }

    void LeaveWithoutTip(CatOrder order)
    {
        Vector3? exit = NearestExit(order.transform.position);
        if (exit.HasValue) order.RunOff(exit.Value);
        orders.Remove(order);
    }

    // Kept waiting too long: it goes for the tip jar (the right treat during its wind-up still calms it down), or,
    // with no jar (or two raids already on), storms off without a tip
    void PatienceOut(CatOrder order)
    {
        if (!playing || order == null || !order.isHungry) return;
        if (impatientCatsRaid && tipJar != null && CatRaid.active.Count < 2)
        {
            CatRaid.Target jar = JarTarget();
            if (jar != null && CatRaid.Begin(order, jar, raid, ImpatientRaidOver) != null) return;
        }
        LeaveWithoutTip(order);
    }

    void ImpatientRaidOver(CatRaid finished, CatRaid.Result result)
    {
        CatOrder order = finished.catOrder;
        if (order == null || !playing) return;
        // Calmed down with the right treat in time: it'll wait a while longer
        if (result == CatRaid.Result.Saved) order.RefillPatience(0.6f);
        // Got to the jar: it grabbed its tip and it's off
        else if (result == CatRaid.Result.Swatted && order.isHungry) LeaveWithoutTip(order);
    }

    // A cat-proof patch of floor where the player stands (cut out of the NavMesh while the round runs)
    void KeepCatsOutOfPlayerSpace()
    {
        if (playerSpace.x <= 0 || playerSpace.z <= 0 || player == null) return;
        GameObject space = new GameObject("Player Space (no cats)");
        Vector3 at = player.transform.position;
        if (NavMesh.SamplePosition(at, out NavMeshHit floor, 2f, NavMesh.AllAreas)) at.y = floor.position.y;
        space.transform.SetPositionAndRotation(at, Quaternion.Euler(0, player.transform.eulerAngles.y, 0));
        NavMeshObstacle obstacle = space.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = playerSpace;
        obstacle.center = new Vector3(0, playerSpace.y * 0.5f, 0);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
    }

    // Ninja stops far enough from the player (so nothing springs up right beside them)
    List<Vector3> StopsAwayFromPlayer()
    {
        if (player == null || ninjaMinDistance <= 0) return ninjaStops;
        Vector3 at = player.transform.position;
        List<Vector3> away = ninjaStops.FindAll(stop => new Vector2(stop.x - at.x, stop.z - at.z).magnitude >= ninjaMinDistance);
        return away.Count > 0 ? away : ninjaStops;
    }

    // Brings the ninja in at one of its stops; or at `holdAt`, where it stays put until told to go (holdStill)
    // (no default parameter: Liminal doesn't allow ones that use Unity types)
    void SummonNinja(bool boss)
    {
        SummonNinja(boss, null);
    }

    void SummonNinja(bool boss, Vector3? holdAt)
    {
        if (ninja == null) return;
        List<Vector3> stops;
        ninja.piles.Clear();
        if (ninjaExplodesPiles && PileStops(out stops, ninja.piles))
        {
            // Only the piles, each blown up whole (the finale's too, if it's making a mess)
            if (boss) ninja.Configure(!bossMakesMess, bossKnocksPerStop, bossSitMin, bossSitMax, true);
            else ninja.Configure(false, catchKnocksPerStop, catchSitMin, catchSitMax, true);
        }
        else
        {
            if (ninjaStops.Count == 0) return;
            stops = StopsAwayFromPlayer();
            if (boss) ninja.Configure(true, bossKnocksPerStop, bossSitMin, bossSitMax);
            else ninja.Configure(false, catchKnocksPerStop, catchSitMin, catchSitMax);
        }
        ninja.stops = stops;
        ninja.holdStill = holdAt.HasValue;   // before it starts, or its first move happens straight away
        // Blowing up piles, its phase sends it away when it's over (a long phase or a slow catch intro mustn't time it
        // out early); otherwise it keeps its own timer, as in Cat Cafe Rush
        ninja.despawnAfterOverride = ninjaExplodesPiles ? 300f : 0f;
        ninja.waitForCatches = ninjaWaitsForCatches;
        ninja.wantsTreats = () => ninja.feedable;   // a ninja that can't be fed lets treats past
        ninja.gameObject.SetActive(true);
        ninja.Initialise(transform, holdAt ?? stops[Random.Range(0, stops.Count)]);
        if (holdAt.HasValue) ninja.BlinkTo(holdAt.Value);
        // The boss gets the same generous hit area as the other cats
        if (cleanShots && ninjaHitbox == null) ninjaHitbox = CatOrder.AddHitbox(ninja.gameObject, hitboxGenerosity);
    }

    // A spot beside each prop pile (on its table, on the player's side) for the ninja to appear at
    bool PileStops(out List<Vector3> stops, List<PropPile> forPiles)
    {
        stops = new List<Vector3>();
        Vector3 viewer = player != null ? player.transform.position : transform.position;
        foreach (PropPile pile in propPiles)
        {
            if (pile == null || !pile.isActiveAndEnabled) continue;
            Vector3 at = pile.transform.position;
            Vector3 towards = Flat(viewer - at);
            towards = towards.sqrMagnitude > 0.001f ? towards.normalized : Vector3.forward;
            Vector3 beside = at + towards * 0.35f;
            if (!NavMesh.SamplePosition(beside, out NavMeshHit hit, 0.6f, NavMesh.AllAreas) &&
                !NavMesh.SamplePosition(at, out hit, 1.5f, NavMesh.AllAreas)) continue;
            stops.Add(hit.position);
            forPiles.Add(pile);
        }
        return stops.Count > 0;
    }

    // ---------- Cat Cafe Waves: the end-of-round tidy bonus ----------

    const float TidyBonusSeconds = 3f;
    const float TidyWaitMax = 15f;

    // Each pile pays into the tip jar (and the score) by how much of it is still standing
    IEnumerator TidyBonus()
    {
        if ((tidyBonusCoins <= 0 && tidyBonusScore <= 0) || propPiles.Count == 0) yield break;
        // Everything the ninja last sent flying comes down first (caught and carried home, or landed), so a pile that
        // was just blown up isn't counted while its plates are still in the air. The clock waits too (not forever).
        float waited = 0;
        wrapUpPaused = true;
        while (playing && FlyingProp.airborne.Count > 0 && waited < TidyWaitMax)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        wrapUpPaused = false;
        if (!playing) yield break;
        SayOnce(tidyLine);
        if (!string.IsNullOrEmpty(tidyBonusBanner)) GameManager.instance.ShowBanner(tidyBonusBanner, bannerSeconds);
        yield return new WaitForSeconds(0.6f);
        foreach (PropPile pile in propPiles)
        {
            if (pile == null) continue;
            float whole = pile.standing;
            Vector3 at = pile.transform.position + Vector3.up * 0.4f;
            int score = Mathf.RoundToInt(whole * tidyBonusScore);
            int coins = Mathf.RoundToInt(whole * tidyBonusCoins);
            if (score > 0) GameManager.instance.AddBonus(score, at);
            if (coins > 0 && tipJar != null) tipJar.AddCoins(at, coins);
            FloatingWord.Show($"{Mathf.RoundToInt(whole * 100)}%", at + Vector3.up * 0.3f,
                              whole > 0.66f ? new Color(0.4f, 1f, 0.5f) : whole > 0.33f ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.4f, 0.3f), 4f, 1.6f);
            yield return new WaitForSeconds(0.5f);
        }
    }
}
