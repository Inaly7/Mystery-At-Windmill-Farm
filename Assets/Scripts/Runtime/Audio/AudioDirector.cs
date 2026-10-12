using System.Collections.Generic;
using UnityEngine;

namespace EggRescue
{
    public sealed class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        [SerializeField] AudioClip[] audioClips;
        [SerializeField] AudioSource sfxSource;
        [SerializeField] AudioSource bgmSource;
        [SerializeField] ParticleSystem[] particleSystems;

        AudioClip _originalBgm;
        bool _adoptedSceneBgm;
        readonly Dictionary<string, AudioClip> _byName = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, ParticleSystem> _spawnedParticles = new Dictionary<string, ParticleSystem>();

        void Awake()
        {
            Instance = this;
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
            if (bgmSource == null) bgmSource = AdoptSceneBgmSource();
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
            }
            RebuildMap();
            LoadEditorClipsIfNeeded();
            if (bgmSource.clip != null) _originalBgm = bgmSource.clip;
        }

        /// <summary>
        /// ???????????????? BGM AudioSource?
        /// ???????? AudioDirector ???????? BGM ????
        /// </summary>
        AudioSource AdoptSceneBgmSource()
        {
            var sources = FindObjectsOfType<AudioSource>(true);
            AudioSource best = null;
            for (var i = 0; i < sources.Length; i++)
            {
                var src = sources[i];
                if (src == null || src == sfxSource) continue;
                if (src.gameObject == gameObject) continue;
                // BGM ?????? + ????(????) + 2D?
                if (!src.loop) continue;
                if (!src.playOnAwake && !src.isPlaying) continue;
                if (src.spatialBlend > 0.5f) continue;
                if (best == null) best = src;
                else src.Stop(); // ?????????????????????
            }
            if (best != null)
            {
                _adoptedSceneBgm = true;
                Debug.Log("[AudioDirector] adopted scene BGM source: " + best.gameObject.name);
            }
            return best;
        }

        void Start()
        {
            // ????? BGM ?????????????? audio_bgm ???????????
            if (_adoptedSceneBgm && bgmSource != null && bgmSource.clip != null)
            {
                if (!bgmSource.isPlaying)
                {
                    bgmSource.loop = true;
                    bgmSource.Play();
                }
                return;
            }
            if (_byName.ContainsKey("audio_bgm"))
                PlayBGM();
            else if (_byName.ContainsKey("bgm"))
            {
                bgmSource.clip = _byName["bgm"];
                bgmSource.loop = true;
                bgmSource.Play();
                _originalBgm = bgmSource.clip;
            }
        }

        public void SetClips(AudioClip[] clips)
        {
            audioClips = clips;
            RebuildMap();
        }

        public void SetParticles(ParticleSystem[] systems)
        {
            particleSystems = systems;
        }

        void RebuildMap()
        {
            _byName.Clear();
            if (audioClips == null) return;
            for (var i = 0; i < audioClips.Length; i++)
            {
                var clip = audioClips[i];
                if (clip != null && !_byName.ContainsKey(clip.name))
                    _byName[clip.name] = clip;
            }
        }

        void LoadEditorClipsIfNeeded()
        {
            if (_byName.Count > 0) return;
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Res/AudioAssets" });
            var list = new List<AudioClip>();
            for (var i = 0; i < (guids == null ? 0 : guids.Length); i++)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) list.Add(clip);
            }
            audioClips = list.ToArray();
            RebuildMap();
#endif
        }

        public static void PlayAudio(string name)
        {
            if (Instance != null) Instance.PlayOneShot(name);
        }

        public static void PlayBGM()
        {
            if (Instance == null) return;
            if (Instance._originalBgm != null)
                Instance.bgmSource.clip = Instance._originalBgm;
            else
            {
                AudioClip clip;
                if (Instance._byName.TryGetValue("audio_bgm", out clip) || Instance._byName.TryGetValue("bgm", out clip))
                    Instance.bgmSource.clip = clip;
            }
            Instance.bgmSource.loop = true;
            if (Instance.bgmSource.clip != null)
                Instance.bgmSource.Play();
        }

        public static void StopBGM()
        {
            if (Instance != null && Instance.bgmSource != null)
                Instance.bgmSource.Stop();
        }

        public static void PlayMusic(string name)
        {
            if (Instance == null) return;
            AudioClip clip;
            if (!Instance._byName.TryGetValue(name ?? "", out clip) || clip == null)
            {
                Debug.Log("[AudioDirector] missing music " + name);
                return;
            }
            Instance.bgmSource.Stop();
            Instance.bgmSource.clip = clip;
            Instance.bgmSource.loop = true;
            Instance.bgmSource.Play();
        }

        public static float GetClipLength(string name)
        {
            if (Instance == null) return 0f;
            AudioClip clip;
            if (!Instance._byName.TryGetValue(name ?? "", out clip) || clip == null) return 0f;
            return clip.length;
        }

        public static void PlayParticle(string name, Vector3 position)
        {
            if (Instance == null || string.IsNullOrEmpty(name)) return;
            var ps = Instance.ResolveParticle(name, position);
            if (ps == null) return;
            ps.transform.position = position;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(true);
        }

        ParticleSystem ResolveParticle(string name, Vector3 position)
        {
            ParticleSystem found;
            if (_spawnedParticles.TryGetValue(name, out found) && found != null)
                return found;

            if (particleSystems != null)
            {
                for (var i = 0; i < particleSystems.Length; i++)
                {
                    var ps = particleSystems[i];
                    if (ps == null || ps.name != name) continue;
                    _spawnedParticles[name] = ps;
                    return ps;
                }
            }

            var prefab = LoadParticlePrefab(name);
            if (prefab == null)
            {
                Debug.LogWarning("[AudioDirector] missing particle " + name);
                return null;
            }

            var go = Instantiate(prefab, position, Quaternion.identity, transform);
            go.name = name;
            var spawned = go.GetComponent<ParticleSystem>();
            if (spawned == null) spawned = go.GetComponentInChildren<ParticleSystem>(true);
            if (spawned == null)
            {
                Debug.LogWarning("[AudioDirector] particle prefab has no ParticleSystem " + name);
                return null;
            }
            _spawnedParticles[name] = spawned;
            return spawned;
        }

        static GameObject LoadParticlePrefab(string name)
        {
            var catalog = Resources.Load<VfxCatalog>("VfxCatalog");
            if (catalog != null && catalog.Prefabs != null)
            {
                for (var i = 0; i < catalog.Prefabs.Length; i++)
                {
                    var prefab = catalog.Prefabs[i];
                    if (prefab != null && prefab.name == name) return prefab;
                }
            }
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/vfx/prefabs/" + name + ".prefab");
#else
            return null;
#endif
        }

        void PlayOneShot(string name)
        {
            AudioClip clip;
            if (!_byName.TryGetValue(name ?? "", out clip) || clip == null)
            {
                Debug.Log("[AudioDirector] missing clip " + name);
                return;
            }
            sfxSource.PlayOneShot(clip);
        }
    }
}
