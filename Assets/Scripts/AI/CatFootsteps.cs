using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class CatFootsteps : MonoBehaviour
{
    [SerializeField]
    AudioSource footstepSource;

    [SerializeField]
    SoundData[] footstepClips;

    public AudioSource source
    {
        get
        {
            return footstepSource;
        }
    }

    [SerializeField]
    [Tooltip("If true, footstep clips will play in a random order; if false, footsteps will play in order provided.")]
    bool randomiseFootstepClipOrder = false;

    [SerializeField]
    [Tooltip("If false, you will need to call animation events on the cat's walking animation for the footstep sounds to play.")]
    bool footstepSoundIndependentOfAnimation = false;

    [SerializeField]
    [Tooltip("This only applies if footsteps are to be independent of animation.")]
    float footstepFrequency = 2.376f;

    int mostRecentClipIndex = -1;
    CatMovement parent;

    void Start()
    {
        if (footstepSource == null)
        {
            footstepSource = GetComponentInChildren<AudioSource>();
        }

        if (footstepSoundIndependentOfAnimation)
        {
            parent = GetComponentInParent<CatMovement>();
            StartCoroutine(FootstepRoutine());
        }
    }

    // This function should be called by an animation event on the cat's walking animation clip
    public void AE_PlayFootstepAudio()
    {
        if (footstepSource == null || footstepClips == null || footstepClips.Length == 0) return;

        PlayFootstepSound();
    }

    IEnumerator FootstepRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(footstepFrequency);
        while (true)
        {
            if (gameObject.activeInHierarchy && parent.isMoving)
            {
                PlayFootstepSound();
                yield return wait;
            }
            else
            {
                yield return null;
            }
        }
    }

    void PlayFootstepSound()
    {
        if (randomiseFootstepClipOrder)
        {
            mostRecentClipIndex = Random.Range(0, footstepClips.Length);
        }
        else
        {
            mostRecentClipIndex = (mostRecentClipIndex + 1) % footstepClips.Length;
        }

        footstepClips[mostRecentClipIndex].PlayOneShot(footstepSource);
    }
}
