using UnityEngine;

public class ParticleManager : MonoBehaviour
{
    [SerializeField] ObjectSpawner particleSpawner;

    public static ParticleManager instance;

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
        if (particleSpawner == null)
        {
            particleSpawner = GetComponent<ObjectSpawner>();
        }
    }

    public void ShowParticleEffect(int index, Vector3 position)
    {
        GameObject obj = particleSpawner.GetObjectFromPool(index);
        if (obj != null)
        {
            obj.SetActive(true);
            obj.transform.position = position;
        }
    }

    public void ShowParticleEffect(int index, Vector3 position, Quaternion rotation)
    {
        GameObject obj = particleSpawner.GetObjectFromPool(index);
        if (obj != null)
        {
            obj.SetActive(true);
            obj.transform.SetPositionAndRotation(position, rotation);
        }     
    }
}
