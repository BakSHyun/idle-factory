using System.Collections.Generic;
using IdleCore;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 사운드 총괄 — 효과음(Resources/Sfx)·BGM(Resources/Bgm) 재생.
    /// 게임 이벤트에 자동 배선: 버튼 클릭/소환/강화/보스 클리어. 이름별 스로틀로 도배 방지.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;
        private AudioSource _sfxSource, _bgmSource;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();

        public static void Ensure(GameSession session)
        {
            if (_instance != null) Destroy(_instance.gameObject);
            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AudioManager>();
            _instance._sfxSource = go.AddComponent<AudioSource>();
            _instance._bgmSource = go.AddComponent<AudioSource>();

            // BGM 루프
            var bgm = Resources.Load<AudioClip>("Bgm/main_theme");
            if (bgm != null)
            {
                _instance._bgmSource.clip = bgm;
                _instance._bgmSource.loop = true;
                _instance._bgmSource.volume = 0.30f;
                _instance._bgmSource.Play();
            }

            // 이벤트 배선
            UI.UIFactory.OnAnyButtonClick = () => Play("click", 0.5f);
            session.Gacha.Pulled += (_, _) => Play("summon", 0.8f);
            session.Stats.LeveledUp += _ => Play("levelup", 0.5f);
            session.Units.LeveledUp += _ => Play("levelup", 0.5f);
            session.Progression.StageCleared += _ => Play("victory", 0.7f);
        }

        /// <summary>이름으로 효과음 재생 (0.12초 스로틀 — 연타 도배 방지).</summary>
        public static void Play(string name, float volume = 1f)
        {
            if (_instance == null) return;
            if (_instance._lastPlayed.TryGetValue(name, out var last) && Time.unscaledTime - last < 0.12f) return;
            _instance._lastPlayed[name] = Time.unscaledTime;

            if (!_instance._clips.TryGetValue(name, out var clip))
            {
                clip = Resources.Load<AudioClip>("Sfx/" + name);
                _instance._clips[name] = clip;
            }
            if (clip != null) _instance._sfxSource.PlayOneShot(clip, volume);
        }
    }
}
