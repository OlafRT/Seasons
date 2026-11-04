using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// Two-layer soundtrack for Tree Jam. Music loops all year; the ambience
    /// crossfades according to the EXISTING season / day-night simulation.
    /// Music fades out as soon as the lumberjack ending begins.
    ///
    /// Attach this to ONE scene GameObject, then drag in the eight AudioClips.
    /// Does not alter any other script or require an installer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreeJamSeasonAudio : MonoBehaviour
    {
        [Header("Existing game systems (auto-found if left blank)")]
        public TreeJamSeasonCycle seasons;
        public TreeJamDayNightCycle dayNight;
        public TreeJamEndgameLoop endgame;

        [Header("Background music - always loops")]
        public AudioClip music;
        [Range(0f, 1f)] public float musicVolume = .16f;

        [Header("Spring ambience")]
        public AudioClip springDay;
        public AudioClip springNight;

        [Header("Summer ambience")]
        public AudioClip summerDay;
        public AudioClip summerNight;

        [Header("Autumn ambience")]
        public AudioClip autumnDay;
        public AudioClip autumnNight;

        [Header("Winter ambience - used during both day and night")]
        public AudioClip winter;

        [Header("Ambience mixing")]
        [Range(0f, 1f)] public float ambienceVolume = .60f;
        [Min(.1f)] public float ambienceCrossfadeSeconds = 3f;

        [Header("Endgame: start fading when the lumberjack arrives")]
        [Tooltip("How long it takes the music to fade down when EndingPlaying begins.")]
        [Min(.1f)] public float lumberjackMusicFadeSeconds = 2.25f;
        [Tooltip("0 = complete music silence. 0.1 = keep music at 10% of its normal level.")]
        [Range(0f, 1f)] public float musicDuringEndingMultiplier = 0f;
        [Tooltip("1 = natural ambience continues through the ending. 0 = ambience fades to silence as well.")]
        [Range(0f, 1f)] public float ambienceDuringEndingMultiplier = 1f;

        [Header("Scene start")]
        [Min(.1f)] public float startingMusicFadeInSeconds = 2f;
        [Min(.1f)] public float startingAmbienceFadeInSeconds = 2f;

        private AudioSource musicSource;
        private AudioSource ambienceA;
        private AudioSource ambienceB;
        private AudioSource desiredAmbienceSource;
        private AudioClip requestedAmbience;
        private bool initialized;
        private bool lastEnding;

        private void Start()
        {
            FindSystems();
            if (seasons == null || dayNight == null)
            {
                Debug.LogWarning("Tree Jam Audio: Season Cycle or Day/Night Cycle is missing. Assign it in the Inspector.", this);
            }

            musicSource = CreateAudioSource("Music - looping");
            ambienceA = CreateAudioSource("Ambience - crossfade A");
            ambienceB = CreateAudioSource("Ambience - crossfade B");

            SetMusicClip();
            requestedAmbience = GetAmbienceClip();
            if (requestedAmbience != null)
            {
                ambienceA.clip = requestedAmbience;
                ambienceA.Play();
                desiredAmbienceSource = ambienceA;
            }
            lastEnding = endgame != null && endgame.EndingPlaying;
            initialized = true;
        }

        private void LateUpdate()
        {
            if (!initialized) return;

            // Read these in LateUpdate so the season and clock have both finished
            // updating for the current frame (including sunrise season changes).
            AudioClip clip = GetAmbienceClip();
            if (clip != requestedAmbience)
                RequestAmbience(clip);

            // Allow changing the music clip in the Inspector during Play Mode.
            if (musicSource.clip != music)
                SetMusicClip();

            bool ending = endgame != null && endgame.EndingPlaying;
            float dt = Time.unscaledDeltaTime;

            float musicGoal = musicVolume * (ending ? musicDuringEndingMultiplier : 1f);
            float musicFadeSeconds = ending ? lumberjackMusicFadeSeconds : startingMusicFadeInSeconds;
            if (musicSource.clip != null)
                musicSource.volume = Mathf.MoveTowards(musicSource.volume, musicGoal,
                    dt * Mathf.Max(.0001f, musicVolume) / Mathf.Max(.1f, musicFadeSeconds));

            float ambienceGoal = ambienceVolume * (ending ? ambienceDuringEndingMultiplier : 1f);
            // A full-volume ambience channel transitions to/from silence in the
            // specified seconds. Both sources update each frame to avoid hard cuts.
            float ambienceFadeSeconds = ambienceCrossfadeSeconds;
            if (!lastEnding && ambienceA.volume <= .0001f && ambienceB.volume <= .0001f)
                ambienceFadeSeconds = startingAmbienceFadeInSeconds;
            float rate = dt * Mathf.Max(.0001f, ambienceVolume) /
                         Mathf.Max(.1f, ambienceFadeSeconds);

            FadeAmbience(ambienceA, ambienceA == desiredAmbienceSource ? ambienceGoal : 0f, rate);
            FadeAmbience(ambienceB, ambienceB == desiredAmbienceSource ? ambienceGoal : 0f, rate);
            lastEnding = ending;
        }

        private void FindSystems()
        {
            if (seasons == null) seasons = FindFirstObjectByType<TreeJamSeasonCycle>();
            if (dayNight == null && seasons != null) dayNight = seasons.dayNight;
            if (dayNight == null) dayNight = FindFirstObjectByType<TreeJamDayNightCycle>();
            if (endgame == null) endgame = FindFirstObjectByType<TreeJamEndgameLoop>();
        }

        private AudioClip GetAmbienceClip()
        {
            if (seasons == null) return null;
            bool isDay = dayNight == null || dayNight.IsDaytime;
            switch (seasons.CurrentSeason)
            {
                case TreeSeason.Spring: return isDay ? springDay : springNight;
                case TreeSeason.Summer: return isDay ? summerDay : summerNight;
                case TreeSeason.Autumn: return isDay ? autumnDay : autumnNight;
                case TreeSeason.Winter: return winter;
                default: return null;
            }
        }

        private void RequestAmbience(AudioClip clip)
        {
            requestedAmbience = clip;
            if (clip == null)
            {
                desiredAmbienceSource = null;
                return; // Both currently playing sources fade away smoothly.
            }

            // A rapid day/night change can return to a clip which is still
            // fading out; reuse it rather than starting another copy.
            if (ambienceA.clip == clip && ambienceA.isPlaying)
            {
                desiredAmbienceSource = ambienceA;
                return;
            }
            if (ambienceB.clip == clip && ambienceB.isPlaying)
            {
                desiredAmbienceSource = ambienceB;
                return;
            }

            // Replace the quieter channel to minimize any discontinuity if
            // the user fast-forwards through several transitions.
            AudioSource incoming = ambienceA.volume <= ambienceB.volume ? ambienceA : ambienceB;
            incoming.Stop();
            incoming.clip = clip;
            incoming.volume = 0f;
            incoming.Play();
            desiredAmbienceSource = incoming;
        }

        private static void FadeAmbience(AudioSource source, float goal, float amount)
        {
            if (source == null) return;
            source.volume = Mathf.MoveTowards(source.volume, goal, amount);
            if (goal <= 0f && source.volume <= .0001f && source.isPlaying)
            {
                source.Stop();
                source.clip = null;
            }
        }

        private void SetMusicClip()
        {
            if (musicSource == null) return;
            musicSource.Stop();
            musicSource.clip = music;
            musicSource.volume = 0f;
            if (music != null) musicSource.Play();
        }

        private AudioSource CreateAudioSource(string childName)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f; // 2D sound: independent of camera position.
            source.volume = 0f;
            return source;
        }
    }
}
