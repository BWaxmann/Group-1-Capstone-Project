using UnityEngine;

public class DefendPoint : MonoBehaviour
{
    [SerializeField]
    [Min(0)]
    int hitScorePenalty = 1;

    [SerializeField]
    [Min(0)]
    int hitParticlesIndex = 0;

    [SerializeField]
    SoundData hitSound = new SoundData(1f, 1f);

    [SerializeField]
    AudioSource hitSource;

    int _timesHit;

    public Vector3 position => transform.position;
    public int timesHit => timesHit;

    public void GetHit()
    {
        _timesHit++;
        GameManager.instance.ReceivePenalty(hitScorePenalty, false);
        ParticleManager.instance.ShowParticleEffect(hitParticlesIndex, position);
        hitSound.PlayOneShot(hitSource);
    }
}
