using UnityEngine;

[RequireComponent(typeof(Animator))]
public class CatFootsteps : MonoBehaviour
{
    [SerializeField]
    AudioSource footstepSource;

    [SerializeField]
    AudioClip[] footstepClips;

    [SerializeField]
    [Range(0f, 1f)]
    float footstepVolume = 0.3f;

    [SerializeField]
    [Tooltip("If true, footstep clips will play in a random order; if false, footsteps will play in order provided.")]
    bool randomiseFootstepClipOrder = false;

    int mostRecentClipIndex = -1;

    void Start()
    {
        if (footstepSource == null)
        {
            footstepSource = GetComponentInChildren<AudioSource>();
        }
    }

    // This function should be called by an animation event on the cat's walking animation clip
    public void AE_PlayFootstepAudio()
    {
        if (footstepSource == null || footstepClips == null || footstepClips.Length == 0) return;

        if (randomiseFootstepClipOrder)
        {
            mostRecentClipIndex = Random.Range(0, footstepClips.Length);
        }
        else
        {
            mostRecentClipIndex = (mostRecentClipIndex + 1) % footstepClips.Length;
        }

        footstepSource.PlayOneShot(footstepClips[mostRecentClipIndex], footstepVolume);
    }
}
