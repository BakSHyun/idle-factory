using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleCore;
using IdleCore.Data;
using IdleCore.Economy;
using IdleCore.Save;
using IdleCore.Stats;

namespace IdleFactory.Simulator
{
    /// <summary>
    /// 진행 곡선 시뮬레이터 — 실게임과 동일한 IdleCore 코드로 봇 플레이어를 돌린다.
    /// 목적: 밸런스 JSON을 바꿨을 때 D1/D7/D30 도달 스테이지·벽 위치·과금 포인트를 즉시 확인.
    ///
    /// 사용: dotnet run [days] [--payer]
    ///   days   시뮬레이션 일수 (기본 30)
    ///   --payer 소과금 봇 (스타터팩 + 페이백 출석 + 일일 영혼팩 = 소울헌터의 '소과금 정착' 경로)
    /// </summary>
    public static class Program
    {
        // 무과금 봇의 일일 무료 수입 (미션/이벤트 보상 가정치)
        private const long DailySoftGems = 800;
        private const int SessionsPerDay = 4;
        private const int SessionMinutes = 15;

        public static void Main(string[] args)
        {
            int days = args.Length > 0 && int.TryParse(args[0], out var d) ? d : 30;
            bool payer = args.Contains("--payer");

            string configPath = Path.Combine(AppContext.BaseDirectory, "../../../../../Assets/GameData/config.json");
            var config = GameConfigLoader.FromJson(File.ReadAllText(Path.GetFullPath(configPath)));

            var clock = new ManualClock();
            var session = new GameSession(config, new InMemorySaveStore(), clock, new SeededRng(20260711));
            var fakeStore = new FakeStoreAdapter(session.Wallet);

            Console.WriteLine($"=== {config.gameName} | {days}일 시뮬레이션 | {(payer ? "소과금" : "무과금")} 봇 ===");
            Console.WriteLine($"{"일차",4} {"스테이지",8} {"DPS",14} {"골드보유",14} {"소울보유",12} {"소환",6} {"비고"}");

            int lastStage = -1;
            int stuckDays = 0;
            long totalPulls = 0;
            var milestones = new List<string>();

            // 소과금 봇: 첫날 ₩5,500 결제 → 스타터팩 + 페이백 출석 구매
            if (payer)
            {
                fakeStore.Purchase(config.skus.First(s => s.skuId == "gem_5500"), null);
                session.Shop.TryPurchase("starter_pack", 0);
                session.PaybackAttendance?.TryPurchase();
            }

            for (int day = 1; day <= days; day++)
            {
                session.Wallet.Earn(CurrencyIds.GemSoft, DailySoftGems);
                if (payer)
                {
                    session.PaybackAttendance?.TryClaimToday();
                    session.Shop.TryPurchase("daily_soul", session.Progression.Current.Index);
                }

                for (int s = 0; s < SessionsPerDay; s++)
                {
                    // 세션 사이 오프라인 방치
                    double offlineHours = (24.0 - SessionsPerDay * SessionMinutes / 60.0) / SessionsPerDay;
                    clock.Advance(TimeSpan.FromHours(offlineHours));
                    session.ClaimOfflineReward();

                    // 온라인 플레이
                    for (int m = 0; m < SessionMinutes; m++)
                    {
                        clock.Advance(TimeSpan.FromMinutes(1));
                        session.Tick(60);
                        SpendGreedily(session);
                    }
                    totalPulls += PullGachaIfAffordable(session);
                    // 진행도 트리거 패키지 (소과금)
                    if (payer)
                        foreach (var p in config.products)
                            session.Shop.TryPurchase(p.id, session.Progression.Current.Index);
                    foreach (var pass in session.Passes.Values)
                        pass.ClaimAll(session.Progression.HighestClearedIndex);
                }

                int stage = session.Progression.HighestClearedIndex;
                string note = "";
                if (stage == lastStage) { stuckDays++; if (stuckDays >= 2) note = $"⚠ 벽 ({stuckDays}일 정체)"; }
                else { if (stuckDays >= 2) milestones.Add($"D{day}: {stuckDays}일 벽 돌파 → {Display(session, stage)}"); stuckDays = 0; }
                lastStage = stage;

                Console.WriteLine($"{day,4} {Display(session, stage),8} {session.Stats.Snapshot().Dps(),14:0.} " +
                    $"{session.Wallet.Get(CurrencyIds.Gold),14} {session.Wallet.Get(CurrencyIds.Soul),12} {totalPulls,6} {note}");
            }

            Console.WriteLine();
            Console.WriteLine("=== 요약 ===");
            Console.WriteLine($"최종 스테이지: {Display(session, session.Progression.HighestClearedIndex)} / 상한 {config.stage.maxChapter}장");
            Console.WriteLine($"총 소환: {totalPulls}회 | 보유 유닛: {session.Units.AllOwned().Count()}종 / {config.units.Count}종");
            Console.WriteLine($"천장 카운터: {session.Gacha.PityCounter("soul_summon")}");
            if (payer)
                Console.WriteLine($"결제: ₩{fakeStore.PurchaseLog.Sum(x => x.priceKrw):N0} | 하드 잔액: {session.Wallet.Get(CurrencyIds.GemHard)}");
            foreach (var m in milestones) Console.WriteLine(m);
        }

        private static string Display(GameSession session, int stageIndex) =>
            stageIndex < 0 ? "-" : new IdleCore.Progression.StageId(stageIndex).Display(session.Config.stage);

        /// <summary>골드/소울을 비용 대비 효율이 가장 좋은 축에 그리디하게 투자.</summary>
        private static void SpendGreedily(GameSession session)
        {
            for (int guard = 0; guard < 500; guard++)
            {
                string best = null;
                double bestCost = double.MaxValue;
                foreach (var kv in session.Stats.Axes)
                {
                    var axis = kv.Value;
                    if (session.Progression.HighestClearedIndex < axis.unlockStageIndex) continue;
                    if (!session.Stats.CanLevelUp(kv.Key)) continue;
                    long cost = session.Stats.NextCost(kv.Key);
                    if (!session.Wallet.CanAfford(axis.costCurrency, cost)) continue;
                    // 단순 휴리스틱: 절대 비용이 가장 싼 축부터 (곱연산이라 골고루 올리는 게 정답)
                    if (cost < bestCost) { bestCost = cost; best = kv.Key; }
                }
                if (best == null) return;
                var bestAxis = session.Stats.Axes[best];
                session.Wallet.TrySpend(bestAxis.costCurrency, session.Stats.NextCost(best));
                session.Stats.LevelUp(best);
            }
        }

        private static long PullGachaIfAffordable(GameSession session)
        {
            long pulls = 0;
            var banner = session.Gacha.Banners["soul_summon"];
            while (session.Wallet.Get(banner.costCurrency) >= banner.CostFor(10))
            {
                if (!session.Gacha.TryPull(banner.id, 10, out _)) break;
                pulls += 10;
            }
            // 신규 유닛 전부 장착 (봇 단순화)
            foreach (var unit in session.Units.AllOwned())
                if (!unit.equipped) session.Units.SetEquipped(unit.unitId, true);
            return pulls;
        }
    }
}
