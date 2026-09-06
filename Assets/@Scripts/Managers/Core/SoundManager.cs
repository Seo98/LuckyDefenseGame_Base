using System;
using UnityEngine;
using Object = UnityEngine.Object;

public class SoundManager
{
    private AudioSource[] _audioSources = new AudioSource[(int)SoundType.MaxCount];
    private GameObject _soundRoot = null;

    private float _bgmVolume = 1.0f;
    private float _sfxVolume = 1.0f;

    public void Init()
    {
        if (_soundRoot == null)
        {
            _soundRoot = GameObject.Find("@Sound");
            if (_soundRoot == null)
            {
                _soundRoot = new GameObject { name = "@Sound" };
                Object.DontDestroyOnLoad(_soundRoot);

                string[] soundNames = Enum.GetNames(typeof(SoundType));

                for (int i = 0; i < soundNames.Length - 1; i++)
                {
                    GameObject go = new GameObject { name = soundNames[i] };
                    _audioSources[i] = go.AddComponent<AudioSource>();
                    go.transform.parent = _soundRoot.transform;
                }

                _audioSources[(int)SoundType.Bgm].loop = true;
            }
        }
    }

    public void Clear()
    {
        foreach (AudioSource audioSource in _audioSources)
        {
            if (audioSource != null)
                audioSource.Stop();
        }
    }

    #region 재생 기능 (Play)

    public void Play(SoundType type, string key, float pitch = 1.0f)
    {
        Managers.Resource.LoadAsync<AudioClip>(key, (audioClip) =>
        {
            if (audioClip == null) return;
            Play(type, audioClip, pitch);
        });
    }

    public void Play(SoundType type, AudioClip audioClip, float pitch = 1.0f)
    {
        AudioSource audioSource = _audioSources[(int)type];
        audioSource.pitch = pitch;

        if (type == SoundType.Bgm)
        {
            // 이미 재생 중인 BGM과 같으면 무시 (끊김 방지)
            if (audioSource.isPlaying)
            {
                if (audioSource.clip == audioClip) return;
                audioSource.Stop();
            }

            audioSource.clip = audioClip;
            audioSource.volume = _bgmVolume;
            audioSource.Play();
        }
        else // Sfx
        {
            audioSource.PlayOneShot(audioClip, _sfxVolume);
        }
    }

    public void Play3D(string key, Vector3 position, float pitch = 1.0f)
    {
        Managers.Resource.LoadAsync<AudioClip>(key, (audioClip) =>
        {
            if (audioClip == null) return;
            AudioSource.PlayClipAtPoint(audioClip, position, _sfxVolume);
        });
    }

    #endregion

    #region 유틸리티 (Stop, Volume)

    public void Stop(SoundType type)
    {
        AudioSource audioSource = _audioSources[(int)type];
        if (audioSource != null)
            audioSource.Stop();
    }

    public void SetBgmVolume(float volume)
    {
        _bgmVolume = Mathf.Clamp01(volume);
        AudioSource bgmSource = _audioSources[(int)SoundType.Bgm];
        if (bgmSource != null)
        {
            bgmSource.volume = _bgmVolume;
        }
    }

    public void SetSfxVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
    }

    #endregion
}