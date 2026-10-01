using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioController : MonoBehaviour
{
    public AudioSource audioSourceMusicaDeFundo;
    public AudioClip musicaDeFundo, combatMusic;
    public static AudioController AudioInstance;

    private bool isCombatMusicPlaying = false;

    void Awake()
    {
        if (AudioInstance == null)
            AudioInstance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        PlayBackgroundMusic();
    }

    void Update()
    {
        // Se a música de combate terminou, volte para a música de fundo
        if (isCombatMusicPlaying && audioSourceMusicaDeFundo != null && !audioSourceMusicaDeFundo.isPlaying)
        {
            PlayBackgroundMusic();
        }
    }

    public void PlayCombatMusic()
    {
        if (audioSourceMusicaDeFundo == null || combatMusic == null) return;
        isCombatMusicPlaying = true;
        audioSourceMusicaDeFundo.clip = combatMusic;
        audioSourceMusicaDeFundo.loop = false; // Música de combate geralmente não deve ser repetida
        audioSourceMusicaDeFundo.Play();
    }

    public void PlayBackgroundMusic()
    {
        if (audioSourceMusicaDeFundo == null || musicaDeFundo == null) return;
        isCombatMusicPlaying = false;
        audioSourceMusicaDeFundo.clip = musicaDeFundo;
        audioSourceMusicaDeFundo.loop = true; // Música de fundo deve repetir continuamente
        audioSourceMusicaDeFundo.Play();
    }

    public void SetVolume(float volume)
    {
        if (audioSourceMusicaDeFundo != null) audioSourceMusicaDeFundo.volume = Mathf.Clamp01(volume);
    }

    public void SetVolum(float volume)
    {
        SetVolume(volume);
    }

    void OnDestroy()
    {
        if (AudioInstance == this) AudioInstance = null;
    }

}
