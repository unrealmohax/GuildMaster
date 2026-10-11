using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Состав семьи нового человека — по возрасту, оси Люди и чертам (<see cref="FamilyRules"/>). Генерация вызывает его последним,
    /// когда черты уже есть: прежние броски генерации не сдвигаются. Порядок бросков: отец, мать (жив ли и имя), супруг (есть ли
    /// и имя), дети (только при супруге: до <c>maxChildren</c> бросков, у каждого пол и имя). Семейный без семьи после бросков —
    /// жив один родитель (бросок, кто). Случай с нулевым шансом бросков не тратит.
    /// </summary>
    internal static class FamilyGenerator
    {
        public static void Generate(Rng rng, DataRegistry data, Adventurer adventurer)
        {
            BalanceSettings balance = data.Balance;
            FamilyBalance family = balance.Family;
            var names = new HashSet<string>(StringComparer.Ordinal) { adventurer.Name };

            float parent = FamilyRules.ParentAliveChance(adventurer, balance);
            foreach (Gender gender in new[] { Gender.Male, Gender.Female })
            {
                if (parent > 0f && rng.Chance(parent)) Add(rng, data, adventurer, FamilyRole.Parent, gender, names);
            }

            float spouse = FamilyRules.SpouseChance(adventurer, data);
            if (spouse > 0f && rng.Chance(spouse))
            {
                Add(rng, data, adventurer, FamilyRole.Spouse, adventurer.Gender == Gender.Male ? Gender.Female : Gender.Male, names);

                float child = FamilyRules.ChildChance(adventurer, balance);
                for (int i = 0; i < family.MaxChildren && child > 0f; i++)
                {
                    if (!rng.Chance(child)) continue;
                    Gender gender = rng.Chance(family.ChildMaleChance) ? Gender.Male : Gender.Female;
                    Add(rng, data, adventurer, FamilyRole.Child, gender, names);
                }
            }

            if (adventurer.Family.Members.Count == 0 && FamilyRules.IsFamilial(adventurer, data))
                Add(rng, data, adventurer, FamilyRole.Parent, rng.Chance(0.5f) ? Gender.Male : Gender.Female, names);
        }

        /// <summary>
        /// Черты, которые человек получил уже с семьёй (партнёр Влюблённого из гильдии): у Влюблённого супруга за кадром нет —
        /// супруг убирается, дети остаются. Без бросков.
        /// </summary>
        public static void FitToTraits(SimContext ctx, Adventurer adventurer)
        {
            if (TraitRules.FindWithHook(adventurer, TraitHook.LoverPartner, ctx.Data) == null) return;
            foreach (FamilyMember member in new List<FamilyMember>(adventurer.Family.Members))
            {
                if (member.Role != FamilyRole.Spouse) continue;
                adventurer.Family.Remove(member);
                ctx.Log.Write(SimLogLevel.Debug, "family #{0} {1}: spouse removed (lover in guild)", adventurer.Id, adventurer.Name);
            }
        }

        private static void Add(Rng rng, DataRegistry data, Adventurer adventurer, FamilyRole role, Gender gender, HashSet<string> names)
        {
            string name = AdventurerGenerator.PickName(rng, data.Names, gender, names);
            names.Add(name);
            adventurer.Family.Add(new FamilyMember(role, gender, name));
        }
    }
}
