using System.Collections.Generic;
using IdleCore.Economy;
using IdleCore.Gacha;
using IdleCore.Progression;
using IdleCore.Stats;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace IdleCore.Data
{
    /// <summary>
    /// 게임 1개의 전체 설정 — Assets/GameData/config.json 하나로 로드된다.
    /// 리스킨 = 이 파일(+아트)을 교체하는 것.
    /// </summary>
    public sealed class GameConfig
    {
        public string gameId = "unnamed";
        public string gameName = "Unnamed Idle Game";

        public Dictionary<StatType, double> baseStats = new Dictionary<StatType, double>();
        public List<GrowthAxisDef> axes = new List<GrowthAxisDef>();
        public StageCurveConfig stage = new StageCurveConfig();
        public OfflineConfig offline = new OfflineConfig();
        public List<UnitDef> units = new List<UnitDef>();
        public List<BannerDef> banners = new List<BannerDef>();
        public List<ProductDef> products = new List<ProductDef>();
        public List<StoreSku> skus = new List<StoreSku>();
        public List<PassDef> passes = new List<PassDef>();
        public PaybackAttendanceDef paybackAttendance;
    }

    public static class GameConfigLoader
    {
        private static JsonSerializerSettings Settings => new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new StringEnumConverter() },
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static GameConfig FromJson(string json) =>
            JsonConvert.DeserializeObject<GameConfig>(json, Settings);

        public static string ToJson(GameConfig config) =>
            JsonConvert.SerializeObject(config, Formatting.Indented, Settings);
    }
}
