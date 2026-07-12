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
        public LiveOps.MissionSystem Missions { get; }
        public LiveOps.AttendanceSystem Attendance { get; }

        private readonly ISaveStore _saveStore;
        private DateTime _firstPlayedUtc;

        public GameSession(GameConfig config, ISaveStore saveStore, IClock clock, IRng rng, IAdAdapter adAdapter = null)
        {
            Config = config;
            Clock = clock;
            _saveStore = saveStore;

            var save = LoadOrCreate(out bool isNewSave);

            Wallet = new Wallet();
            Wallet.Import(save.balances, save.lifetimeEarned, save.lifetimeSpent);
            if (isNewSave)
                foreach (var kv in config.startingBalances)
                    Wallet.Earn(kv.Key, kv.Value);

            Stats = new StatSystem(config.axes, config.baseStats);
            Stats.ImportLevels(save.axisLevels);

            Progression = new ProgressionSystem(config.stage, Stats, Wallet, save.currentStageIndex, save.highestClearedIndex)
            {
                KillsOnStage = save.killsOnStage,
            };

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

            Missions = new LiveOps.MissionSystem(config.dailyMissions, Wallet, clock);
            Missions.Import(save.missions);
            Attendance = new LiveOps.AttendanceSystem(config.attendanceDays, Wallet, clock, save.attendance);
            // 영옥 획득량 스탯 (장식 보유 효과) → 미션/출석 보상에 반영
            System.Func<string, double> softGemScale = currency =>
                currency == CurrencyIds.GemSoft ? 1 + System.Math.Max(0, Stats.Snapshot().Get(StatType.SoftGemGain)) : 1.0;
            Missions.RewardMultiplier = softGemScale;
            Attendance.RewardMultiplier = softGemScale;

            // 미션 메트릭 배선 — 시스템 이벤트가 자동 집계된다
            Gacha.Pulled += (_, count) => Missions.Report("summon", count);
            Dungeons.Challenged += _ => Missions.Report("dungeon", 1);
            Stats.LeveledUp += _ => Missions.Report("levelup", 1);
            Units.LeveledUp += _ => Missions.Report("levelup", 1);
            Missions.Report("login", 1);

            _firstPlayedUtc = save.firstPlayedUtc == default ? clock.UtcNow : save.firstPlayedUtc;
            LastSeenUtc = save.lastSeenUtc;
            SyncExternalEffects();
        }

        public DateTime LastSeenUtc { get; private set; }

        private static StatType ElementDamageStat(string element) => element switch
        {
            Elements.Fire => StatType.FireDamage,
            Elements.Lightning => StatType.LightningDamage,
            _ => StatType.DarkDamage,
        };

        /// <summary>
        /// 파티 속성 보너스 = 상성(유리 +6%/불리 -4% per 유닛) + 속성 공격력 스탯(불꽃/뇌전/명계 연마 축).
        /// 속성 스탯은 파티 내 해당 속성 유닛 비중만큼 반영된다.
        /// </summary>
        public double ElementAdvantage()
        {
            string mobElement = Elements.MobElement(Progression.Current.Chapter(Config.stage));
            var snapshot = Stats.Snapshot();
            double advantage = 0;
            int totalElemental = 0;
            var counts = new Dictionary<string, int>();
            foreach (var unit in Units.AllOwned())
            {
                if (!unit.equipped) continue;
                var def = Units.Defs[unit.unitId];
                if (string.IsNullOrEmpty(def.element)) continue;
                totalElemental++;
                counts.TryGetValue(def.element, out var c);
                counts[def.element] = c + 1;
                if (Elements.Beats(def.element, mobElement)) advantage += 0.06;
                else if (Elements.Beats(mobElement, def.element)) advantage -= 0.04;
            }
            if (totalElemental > 0)
                foreach (var kv in counts)
                    advantage += snapshot.Get(ElementDamageStat(kv.Key)) * kv.Value / totalElemental;
            advantage += snapshot.Get(StatType.AllElementDamage); // 오브: 모든 속성 피해
            return advantage;
        }

        private int _lastElementChapter = -1;

        private void SyncExternalEffects()
        {
            var effects = Units.ContributeEffects();
            if (Subscriptions != null) effects.AddRange(Subscriptions.ContributeEffects());

            // 속성 상성 → 최종 데미지 배율로 주입
            double advantage = ElementAdvantage();
            if (System.Math.Abs(advantage) > 0.0001)
                effects.Add(new StatEffect
                {
                    stat = StatType.FinalDamage,
                    mode = EffectMode.Mul,
                    value = new ValueCurve { baseValue = advantage },
                });
            _lastElementChapter = Progression.Current.Chapter(Config.stage);
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
            if (reward.CreditedHours > 0.01) Missions.Report("offline_claim", 1);
            return reward;
        }

        /// <summary>온라인 파밍 틱. 뷰의 프레임/초 단위 어디서 불러도 된다 (수식 기반이라 주기 무관).</summary>
        /// <summary>스킬 자동 시전 시 (unitId) — UI 연출/사운드용</summary>
        public event Action<string> SkillCast;
        private readonly Dictionary<string, double> _skillTimers = new Dictionary<string, double>();
        private readonly Dictionary<string, double> _procAccumulators = new Dictionary<string, double>();

        public double SkillCooldownRemaining(string unitId) =>
            _skillTimers.TryGetValue(unitId, out var t) ? Math.Max(0, t) : 0;

        /// <summary>돌파 마일스톤이 반영된 실효 스킬 수치 (UI 표시와 실제 계산의 단일 출처).</summary>
        public (double cooldown, double procChance, double damageSeconds, int hits, double statusChance)
            EffectiveSkill(string unitId)
        {
            var def = Units.Defs[unitId];
            var spec = def.activeSkill;
            var unit = Units.Get(unitId);
            int lb = unit?.limitBreak ?? 0;

            double cdReduction = 0, damageBonus = 0, chanceBonus = 0;
            int extraHits = 0;
            foreach (var mod in def.skillMilestones)
            {
                if (lb < mod.atLimitBreak) continue;
                cdReduction += mod.cooldownReduction;
                damageBonus += mod.damageBonus;
                chanceBonus += mod.chanceBonus;
                extraHits += mod.extraHits;
            }

            // 스킬 피해량 스탯: 차사·스킬 유닛의 보유 효과가 서로의 스킬을 강화
            double heroBonus = Math.Max(0, Stats.Snapshot().Get(StatType.HeroSkillDamage));
            int hits = spec.hitCount + extraHits;
            double damageSeconds = spec.damagePerHit * hits * (1 + damageBonus) * (1 + heroBonus);
            double cooldown = Math.Max(1.0, spec.cooldown - cdReduction);
            double procChance = spec.trigger == "proc"
                ? Math.Min(0.8, spec.procChance + chanceBonus) : 0;
            double statusChance = spec.statusEffect != null
                ? Math.Min(1.0, spec.statusChance + (spec.trigger == "proc" ? 0 : chanceBonus)) : 0;
            return (cooldown, procChance, damageSeconds, hits, statusChance);
        }

        public FarmResult Tick(double seconds)
        {
            // 보스 도전은 UI가 전투 연출과 함께 수동/자동 트리거한다 (자동 즉시 돌파 없음)
            var result = Progression.Advance(seconds, autoPush: false);

            // 장착 스킬 자동 시전 (쿨타임형: N초마다 / proc형: 평타당 확률 — 기대값 누적, 결정론)
            double attackSpeed = Math.Max(0.1, Stats.Snapshot().Get(StatType.AttackSpeed));
            foreach (var unit in Units.AllOwned())
            {
                if (!unit.equipped) continue;
                var def = Units.Defs[unit.unitId];
                if (def.activeSkill == null) continue;
                var skill = EffectiveSkill(unit.unitId);
                int casts = 0;

                if (def.activeSkill.trigger == "proc")
                {
                    _procAccumulators.TryGetValue(unit.unitId, out var accum);
                    accum += attackSpeed * seconds * skill.procChance;
                    while (accum >= 1) { accum -= 1; casts++; }
                    _procAccumulators[unit.unitId] = accum;
                }
                else
                {
                    _skillTimers.TryGetValue(unit.unitId, out var timer);
                    timer -= seconds;
                    while (timer <= 0) { timer += skill.cooldown; casts++; }
                    _skillTimers[unit.unitId] = timer;
                }

                for (int c = 0; c < casts && c < 20; c++)
                {
                    // 스킬 피해 + 상태이상 기대 피해(부여 확률 × 1초어치)
                    double burstSeconds = skill.damageSeconds + skill.statusChance * 1.0;
                    var burst = Progression.Advance(burstSeconds, autoPush: false);
                    result.Kills += burst.Kills;
                    result.Gold += burst.Gold;
                    result.Soul += burst.Soul;
                    SkillCast?.Invoke(unit.unitId);
                }
            }

            // 챕터가 바뀌면 몬스터 속성이 바뀜 → 상성 재계산
            if (Progression.Current.Chapter(Config.stage) != _lastElementChapter)
                SyncExternalEffects();

            LastSeenUtc = Clock.UtcNow;
            if (result.Kills > 0) Missions.Report("kill", result.Kills);
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
                killsOnStage = Progression.KillsOnStage,
                units = Units.Export(),
                gachaPity = Gacha.ExportPity(),
                gachaPulls = Gacha.ExportPulls(),
                paybackAttendance = PaybackAttendance?.State,
                subscriptions = Subscriptions.Export(),
                dungeons = Dungeons.Export(),
                missions = Missions.Export(),
                attendance = Attendance.State,
                adSlotUses = Ads.ExportTodayUses(),
                adSlotDate = Ads.ExportTodayDate(),
            };
            foreach (var kv in Passes) data.passes[kv.Key] = kv.Value.State;
            _saveStore.Store(JsonConvert.SerializeObject(data));
        }

        private SaveData LoadOrCreate(out bool isNewSave)
        {
            var json = _saveStore.Load();
            isNewSave = string.IsNullOrEmpty(json);
            if (isNewSave) return new SaveData();
            var data = JsonConvert.DeserializeObject<SaveData>(json);
            return SaveMigrator.Migrate(data);
        }
    }
}
