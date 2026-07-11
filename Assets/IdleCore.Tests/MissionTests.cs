using System;
using System.Collections.Generic;
using IdleCore;
using IdleCore.Economy;
using IdleCore.LiveOps;
using NUnit.Framework;

namespace IdleCore.Tests
{
    public class MissionTests
    {
        private static (MissionSystem, Wallet, ManualClock) Build()
        {
            var clock = new ManualClock();
            var wallet = new Wallet();
            var defs = new[]
            {
                new MissionDef
                {
                    id = "m_kill", metric = "kill", target = 500,
                    rewards = new List<CurrencyGrant> { new CurrencyGrant { currency = CurrencyIds.GemSoft, amount = 100 } },
                },
                new MissionDef
                {
                    id = "m_login", metric = "login", target = 1,
                    rewards = new List<CurrencyGrant> { new CurrencyGrant { currency = CurrencyIds.GemSoft, amount = 50 } },
                },
            };
            return (new MissionSystem(defs, wallet, clock), wallet, clock);
        }

        [Test]
        public void Progress_AccumulatesAndCaps()
        {
            var (missions, _, _) = Build();
            missions.Report("kill", 300);
            Assert.IsFalse(missions.CanClaim("m_kill"));
            missions.Report("kill", 999);
            Assert.AreEqual(500, missions.State("m_kill").progress, "목표에서 캡");
            Assert.IsTrue(missions.CanClaim("m_kill"));
        }

        [Test]
        public void Claim_OncePerDay_ResetsNextDay()
        {
            var (missions, wallet, clock) = Build();
            missions.Report("kill", 500);
            Assert.IsTrue(missions.TryClaim("m_kill"));
            Assert.IsFalse(missions.TryClaim("m_kill"), "중복 수령 불가");
            Assert.AreEqual(100, wallet.Get(CurrencyIds.GemSoft));

            clock.Advance(TimeSpan.FromDays(1));
            Assert.AreEqual(0, missions.State("m_kill").progress, "일일 리셋");
            missions.Report("kill", 500);
            Assert.IsTrue(missions.TryClaim("m_kill"));
        }

        [Test]
        public void ClaimAll_ClaimsOnlyCompleted()
        {
            var (missions, wallet, _) = Build();
            missions.Report("login", 1);
            missions.Report("kill", 100); // 미완료
            Assert.AreEqual(1, missions.ClaimAll());
            Assert.AreEqual(50, wallet.Get(CurrencyIds.GemSoft));
        }

        [Test]
        public void Attendance_CyclesAndOncePerDay()
        {
            var clock = new ManualClock();
            var wallet = new Wallet();
            var days = new List<AttendanceDay>
            {
                new AttendanceDay { rewards = new List<CurrencyGrant> { new CurrencyGrant { currency = CurrencyIds.GemSoft, amount = 300 } } },
                new AttendanceDay { rewards = new List<CurrencyGrant> { new CurrencyGrant { currency = CurrencyIds.GemSoft, amount = 400 } } },
            };
            var attendance = new AttendanceSystem(days, wallet, clock);

            Assert.AreEqual(1, attendance.CurrentDay);
            Assert.IsTrue(attendance.TryClaimToday());
            Assert.IsFalse(attendance.TryClaimToday(), "하루 1회");
            Assert.AreEqual(300, wallet.Get(CurrencyIds.GemSoft));

            clock.Advance(TimeSpan.FromDays(1));
            Assert.IsTrue(attendance.TryClaimToday());
            Assert.AreEqual(700, wallet.Get(CurrencyIds.GemSoft));

            clock.Advance(TimeSpan.FromDays(1));
            Assert.AreEqual(1, attendance.CurrentDay, "2일 사이클 완주 → 1일차로 순환");
        }

        [Test]
        public void GameSession_WiresMetrics()
        {
            var config = new IdleCore.Data.GameConfig
            {
                baseStats = new Dictionary<IdleCore.Stats.StatType, double>
                {
                    { IdleCore.Stats.StatType.Attack, 1000 },
                    { IdleCore.Stats.StatType.AttackSpeed, 1 },
                    { IdleCore.Stats.StatType.CritMultiplier, 1 },
                },
                dailyMissions = new List<MissionDef>
                {
                    new MissionDef { id = "m_login", metric = "login", target = 1,
                        rewards = new List<CurrencyGrant> { new CurrencyGrant { currency = CurrencyIds.GemSoft, amount = 50 } } },
                    new MissionDef { id = "m_kill", metric = "kill", target = 10,
                        rewards = new List<CurrencyGrant> { new CurrencyGrant { currency = CurrencyIds.GemSoft, amount = 50 } } },
                },
            };
            var session = new GameSession(config, new IdleCore.Save.InMemorySaveStore(), new ManualClock(), new SeededRng(1));

            Assert.IsTrue(session.Missions.CanClaim("m_login"), "세션 생성 = 접속 미션 자동 완료");
            session.Tick(60);
            Assert.IsTrue(session.Missions.CanClaim("m_kill"), "파밍 킬이 미션에 집계");
        }
    }
}
