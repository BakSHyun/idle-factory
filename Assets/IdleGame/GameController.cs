using System.IO;
using IdleCore;
using IdleCore.Data;
using IdleCore.Economy;
using IdleCore.Progression;
using IdleCore.Save;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 게임의 유일한 진입점. 씬 편집 없이 코드로 전부 조립한다 (양산 원칙: 에디터 작업 최소화).
    /// 빈 씬에서 자동 부트스트랩 → config.json 로드 → GameSession 구동 → UI 생성.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        public static GameController Instance { get; private set; }
        public GameSession Session { get; private set; }

        private float _tickAccumulator;
        private float _saveAccumulator;
        private UI.RootView _ui;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("GameController");
            DontDestroyOnLoad(go);
            go.AddComponent<GameController>();
        }

        private void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;

            string configPath = Path.Combine(Application.streamingAssetsPath, "config.json");
            var config = GameConfigLoader.FromJson(File.ReadAllText(configPath));

            string savePath = Path.Combine(Application.persistentDataPath, $"save_{config.gameId}.json");
            Session = new GameSession(
                config,
                new FileSaveStore(savePath),
                new SystemClock(),
                new SeededRng(unchecked((int)System.DateTime.UtcNow.Ticks)));

            var offline = Session.ClaimOfflineReward();
            _ui = UI.RootView.Create(Session);
            if (offline.CreditedHours > 0.01)
                _ui.ShowOfflinePopup(offline);
        }

        private void Update()
        {
            // 플레이 중 스크립트 리로드로 상태 유실 → 전체 재부팅 (세이브는 파일에 있음)
            if (Session == null || _ui == null)
            {
                var stale = GameObject.Find("GameCanvas");
                if (stale != null) Destroy(stale);
                Instance = null;
                Destroy(gameObject);
                Bootstrap();
                return;
            }

            _tickAccumulator += Time.deltaTime;
            if (_tickAccumulator >= 1f)
            {
                var result = Session.Tick(_tickAccumulator);
                _tickAccumulator = 0f;
                _ui.OnFarmTick(result);
            }

            _saveAccumulator += Time.deltaTime;
            if (_saveAccumulator >= 30f)
            {
                _saveAccumulator = 0f;
                Session.Save();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Session?.Save();
            else if (Session != null)
            {
                var offline = Session.ClaimOfflineReward();
                if (offline.CreditedHours > 0.01) _ui.ShowOfflinePopup(offline);
            }
        }

        private void OnApplicationQuit() => Session?.Save();
    }
}
