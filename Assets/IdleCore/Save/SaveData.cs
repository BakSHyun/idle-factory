using System;
using System.Collections.Generic;
using IdleCore.Economy;
using IdleCore.Gacha;

namespace IdleCore.Save
{
    /// <summary>
    /// 저장 스키마. 로컬이 진실(오프라인 우선) — 클라우드 세이브는 ICloudSave 어댑터로 나중에.
    /// 필드 추가 시 schemaVersion을 올리고 SaveMigrator에 마이그레이션을 추가한다.
    /// </summary>
    public sealed class SaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;

        // 시간
        public DateTime lastSeenUtc;
        public DateTime firstPlayedUtc;

        // 경제
        public Dictionary<string, long> balances = new Dictionary<string, long>();
        public Dictionary<string, long> lifetimeEarned = new Dictionary<string, long>();
        public Dictionary<string, long> lifetimeSpent = new Dictionary<string, long>();
        public List<PurchaseRecord> purchaseHistory = new List<PurchaseRecord>();

        // 성장
        public Dictionary<string, int> axisLevels = new Dictionary<string, int>();
        public int currentStageIndex;
        public int highestClearedIndex = -1;

        // 수집
        public Dictionary<string, OwnedUnit> units = new Dictionary<string, OwnedUnit>();
        public Dictionary<string, int> gachaPity = new Dictionary<string, int>();

        // BM
        public PaybackAttendanceState paybackAttendance;
        public Dictionary<string, PassState> passes = new Dictionary<string, PassState>();
        public Dictionary<string, SubscriptionState> subscriptions = new Dictionary<string, SubscriptionState>();
        public Dictionary<string, int> adSlotUses = new Dictionary<string, int>();
        public DateTime adSlotDate;
    }

    public interface ISaveStore
    {
        string Load();          // 없으면 null
        void Store(string json);
    }

    public sealed class InMemorySaveStore : ISaveStore
    {
        private string _json;
        public string Load() => _json;
        public void Store(string json) => _json = json;
    }

    public sealed class FileSaveStore : ISaveStore
    {
        private readonly string _path;
        public FileSaveStore(string path) => _path = path;
        public string Load() => System.IO.File.Exists(_path) ? System.IO.File.ReadAllText(_path) : null;
        public void Store(string json)
        {
            var dir = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            // 원자적 쓰기: 임시 파일 후 교체 (저장 중 크래시로 세이브 파손 방지)
            var tmp = _path + ".tmp";
            System.IO.File.WriteAllText(tmp, json);
            if (System.IO.File.Exists(_path)) System.IO.File.Delete(_path);
            System.IO.File.Move(tmp, _path);
        }
    }

    /// <summary>미래의 뒤끝/Firebase 클라우드 세이브 어댑터 자리.</summary>
    public interface ICloudSave
    {
        void Upload(string json, Action<bool> onComplete);
        void Download(Action<string> onComplete);
    }

    public static class SaveMigrator
    {
        /// <summary>구버전 세이브를 현재 스키마로 끌어올린다.</summary>
        public static SaveData Migrate(SaveData data)
        {
            if (data.schemaVersion > SaveData.CurrentSchemaVersion)
                throw new InvalidOperationException($"save from future schema v{data.schemaVersion}");
            // v1 → v2 마이그레이션이 생기면 여기에 체인 추가
            data.schemaVersion = SaveData.CurrentSchemaVersion;
            return data;
        }
    }
}
