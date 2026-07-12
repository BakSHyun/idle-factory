using System.Collections.Generic;
using IdleCore;
using IdleCore.Data;
using IdleCore.Gacha;
using IdleCore.Save;
using IdleCore.Stats;
using NUnit.Framework;

namespace IdleCore.Tests
{
    public class ElementTests
    {
        [Test]
        public void Triangle_FireDarkLightning()
        {
            Assert.IsTrue(Elements.Beats(Elements.Fire, Elements.Dark));
            Assert.IsTrue(Elements.Beats(Elements.Dark, Elements.Lightning));
            Assert.IsTrue(Elements.Beats(Elements.Lightning, Elements.Fire));
            Assert.IsFalse(Elements.Beats(Elements.Fire, Elements.Lightning));
            Assert.IsFalse(Elements.Beats(Elements.Fire, Elements.Fire));
        }

        [Test]
        public void MobElement_RotatesByChapter()
        {
            Assert.AreEqual(Elements.MobElement(1), Elements.MobElement(4));
            Assert.AreNotEqual(Elements.MobElement(1), Elements.MobElement(2));
        }

        [Test]
        public void Advantage_AffectsFinalDamage()
        {
            string chapter1Mob = Elements.MobElement(1);
            // 1챕터 몹을 이기는 속성의 차사
            string counter = null;
            foreach (var e in Elements.All)
                if (Elements.Beats(e, chapter1Mob)) counter = e;

            var config = new GameConfig
            {
                baseStats = new Dictionary<StatType, double>
                {
                    { StatType.Attack, 100 }, { StatType.AttackSpeed, 1 }, { StatType.CritMultiplier, 1 },
                },
                units = new List<UnitDef>
                {
                    new UnitDef { id = "h1", kind = "hero", element = counter },
                },
            };
            var session = new GameSession(config, new InMemorySaveStore(), new ManualClock(), new SeededRng(1));

            double before = session.Stats.Snapshot().Dps();
            session.Units.AddCopy("h1");
            session.Units.TryEquip("h1");
            double after = session.Stats.Snapshot().Dps();

            Assert.AreEqual(before * 1.06, after, before * 0.001, "상성 유리 차사 1명 = 최종 데미지 +6%");
        }
    }
}
