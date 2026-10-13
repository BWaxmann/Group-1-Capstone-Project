using System.Collections;
using UnityEngine;

// Each hand's launcher fires its own food (left starts Blue, right starts Orange).
// When more foods are on the menu, squeezing a grip switches that hand to the next food.
// A tin in the food's colour hangs under each launcher so you always know which is which.
// In the editor, keys 1-3 set the right hand's food (the mouse only fires the right launcher).
public class HandFoods : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The model shown under each launcher (tinted to the food's colour).")]
    GameObject tinPrefab;

    [SerializeField]
    [Min(0.02f)]
    float tinSize = 0.09f;

    [SerializeField]
    SoundData switchSound = new SoundData(1f, 1f);

    PlayerInput player;
    AudioSource audioSource;
    int foodsAvailable = 1;
    readonly int[] handFood = { 0, 1 };              // [0] = left, [1] = right
    readonly GameObject[] handTin = new GameObject[2];

    public void Setup(GameObject tinModel, SoundData switchFoodSound)
    {
        tinPrefab = tinModel;
        switchSound = switchFoodSound;
    }

    bool FindPlayer()
    {
        if (player == null) player = FindObjectOfType<PlayerInput>();
        return player != null;
    }

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
    }

    // Called when the round starts: left hand gets the first food, right hand the second
    public void Begin(int foodsOnMenu)
    {
        foodsAvailable = Mathf.Max(1, foodsOnMenu);
        Set(false, 0, false);
        Set(true, Mathf.Min(1, foodsAvailable - 1), false);
    }

    public void SetFoodsAvailable(int count)
    {
        foodsAvailable = Mathf.Max(1, count);
    }

    void Update()
    {
        if (!FindPlayer() || !player.isBaristaMode || !player.hasLaunchers) return;

        // The tins are made the first time the launchers are in hand
        if (handTin[0] == null || handTin[1] == null)
        {
            Set(false, handFood[0], false);
            Set(true, handFood[1], false);
        }

        if (foodsAvailable > 1)
        {
            if (player.GripPressed(false)) Set(false, (handFood[0] + 1) % foodsAvailable, true);
            if (player.GripPressed(true)) Set(true, (handFood[1] + 1) % foodsAvailable, true);
        }

#if UNITY_EDITOR
        for (int i = 0; i < Mathf.Min(foodsAvailable, 9); i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Set(true, i, true);
        }
#endif
    }

    void Set(bool rightHand, int foodIndex, bool feedback)
    {
        SpaceCafeService service = SpaceCafeService.instance;
        if (service == null || !FindPlayer()) return;

        int hand = rightHand ? 1 : 0;
        handFood[hand] = Mathf.Clamp(foodIndex, 0, Mathf.Max(0, service.foodCount - 1));
        player.LoadFood(rightHand, service.FoodVariation(handFood[hand]));

        // Swap the tin under that launcher
        Transform launcher = player.Launcher(rightHand);
        if (launcher == null || !launcher.gameObject.activeInHierarchy) return;
        if (handTin[hand] != null) Destroy(handTin[hand]);
        handTin[hand] = SpaceCafeVisuals.TinUnderLauncher(launcher, tinPrefab, service.FoodColour(handFood[hand]), tinSize);

        if (feedback)
        {
            Haptics.Pulse(rightHand, Haptics.Strength.Medium);
            switchSound.PlayOneShot(audioSource);
            StartCoroutine(Pop(handTin[hand].transform));
        }
    }

    IEnumerator Pop(Transform tin)
    {
        float timer = 0;
        while (timer < 0.3f && tin != null)
        {
            timer += Time.deltaTime;
            tin.localScale = Vector3.one * Mathf.Max(0.01f, SpaceCafeVisuals.EaseOutBack(timer / 0.3f));
            yield return null;
        }
        if (tin != null) tin.localScale = Vector3.one;
    }
}
