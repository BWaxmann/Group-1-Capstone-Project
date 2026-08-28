using UnityEngine;

public class DefendPoint : MonoBehaviour
{
    [SerializeField]
    [Min(0)]
    int hitScorePenalty = 1;

    int _timesHit;

    public Vector3 position => transform.position;
    public int timesHit => timesHit;

    public void GetHit()
    {
        _timesHit++;
        GameManager.instance.UpdateScore(-hitScorePenalty, false);
        ParticleManager.instance.ShowParticleEffect(0, position);
    }
}
