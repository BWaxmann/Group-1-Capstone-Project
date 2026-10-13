using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// Runs the Space Cafe: sends customers to tables, decides their orders, adds foods to the menu
// as the round goes on, and flies coins into the register when cats are served.
// Set up by Catastrophe Cafe > Space Cafe > Generate Layout.
public class SpaceCafeService : MonoBehaviour
{
    [Serializable]
    public class Food
    {
        public string name = "Food";
        [Tooltip("Position of this food's treat in the Treats Pool (0 = Blue, 1 = Orange, 2 = Pink, 3 = Purple).")]
        public int treatPoolVariation;
        [Tooltip("Shown inside the order bubble.")]
        public GameObject treatPrefab;
        public Color colour = Color.white;
    }

    [Serializable]
    public class Stage
    {
        [Min(0)] public float startsAfterSeconds;
        [Min(1)] public int foodsOnMenu = 1;
        [Range(0, 180)]
        [Tooltip("Only tables within this many degrees of the player's front get customers. 180 = all around.")]
        public float tableAngleLimit = 60;
        [Min(1)] public int maxCustomers = 2;
        [Min(0.1f)] public float secondsBetweenCustomers = 2.5f;
        [Tooltip("Banner shown when this stage starts. Leave empty for none.")]
        public string announcement;
    }

    [SerializeField]
    Food[] foods;

    [SerializeField]
    CustomerCat[] customerPrefabs;

    [SerializeField]
    HandFoods handFoods;

    [SerializeField]
    [Tooltip("Where the coins fly to (the register or tip jar).")]
    Transform coinTarget;

    [SerializeField]
    [Min(1)]
    float customerPatience = 14f;

    [SerializeField]
    Stage[] stages = new Stage[]
    {
        new Stage { startsAfterSeconds = 0, foodsOnMenu = 2, tableAngleLimit = 50, maxCustomers = 2, secondsBetweenCustomers = 2.5f },
        new Stage { startsAfterSeconds = 50, foodsOnMenu = 2, tableAngleLimit = 100, maxCustomers = 3, secondsBetweenCustomers = 2f, announcement = "MORE CUSTOMERS!" },
        new Stage { startsAfterSeconds = 130, foodsOnMenu = 3, tableAngleLimit = 180, maxCustomers = 4, secondsBetweenCustomers = 1.6f, announcement = "NEW: PINK!\n<size=60%>Flick a thumbstick to switch</size>" }
    };

    [Header("Finale (LAST ORDERS!)")]

    [SerializeField]
    [Min(0)]
    int finaleExtraCustomers = 2;

    [SerializeField]
    [Min(0.1f)]
    float finaleSpawnSpeedMultiplier = 0.6f;

    [Header("Coins")]

    [SerializeField]
    [Min(0)]
    int coinsPerCustomer = 3;

    [SerializeField]
    [Min(0.1f)]
    float coinFlightTime = 0.6f;

    [SerializeField]
    Color coinColour = new Color(1f, 0.8f, 0.15f);

    [Header("Sounds")]

    [SerializeField]
    SoundData portalSound = new SoundData(1f, 1f);

    [SerializeField]
    SoundData wrongFoodSound = new SoundData(1f, 1f);

    [SerializeField]
    SoundData coinSound = new SoundData(1f, 1f);

    public static SpaceCafeService instance;

    public event Action<int> CoinsChanged;

    public int coins { get; private set; }
    public int foodCount => foods != null ? foods.Length : 0;

    readonly List<CustomerCat> pool = new List<CustomerCat>();
    PlayerInput player;
    int stageIndex = -1;
    bool finale;
    Transform coinTargetPunch;
    Vector3 coinTargetScale;

    public void Setup(Food[] menu, CustomerCat[] customers, HandFoods hands, Transform coinsGoTo,
                      SoundData portal, SoundData wrongFood, SoundData coin)
    {
        foods = menu;
        customerPrefabs = customers;
        handFoods = hands;
        coinTarget = coinsGoTo;
        portalSound = portal;
        wrongFoodSound = wrongFood;
        coinSound = coin;
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        player = FindObjectOfType<PlayerInput>();
        GameManager.instance.GameStart += BeginService;
        GameManager.instance.FinaleStart += BeginFinale;
        GameManager.instance.GameEnd += EndService;

        if (coinTarget != null)
        {
            coinTargetPunch = coinTarget;
            coinTargetScale = coinTarget.localScale;
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (GameManager.instance != null)
        {
            GameManager.instance.GameStart -= BeginService;
            GameManager.instance.FinaleStart -= BeginFinale;
            GameManager.instance.GameEnd -= EndService;
        }
    }

    public Color FoodColour(int foodIndex)
    {
        return foods != null && foodIndex >= 0 && foodIndex < foods.Length ? foods[foodIndex].colour : Color.white;
    }

    public int FoodVariation(int foodIndex)
    {
        return foods != null && foodIndex >= 0 && foodIndex < foods.Length ? foods[foodIndex].treatPoolVariation : -1;
    }

    void BeginService()
    {
        coins = 0;
        finale = false;
        stageIndex = -1;

        // Customers first, so nothing below can stop the service from starting
        StartCoroutine(ServiceRoutine());
        UpdateStage(0, false);

        // Left hand gets the first food, right hand the second
        if (handFoods == null) handFoods = gameObject.AddComponent<HandFoods>();
        handFoods.Begin(Mathf.Min(stages[stageIndex].foodsOnMenu, foodCount));
    }

    void BeginFinale()
    {
        finale = true;
        if (handFoods != null) handFoods.SetFoodsAvailable(foodCount);
    }

    void EndService()
    {
        StopAllCoroutines();
    }

    IEnumerator ServiceRoutine()
    {
        float elapsed = 0;
        float spawnTimer = 1f;
        while (true)
        {
            elapsed += Time.deltaTime;
            spawnTimer -= Time.deltaTime;
            UpdateStage(elapsed, true);

            Stage stage = stages[stageIndex];
            int maxCustomers = stage.maxCustomers + (finale ? finaleExtraCustomers : 0);
            if (spawnTimer <= 0 && CustomersOut() < maxCustomers)
            {
                if (SendCustomer(finale ? 180f : stage.tableAngleLimit, finale ? foodCount : stage.foodsOnMenu))
                {
                    spawnTimer = stage.secondsBetweenCustomers * (finale ? finaleSpawnSpeedMultiplier : 1f);
                }
            }
            yield return null;
        }
    }

    void UpdateStage(float elapsed, bool announce)
    {
        if (stages == null || stages.Length == 0) return;

        int newStage = 0;
        for (int i = 0; i < stages.Length; i++)
        {
            if (elapsed >= stages[i].startsAfterSeconds) newStage = i;
        }
        if (newStage == stageIndex) return;

        stageIndex = newStage;
        if (handFoods != null) handFoods.SetFoodsAvailable(Mathf.Min(stages[stageIndex].foodsOnMenu, foodCount));
        if (announce && !string.IsNullOrEmpty(stages[stageIndex].announcement))
        {
            GameManager.instance.ShowBanner(stages[stageIndex].announcement, 3f);
            Haptics.PulseBoth(Haptics.Strength.Medium);
        }
    }

    int CustomersOut()
    {
        int count = 0;
        foreach (CustomerCat cat in pool)
        {
            if (cat != null && cat.gameObject.activeSelf) count++;
        }
        return count;
    }

    bool SendCustomer(float angleLimit, int foodsOnMenu)
    {
        List<CustomerTable> free = new List<CustomerTable>();
        foreach (CustomerTable table in CustomerTable.all)
        {
            if (table != null && table.isFree && table.angle <= angleLimit + 0.1f)
            {
                free.Add(table);
            }
        }
        if (free.Count == 0) return false;

        CustomerCat cat = GetCustomer();
        if (cat == null) return false;

        int food = Random.Range(0, Mathf.Clamp(foodsOnMenu, 1, foodCount));
        CustomerTable chosen = free[Random.Range(0, free.Count)];
        cat.gameObject.SetActive(true);
        cat.Arrive(chosen, FoodVariation(food), foods[food].treatPrefab, foods[food].colour, customerPatience);
        return true;
    }

    CustomerCat GetCustomer()
    {
        if (customerPrefabs == null || customerPrefabs.Length == 0)
        {
            Debug.LogError("[Space Cafe] No customer cat prefabs are assigned.", this);
            return null;
        }

        CustomerCat prefab = customerPrefabs[Random.Range(0, customerPrefabs.Length)];
        foreach (CustomerCat cat in pool)
        {
            if (cat != null && !cat.gameObject.activeSelf && cat.name == prefab.name) return cat;
        }

        CustomerCat newCat = Instantiate(prefab, transform);
        newCat.name = prefab.name;
        newCat.gameObject.SetActive(false);
        pool.Add(newCat);
        return newCat;
    }

    public void CustomerServed(CustomerCat cat)
    {
        if (coinTarget != null && coinsPerCustomer > 0)
        {
            StartCoroutine(CoinShower(cat.transform.position + Vector3.up * 0.2f));
        }
    }

    // Coins pop out of the happy cat and arc into the register, one after another
    IEnumerator CoinShower(Vector3 from)
    {
        for (int i = 0; i < coinsPerCustomer; i++)
        {
            StartCoroutine(FlyCoin(from + Random.insideUnitSphere * 0.08f, i));
            yield return new WaitForSeconds(0.08f);
        }
    }

    IEnumerator FlyCoin(Vector3 from, int index)
    {
        GameObject coin = SpaceCafeVisuals.Shape(PrimitiveType.Cylinder, transform, "Coin", coinColour);
        coin.transform.localScale = new Vector3(0.06f, 0.005f, 0.06f);
        coin.transform.position = from;

        Vector3 to = coinTarget.position + Vector3.up * 0.1f;
        float height = 0.6f + Vector3.Distance(from, to) * 0.15f;
        float timer = 0;
        while (timer < coinFlightTime)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / coinFlightTime);
            float eased = t * t * (3f - 2f * t);
            coin.transform.position = Vector3.Lerp(from, to, eased) + Vector3.up * Mathf.Sin(t * Mathf.PI) * height;
            coin.transform.Rotate(720f * Time.deltaTime, 0, 0, Space.Self);
            yield return null;
        }
        Destroy(coin);

        coins++;
        CoinsChanged?.Invoke(coins);
        PlayAt(coinSound, to, 1f + index * 0.1f);
        if (coinTargetPunch != null)
        {
            StartCoroutine(Punch(coinTargetPunch));
        }
    }

    IEnumerator Punch(Transform target)
    {
        float timer = 0;
        while (timer < 0.2f)
        {
            timer += Time.deltaTime;
            target.localScale = coinTargetScale * (1f + 0.12f * Mathf.Sin(timer / 0.2f * Mathf.PI));
            yield return null;
        }
        target.localScale = coinTargetScale;
    }

    public void PlayPortalSound(Vector3 position)
    {
        PlayAt(portalSound, position, Random.Range(0.9f, 1.1f));
    }

    public void PlayWrongFoodSound(Vector3 position)
    {
        PlayAt(wrongFoodSound, position, Random.Range(0.95f, 1.1f));
    }

    static void PlayAt(SoundData sound, Vector3 position, float pitch)
    {
        if (sound.soundClip == null) return;

        GameObject temp = new GameObject("Sound");
        temp.transform.position = position;
        AudioSource source = temp.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.minDistance = 2f;
        sound.PlayOneShot(source, pitch);
        Destroy(temp, sound.soundClip.length / Mathf.Max(0.1f, Mathf.Abs(source.pitch)) + 0.1f);
    }
}
