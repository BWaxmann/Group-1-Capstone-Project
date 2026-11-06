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
public partial class RushDirector : MonoBehaviour
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

        [Min(1)]
        public float seconds = 35f;

        [Tooltip("Feed and Boss: how many hungry cats to keep about.")]
        [Min(0)]
        public int cats = 3;

        [Tooltip("Feed and Boss: shortest and longest order (treats per cat).")]
        [Min(1)]
        public int shortestOrder = 1;

        [Min(1)]
        public int longestOrder = 1;

        [TextArea]
        public string banner = "";

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

    [Min(0.01f)]
    public float tinSize = 0.09f;

    [Header("Ninja")]

    public NinjaRushCat ninjaPrefab;

    public List<Vector3> ninjaStops = new List<Vector3>();

    [Min(1)]
    public int catchKnocksPerStop = 2;

    [Min(0.2f)]
    public float catchSitMin = 0.8f;

    [Min(0.2f)]
    public float catchSitMax = 1.2f;

    [Min(1)]
    public int bossKnocksPerStop = 1;

    [Min(0.2f)]
    public float bossSitMin = 2f;

    [Min(0.2f)]
    public float bossSitMax = 2.8f;

    public string bossFedBanner = "NINJA FED!";

    public string bossLeftBanner = "THE NINJA VANISHED!";

    [SerializeField] GameObject ninjaInputTutorialImage;

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
    [Range(0f, 180f)]
    public float burstSpread = 180f;

    [Tooltip("How fast the burst throws a prop sideways, in m/s.")]
    [Min(0f)]
    public float burstOutwardSpeed = 3.5f;

    [Tooltip("How fast the burst throws a prop upwards, in m/s (less = flatter, more horizontal).")]
    [Min(0f)]
    public float burstUpSpeed = 3.2f;

    [Tooltip("Highest a prop can fly, in metres above the floor (it never goes into the ceiling). 0 = leave as is.")]
    [Min(0f)]
    public float maxBurstHeight = 0f;

    [Tooltip("Seconds a prop flies away before it can be caught.")]
    [Min(0f)]
    public float catchableAfter = 0.3f;

    [Tooltip("How slowly props sink once the burst is over, in m/s.")]
    [Min(0.05f)]
    public float fallSpeed = 0.55f;

    [Header("Trials")]

    [Tooltip("A cat attacking a pile tips its top prop over (not catchable) instead of sending it flying.")]
    public bool attacksTopple = false;

    [Tooltip("In the boss finale, each hand can flick its thumbstick to swap Single / Scatter / Buckshot.")]
    public bool finaleFireStyles = false;

    [TextArea]
    public string fireStylesBanner = "NINJA BOSS!\n<size=50%>Flick the thumbsticks to swap shots!</size>";

    [Header("Player's space")]

    [Tooltip("The ninja never appears closer than this to the player, in metres.")]
    [Min(0f)]
    public float ninjaMinDistance = 2.5f;

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

    [Header("The first customer")]

    [Tooltip("With the launchers in the player's hands from the start, one cat waits to be fed (a treat of each colour); feeding it starts the countdown. Off = the countdown starts by itself after the welcome.")]
    public bool firstCustomer = true;

    [Tooltip("Where the first customer waits (an empty object in the scene, its blue arrow the way the cat faces). Empty = just in front of the player.")]
    public Transform firstCustomerSpot;

    [Min(0f)]
    [Tooltip("Seconds the first customer waits before it gives up and the countdown starts anyway. 0 = forever.")]
    public float firstCustomerTimeout = 90f;

    [Tooltip("Optional. Hung above the first customer until it's fed: a prefab of the arrow and glow that used to mark the launchers, say.")]
    public GameObject firstCustomerSign;

    [Min(0f)]
    [Tooltip("How high above the cat's feet the sign hangs, in metres.")]
    public float firstCustomerSignHeight = 1.3f;

    [Min(0f)]
    [Tooltip("With no first customer: seconds after the rig is up before the countdown starts by itself.")]
    public float startsByItselfAfter = 8f;

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

    [Min(0)]
    public int bossCoins = 8;

    [Tooltip("Optional: where the tip jar stands. Empty = on the counter beside the cat clock.")]
    public Transform tipJarSpot;

    [Tooltip("Optional: the name of an object the tip jar takes the place of (it's hidden), e.g. a bottle on the counter.")]
    public string tipJarReplaces = "";

    [Min(0.5f)]
    public float tipJarScale = 1f;

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
    public float propDropMargin = 0.8f;

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

    [Min(0f)]
    public float orderDelayMin = 2f;

    [Min(0f)]
    public float orderDelayMax = 4.5f;

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

    [Range(0.3f, 1f)]
    public float kittenScale = 0.55f;

    [Min(1)]
    public int kittensPerHerdMin = 5;

    [Min(1)]
    public int kittensPerHerdMax = 7;

    [Min(0.5f)]
    public float herdGapMin = 2.2f;

    [Min(0.5f)]
    public float herdGapMax = 3.2f;

    [Min(0.5f)]
    public float kittenSpeedMin = 3.2f;

    [Min(0.5f)]
    public float kittenSpeedMax = 4.2f;

    [Min(0f)]
    [Tooltip("Seconds each herd runs around in front of the player (zig-zagging) before it heads off. 0 = straight across.")]
    public float herdRoamMin = 5f;

    [Min(0f)]
    public float herdRoamMax = 10f;

    [Min(1)]
    [Tooltip("Most kittens out at once (the Quest's limit: a new herd sends fewer if it would go over).")]
    public int maxKittens = 24;

    [Min(0.1f)]
    [Tooltip("Radius of each kitten's invisible hit area, in metres.")]
    public float kittenHitRadius = 0.35f;

    [Min(0)]
    public int kittenScore = 2;

    [Min(0)]
    public int kittenCoins = 1;

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

    [Range(0f, 24f)]
    public float shiftStartHour = 9f;

    [Range(0f, 24f)]
    public float shiftEndHour = 17f;

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

    [Min(0.5f)]
    public float spyGapStart = 4f;

    [Min(0.5f)]
    public float spyGapEnd = 2.5f;

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
    [Min(1)]
    public int remixMaxBusy = 1;

    [Min(0)]
    [Tooltip("Seconds of breather in the finale after a big thing finishes before the next may start (so they come one after another, not in a pile).")]
    public float remixBreather = 6f;

    [Tooltip("Seconds between spy cats in the finale (0 = none).")]
    [Min(0)]
    public float remixSpyGapMin = 14f;

    [Min(0)]
    public float remixSpyGapMax = 20f;

    [Min(0)]
    public int remixHeists = 2;

    [Min(0)]
    public int remixHeistGang = 1;

    [Range(0f, 1f)]
    [Tooltip("How far through the finale a kitten flood pours out of a vent (0 = none).")]
    public float remixFloodAt = 0.45f;

    [Min(0.2f)]
    [Tooltip("Seconds the ninja sits between piles in the finale (slower than the catch phase: there's a lot else going on).")]
    public float remixNinjaSitMin = 7f;

    [Min(0.2f)]
    public float remixNinjaSitMax = 10f;

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

    public string catchIntroHint = "Grip!";

    public string catchIntroPraise = "Nice catch!";

    [Min(1f)]
    public float catchIntroTimeout = 12f;

    PlayerInput player;
    bool launchersReady;
    CatOrder firstCustomerOrder;
    GameObject firstCustomerSignInstance;
    Phase current;
    bool playing;

    [SerializeField]
    Announcer voice;

    bool windingDown;   // the end of a phase: nothing new starts

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
        if (voice == null || string.IsNullOrEmpty(line))
        {
            return true;
        }
        if (heardLines.Contains(line) && voice.saying != line)
        {
            return true;
        }
        return !voice.talking;
    }

    IEnumerator WaitClockRoutine(float seconds)
    {
        float until = clock + seconds;
        while (clock < until)
        {
            yield return null;
        }
    }

    readonly HashSet<string> heardLines = new HashSet<string>();     // said this round (not just queued)
    readonly HashSet<string> pendingLines = new HashSet<string>();   // queued, not said yet

    void Awake()
    {
        ParticleManager.effectScale = effectScale;
        PickupManager.allowed = index => allowedPickups.Contains(index);
        Treat.cleanShots = cleanShots;
        Treat.aimAssistTurnSpeed = aimAssistTurnSpeed;
        if (aimAssistAngle > 0)
        {
            Treat.aimAssistTarget = (System.Func<Treat, Vector3?>)AssistTarget;
        }
        else
        {
            Treat.aimAssistTarget = null;
        }
        PropPile.barsWaitForNinja = ninjaExplodesPiles;
        SoundLevels.music = musicLevel;
        SoundLevels.cats = catLevel;
        SoundLevels.world = worldLevel;
    }

    void Start()
    {
        ninjaInputTutorialImage?.SetActive(false);

        // The Mafia cat for heists, borrowed from the boss spawner (which may have switched itself off)
        foreach (BossCatSpawner spawner in Resources.FindObjectsOfTypeAll<BossCatSpawner>())
        {
            if (spawner.gameObject.scene.IsValid() && mafiaPrefab == null)
            {
                mafiaPrefab = spawner.Prefab<MafiaBossCat>();
            }
        }

        // How knocked props fly in this mode
        CatChaosSettings flight = CatChaosSettings.instance;
        if (flight != null)
        {
            flight.launchSpread = burstSpread;
            flight.launchTowardPlayerSpeed = burstOutwardSpeed;
            flight.launchUpSpeed = burstUpSpeed;
            if (maxBurstHeight > 0)
            {
                flight.maxHeightAboveFloor = maxBurstHeight;
            }
            flight.catchableAfter = catchableAfter;
            flight.driftFallSpeed = fallSpeed;
            flight.catchableGlow = catchGlow;
            flight.missedStayDown = missedPropsStayDown;
        }

        player = FindObjectOfType<PlayerInput>();
        // Cat Cafe Waves (a remix finale at the end): the round is as long as its phases, so a longer phase makes a longer
        // round rather than squeezing the others
        float roundLength = 0;
        foreach (Phase phase in phases)
        {
            roundLength += phase.seconds;
        }
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
            StartCoroutine(Warmup.RunRoutine(shaderVariants));
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
            StartCoroutine(WelcomeRoutine());
        }
        StartCoroutine(LobbyRoutine());
    }

    IEnumerator WelcomeRoutine()
    {
        yield return new WaitForSeconds(1.5f);
        if (!playing)
        {
            Say(welcomeLine);
        }
    }

    // ---------- The lobby: launchers in hand, and the first customer who opens the cafe ----------

    // The launchers as the shift needs them: each hand's food (with a tin of that colour hanging under it), the reticle
    // colours, the treat speed. Once: the lobby does it as soon as the rig is up, the shift start makes sure.
    void SetUpLaunchers()
    {
        if (launchersReady || player == null)
        {
            return;
        }
        launchersReady = true;
        player.treatSpeed = treatSpeed;
        player.SetReticleColours(leftColour, rightColour);
        player.blockFire = LauncherKitten.BlocksFire;
        player.LoadFood(false, leftFood);
        player.LoadFood(true, rightFood);
        if (tinPrefab != null)
        {
            SpaceCafeVisuals.TinUnderLauncher(player.Launcher(false), tinPrefab, leftColour, tinSize);
            SpaceCafeVisuals.TinUnderLauncher(player.Launcher(true), tinPrefab, rightColour, tinSize);
        }
    }

    // Waits for the player rig (Liminal switches it on after the scene starts). With the launchers already in its
    // hands the cafe opens the new way: one customer waits to be fed, and that starts the countdown. With a floating
    // pair to pick up instead, picking it up starts the countdown, as it always did.
    IEnumerator LobbyRoutine()
    {
        float giveUpAt = Time.time + 10f;
        while ((player == null || player.eyePoint == null) && Time.time < giveUpAt)
        {
            if (player == null)
            {
                player = FindObjectOfType<PlayerInput>();
            }
            yield return null;
        }
        if (player == null || playing)
        {
            yield break;
        }
        yield return null;   // the rig's own Start has run by now
        if (!player.hasLaunchers)
        {
            yield break;
        }
        SetUpLaunchers();

        // The cat pool fills over the first second or two
        giveUpAt = Time.time + 10f;
        while (firstCustomer && catPool != null && !catPool.isReady && Time.time < giveUpAt)
        {
            yield return null;
        }

        if (!firstCustomer || !SeatFirstCustomer())
        {
            if (firstCustomer)
            {
                Debug.LogWarning($"[Cat Cafe Waves] No first customer could be seated: the countdown starts by itself in {startsByItselfAfter} s.");
            }
            yield return new WaitForSeconds(startsByItselfAfter);
            if (!playing)
            {
                GameManager.instance.BeginCountdown();
            }
            yield break;
        }
        if (firstCustomerTimeout <= 0f)
        {
            yield break;
        }
        yield return new WaitForSeconds(firstCustomerTimeout);
        if (!playing && firstCustomerOrder != null)
        {
            // Nobody fed it: it wanders off, and the shift starts anyway
            CatOrder waiting = firstCustomerOrder;
            firstCustomerOrder = null;
            TakeDownFirstCustomerSign();
            Vector3? exit = NearestExit(waiting.transform.position);
            if (exit.HasValue)
            {
                waiting.RunOff(exit.Value);
            }
            GameManager.instance.BeginCountdown();
        }
    }

    // One cat in front of the player, wanting one treat of each colour, as patient as can be
    bool SeatFirstCustomer()
    {
        if (catPool == null)
        {
            Debug.LogWarning("[Cat Cafe Waves] No Cat Pool on the Rush Director, so no first customer.");
            return false;
        }
        if (!catPool.isReady)
        {
            Debug.LogWarning("[Cat Cafe Waves] The cat pool wasn't ready in time, so no first customer.");
            return false;
        }
        Transform spot = firstCustomerSpot;
        if (spot == null)
        {
            // Just in front of the player, facing it
            Vector3 forward = Flat(player.transform.forward);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }
            forward = forward.normalized;
            GameObject marker = new GameObject("First Customer Spot");
            marker.transform.position = player.transform.position + forward * 1.8f;
            marker.transform.rotation = Quaternion.LookRotation(-forward);
            spot = marker.transform;
        }
        Vector3 position = spot.position;
        if (NavMesh.SamplePosition(position, out NavMeshHit floor, 1.5f, NavMesh.AllAreas))
        {
            position = floor.position;
        }
        CatMovement cat = catPool.GetRandomObjectFromPool<CatMovement>();
        if (cat == null)
        {
            Debug.LogWarning("[Cat Cafe Waves] The cat pool had no cat to spare for the first customer.");
            return false;
        }
        cat.gameObject.SetActive(true);
        cat.Initialise(spot, position);

        List<CatOrder.Food> foods = new List<CatOrder.Food>();
        foods.Add(new CatOrder.Food
        {
            variation = leftFood,
            icon = new OrderBubble.Item(leftTreatPrefab, leftColour)
        });
        foods.Add(new CatOrder.Food
        {
            variation = rightFood,
            icon = new OrderBubble.Item(rightTreatPrefab, rightColour)
        });
        float generosity = 0f;
        if (cleanShots)
        {
            generosity = hitboxGenerosity;
        }
        CatOrder order = CatOrder.Give(cat, foods, bubbleScale, generosity, catSpeedMultiplier);
        order.orderSpotFor = waiting => position;   // it stays where it is
        order.served = FirstCustomerServed;
        order.OrderLater(0f, 0f);   // orders at once, and waits for good
        firstCustomerOrder = order;
        if (firstCustomerSign != null)
        {
            firstCustomerSignInstance = Instantiate(firstCustomerSign, cat.transform);
            firstCustomerSignInstance.transform.localPosition = new Vector3(0f, firstCustomerSignHeight, 0f);
            firstCustomerSignInstance.transform.localRotation = Quaternion.identity;
        }
        return true;
    }

    // Fed: it pops off happily (the usual finisher) and the countdown starts
    void FirstCustomerServed(CatOrder order)
    {
        firstCustomerOrder = null;
        TakeDownFirstCustomerSign();
        GameManager.instance.BeginCountdown();
    }

    void TakeDownFirstCustomerSign()
    {
        if (firstCustomerSignInstance != null)
        {
            Destroy(firstCustomerSignInstance);
            firstCustomerSignInstance = null;
        }
    }

    // Queues a line. What's actually been heard is kept track of (a line crowded out by others isn't)
    // `hold`: the waves' clock stands still until it's been said (a wave's callout: the wave gets all its time after it)
    void Say(string line, string sting = null, bool urgent = false, bool hold = false)
    {
        if (voice == null)
        {
            return;
        }
        if (!string.IsNullOrEmpty(line))
        {
            pendingLines.Add(line);
        }
        voice.Say(line, sting, said =>
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }
            pendingLines.Remove(line);
            if (said)
            {
                heardLines.Add(line);
            }
        }, urgent, hold);
    }

    // A hint about what's happening right now, said until it's been heard once: it jumps the queue, and if it still gets
    // crowded out (it waited too long), it's said the next time its moment comes round instead
    void SayOnce(string line)
    {
        if (string.IsNullOrEmpty(line) || heardLines.Contains(line) || pendingLines.Contains(line))
        {
            return;
        }
        Say(line, null, true);
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
        if (player == null)
        {
            player = FindObjectOfType<PlayerInput>();
        }
        if (player == null)
        {
            Debug.LogError("[Cat Cafe Rush] No player rig (PlayerInput) found when the round started.");
            return;
        }

        SetUpLaunchers();
        KeepCatsOutOfPlayerSpace();
        PickEntryDoors();
        FindVents();
        if (fedCatsUseHoles)
        {
            MakeHoles();
        }
        if (coinsPerTreat > 0)
        {
            PlaceTipJar();
        }
        if (tipJar != null)
        {
            TipsBoard.Begin(tipJar, tipsBoard);
        }
        piles.Clear();
        piles.AddRange(FindObjectsOfType<DefendPoint>());
        propPiles.Clear();
        propPiles.AddRange(FindObjectsOfType<PropPile>());

        // Catching can be made a little harder (the grip has to point closer to the prop)
        GravityBeam beam = player.GetComponent<GravityBeam>();
        if (beam != null && catchAimAngle > 0)
        {
            beam.aimAngle = catchAimAngle;
        }

        StartCoroutine(RunPhasesRoutine());
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
        if (MafiaHeist.current != null)
        {
            MafiaHeist.current.Cancel();
        }
        KittenRun.EndAll();
        if (player != null)
        {
            player.ForceFireStyle(null);
        }
        if (ninja != null)
        {
            ninja.Leave();
        }
        Say(closingLine);
    }

    // Nothing solid between the player's eye and `point` (cats, props and the player's own rig don't count)
    static bool CanSee(Vector3 eye, Vector3 point)
    {
        Vector3 to = point - eye;
        foreach (RaycastHit hit in Physics.RaycastAll(eye, to.normalized, to.magnitude - 0.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Collider other = hit.collider;
            if (other.GetComponentInParent<CatMovement>() != null || other.GetComponentInParent<FlyingProp>() != null ||
                other.GetComponentInParent<PlayerInput>() != null)
            {
                continue;
            }
            return false;
        }
        return true;
    }

    void Update()
    {
        if (playing)
        {
            if (!holding && !introPaused && !waveHeld)
            {
                clock += Time.deltaTime;   // in step with the round clock
            }
            GameManager.instance.clockPaused = wrapUpPaused || introPaused || holding || waveHeld;
        }
        if (!playing || current == null)
        {
            return;
        }
        if (raids)
        {
            UpdateRaids();
        }
        UpdateBreather();
        if (windingDown)
        {
            return;
        }
        if (Feeding(current.kind) && current.latchEvery > 0)
        {
            UpdateLatches();
        }
        if (!HasOrders(current.kind))
        {
            return;
        }
        if (Time.time - lastArrival < arrivalGap)
        {
            return;
        }

        orders.RemoveAll(o => o == null || !o.isHungry);
        if (orders.Count + droppingIn < current.cats)
        {
            SendInCat();
        }
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
}
