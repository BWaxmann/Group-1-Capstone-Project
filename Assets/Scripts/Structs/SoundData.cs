using System;
using UnityEngine;

[Serializable]
public struct SoundData
{
    [SerializeField]
    AudioClip _soundClip;

    [SerializeField]
    [Range(0f, 1f)]
    float _volume;

    [SerializeField]
    [Tooltip("Use a value of 1 for the default pitch.")]
    [Range(-3f, 3f)]
    float _pitch;

    public AudioClip soundClip { get { return _soundClip; } }
    public float volume { get { return _volume; } }
    public float pitch { get { return _pitch; } }

    public SoundData(float volume, float pitch)
    {
        _soundClip = null;
        _volume = volume;
        _pitch = pitch;
    }

    public void PlayOneShot(AudioSource source)
    {
        source.pitch = _pitch;
        source.PlayOneShot(soundClip, volume);
    }

    public void PlayMusic(AudioSource source)
    {
        source.volume = _volume;
        source.pitch = pitch;
        source.clip = _soundClip;
        source.Play();
    }
}
