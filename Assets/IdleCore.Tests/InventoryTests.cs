using System.Collections.Generic;
using System.Linq;
using IdleCore.Gacha;
using IdleCore.Stats;
using NUnit.Framework;

namespace IdleCore.Tests
{
    public class InventoryTests
    {
        private static List<UnitDef> Units() => new List<UnitDef>
        {
            new UnitDef { id = "w1", kind = "weapon", grade = UnitGrade.Rare },
            new UnitDef { id = "w2", kind = "weapon", grade = UnitGrade.Mythic },
            new UnitDef { id = "s1", kind = "skill", grade = UnitGrade.Rare },
            new UnitDef { id = "s2", kind = "skill", grade = UnitGrade.Epic },
            new UnitDef { id = "s3", kind = "skill", grade = UnitGrade.Mythic },
        };

        private static Dictionary<string, int> Slots() => new Dictionary<string, int>
        {
            { "weapon", 1 }, { "skill", 2 },
        };

        [Test]
        public void Equip_RespectsSlotLimit()
        {
            var inv = new UnitInventory(Units(), Slots());
            inv.AddCopy("s1"); inv.AddCopy("s2"); inv.AddCopy("s3");

            Assert.IsTrue(inv.TryEquip("s1"));
            Assert.IsTrue(inv.TryEquip("s2"));
            Assert.IsFalse(inv.TryEquip("s3"), "스킬 슬롯 2칸 — 3번째 장착 실패");

            inv.Unequip("s1");
            Assert.IsTrue(inv.TryEquip("s3"), "해제 후 장착 가능");
        }

        [Test]
        public void AutoEquipBest_PicksHighestGradePerKind()
        {
            var inv = new UnitInventory(Units(), Slots());
            foreach (var id in new[] { "w1", "w2", "s1", "s2", "s3" }) inv.AddCopy(id);

            inv.AutoEquipBest();

            Assert.IsTrue(inv.Get("w2").equipped, "무기 슬롯 1칸 → 신화 낫");
            Assert.IsFalse(inv.Get("w1").equipped);
            Assert.IsTrue(inv.Get("s3").equipped);
            Assert.IsTrue(inv.Get("s2").equipped);
            Assert.IsFalse(inv.Get("s1").equipped, "스킬 2칸 → 신화+전설, 특급 탈락");
        }

        [Test]
        public void CollectionMilestones_ApplyByUniqueCount()
        {
            var milestones = new List<CollectionMilestone>
            {
                new CollectionMilestone
                {
                    count = 3,
                    effects = new List<StatEffect>
                    {
                        new StatEffect { stat = StatType.FinalDamage, mode = EffectMode.Mul,
                            value = new ValueCurve { baseValue = 0.10 } },
                    },
                },
            };
            var inv = new UnitInventory(Units(), Slots(), milestones);

            inv.AddCopy("w1"); inv.AddCopy("s1");
            Assert.IsFalse(inv.ContributeEffects().Any(e => e.stat == StatType.FinalDamage),
                "2종 등록: 마일스톤(3종) 미달");

            inv.AddCopy("s2");
            Assert.IsTrue(inv.ContributeEffects().Any(e => e.stat == StatType.FinalDamage),
                "3종 등록: 도감 마일스톤 발동");

            inv.AddCopy("s2"); // 중복 사본은 등록 수에 영향 없음
            Assert.AreEqual(3, inv.UniqueOwnedCount);
        }

        [Test]
        public void EquipEffects_OnlyWhenEquipped_CollectionAlways()
        {
            var units = Units();
            units[0].baseEffects = new List<StatEffect>
            {
                new StatEffect { stat = StatType.Attack, mode = EffectMode.Add,
                    value = new ValueCurve { baseValue = 100 } },
            };
            units[0].collectionEffects = new List<StatEffect>
            {
                new StatEffect { stat = StatType.Health, mode = EffectMode.Add,
                    value = new ValueCurve { baseValue = 5 } },
            };
            var inv = new UnitInventory(units, Slots());
            inv.AddCopy("w1");

            var effects = inv.ContributeEffects();
            Assert.IsFalse(effects.Any(e => e.stat == StatType.Attack), "미장착: 장착 효과 없음");
            Assert.IsTrue(effects.Any(e => e.stat == StatType.Health), "미장착이어도 도감 효과는 적용");

            inv.TryEquip("w1");
            Assert.IsTrue(inv.ContributeEffects().Any(e => e.stat == StatType.Attack));
        }
    }
}
