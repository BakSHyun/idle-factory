using System;
using System.Collections.Generic;
using IdleCore.Data;
using IdleCore.Economy;
using IdleCore.Gacha;
using IdleCore.Progression;
using IdleCore.Save;
using IdleCore.Stats;
using Newtonsoft.Json;

namespace IdleCore
{
    /// <summary>
    /// 게임 1판의 파사드 — 설정(GameConfig)과 세이브(SaveData)로 모든 시스템을 조립한다.
    /// Unity 뷰와 헤드리스 시뮬레이터가 이 클래스를 동일하게 사용한다.
    /// </summary>
    public sealed class GameSession
    {
        public GameConfig Config { get; }
        public IClock Clock { get; }
        public Wallet Wallet { get; }
        public StatSystem Stats { get; }
        public ProgressionSystem Progression { get; }
        public UnitInventory Units { get; }
        public GachaSystem Gacha { get; }
        public ShopSystem Shop { get; }
        public PaybackAttendanceSystem PaybackAttendance { get; }
        public Dictionary<string, PassSystem> Passes { get; } = new Dictionary<string, PassSystem>();
        public SubscriptionSystem Subscriptions { get; }
        public AdGateway Ads { get; }
        public DungeonSystem Dungeons { get; }

        private readonly ISaveStore _saveStore;
        private DateTime _firstPlayedUtc;

        public GameSession(GameConfig config, ISaveStore saveStore, IClock clock, IRng rng, IAdAdapter adAdapter = null)
        {
            Config = config;
            Clock = clock;
            _saveStore = saveStore;

            var save = LoadOrCreate();

            Wallet = new Wallet();
            Wallet.Import(save.balances, save.lifetimeEarned, save.lifetimeSpent);

            Stats = new StatSystem(config.axes, config.baseStats);
            Stats.ImportLevels(save.axisLevels);

            Progression = new ProgressionSystem(config.stage, Stats, Wallet, save.currentStageIndex, save.highestClearedIndex);

            Units = new UnitInventory(config.units, config.equipSlots, config.collectionMilestones);
            Units.Import(save.units);
            Units.UnitChanged += _ => SyncExternalEffects();

            Gacha = new GachaSystem(config.banners, Units, Wallet, rng);
            Gacha.ImportPity(save.gachaPity);
            Gacha.ImportPulls(save.gachaPulls);

            Shop = new ShopSystem(config.products, Wallet, clock, Units);
            Shop.ImportHistory(save.purchaseHistory);

            if (config.paybackAttendance != null)
                PaybackAttendance = new PaybackAttendanceSystem(config.paybackAttendance, Wallet, clock, save.paybackAttendance);

            foreach (var passDef in config.passes)
            {
                save.passes.TryGetValue(passDef.id, out var passState);
                Passes[passDef.id] = new PassSystem(passDef, Wallet, clock, passState);
            }

            Dungeons = new DungeonSystem(config.dungeons, Wallet, Stats, clock);
            Dungeons.Import(save.dungeons);

            Subscriptions = new SubscriptionSystem(config.subscriptions, Wallet, clock);
            Subscriptions.Import(save.subscriptions);
            Subscriptions.SubscriptionChanged += _ => SyncExternalEffects();

            Ads = new AdGateway(config.adSlots, adAdapter ?? new FakeAdAdapter(), Subscriptions, clock);
            Ads.Import(save.adSlotUses, save.adSlotDate == default ? clock.UtcNow.Date : save.adSlotDate);

            _firstPlayedUtc = save.firstPlayedUtc == default ? clock.UtcNow : save.firstPlayedUtc;
            LastSeenUtc = save.lastSeenUtc;
            SyncExternalEffects();
        }

        public DateTime LastSeenUtc { get; private set; }

        private void SyncExternalEffects()
        {
            var effects = Units.ContributeEffects();
            if (Subscriptions != null) effects.AddRange(Subscriptions.ContributeEffects());
            Stats.SetExternalEffects(effects);
        }

        /// <summary>앱 복귀 시 호출: 오프라인 보상을 계산·지급하고 결과를 반환한다.</summary>
        public OfflineReward ClaimOfflineReward()
        {
            if (LastSeenUtc == default) { LastSeenUtc = Clock.UtcNow; return new OfflineReward(); }
            var elapsed = Clock.UtcNow - LastSeenUtc;
            var reward = OfflineRewardCalculator.Calculate(
                Config.offline, Config.stage, Stats.Snapshot(), Progression.Current.Index, elapsed);
            Wallet.Earn(CurrencyIds.Gold, reward.Gold);
            Wallet.Earn(CurrencyIds.Soul, reward.Soul);
            LastSeenUtc = Clock.UtcNow;
            return reward;
        }

        /// <summary>온라인 파밍 틱. 뷰의 프레임/초 단위 어디서 불러도 된다 (수식 기반이라 주기 무관).</summary>
        public FarmResult Tick(double seconds)
        {
            var result = Progression.Advance(seconds);
            LastSeenUtc = Clock.UtcNow;
            return result;
        }

        public void Save()
        {
            var data = new SaveData
            {
                lastSeenUtc = LastSeenUtc == default ? Clock.UtcNow : LastSeenUtc,
                firstPlayedUtc = _firstPlayedUtc,
                balances = Wallet.ExportBalances(),
                lifetimeEarned = Wallet.ExportLifetimeEarned(),
                lifetimeSpent = Wallet.ExportLifetimeSpent(),
                purchaseHistory = Shop.ExportHistory(),
                axisLevels = Stats.ExportLevels(),
                currentStageIndex = Progression.Current.Index,
                highestClearedIndex = Progression.HighestClearedIndex,
                units = Units.Export(),
                gachaPity = Gacha.ExportPity(),
                gachaPulls = Gacha.ExportPulls(),
                paybackAttendance = PaybackAttendance?.State,
                subscriptions = Subscriptions.Export(),
                dungeons = Dungeons.Export(),
                adSlotUses = Ads.ExportTodayUses(),
                adSlotDate = Ads.ExportTodayDate(),
            };
            foreach (var kv in Passes) data.passes[kv.Key] = kv.Value.State;
            _saveStore.Store(JsonConvert.SerializeObject(data));
        }

        private SaveData LoadOrCreate()
        {
            var json = _saveStore.Load();
            if (string.IsNullOrEmpty(json)) return new SaveData();
            var data = JsonConvert.DeserializeObject<SaveData>(json);
            return SaveMigrator.Migrate(data);
        }
    }
}
