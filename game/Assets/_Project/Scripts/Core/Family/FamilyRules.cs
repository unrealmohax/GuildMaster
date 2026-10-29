using System;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Чистые правила семьи: шансы состава по возрасту, норма платы, сколько платит человек, шанс болезни и смерти, слово родства.
    /// Случайных чисел не тратят.
    /// </summary>
    public static class FamilyRules
    {
        /// <summary>Лет сверх нижней границы возраста генерации.</summary>
        public static int YearsOver(Adventurer adventurer, BalanceSettings balance) =>
            Math.Max(0, adventurer.Age - balance.Adventurers.Age.Min);

        /// <summary>Шанс, что родитель жив: с возрастом ниже.</summary>
        public static float ParentAliveChance(Adventurer adventurer, BalanceSettings balance)
        {
            FamilyBalance family = balance.Family;
            return Clamp01(family.ParentAliveAtMin - family.ParentAlivePerYear * YearsOver(adventurer, balance));
        }

        /// <summary>
        /// Шанс супруга: с возрастом выше; ось Люди к Одиночке — × (1 − <c>loneSpouseCut</c> × сила полюса); Семейный —
        /// × <c>familySpouseMultiplier</c>; у Влюблённого (партнёр в гильдии) супруга за кадром нет.
        /// </summary>
        public static float SpouseChance(Adventurer adventurer, DataRegistry data)
        {
            BalanceSettings balance = data.Balance;
            FamilyBalance family = balance.Family;
            if (TraitRules.FindWithHook(adventurer, TraitHook.LoverPartner, data) != null) return 0f;

            float chance = family.SpouseBase + family.SpousePerYear * YearsOver(adventurer, balance);
            chance *= 1f - family.LoneSpouseCut * AxisMath.Strength(adventurer.GetAxis(AxisId.People), AxisPole.Negative);
            if (IsFamilial(adventurer, data)) chance *= family.FamilySpouseMultiplier;
            return Clamp01(chance);
        }

        /// <summary>Шанс каждого ребёнка (бросок — только при супруге): с возрастом выше.</summary>
        public static float ChildChance(Adventurer adventurer, BalanceSettings balance)
        {
            FamilyBalance family = balance.Family;
            return Clamp01(family.ChildBase + family.ChildPerYear * YearsOver(adventurer, balance));
        }

        /// <summary>Черта Семейный: платит семье первым и сверх нормы, острее переживает беды.</summary>
        public static bool IsFamilial(Adventurer adventurer, DataRegistry data) =>
            TraitRules.FindWithHook(adventurer, TraitHook.FamilySendsMoneyHome, data) != null;

        /// <summary>Норма за одного родного в неделю: по роли, больной — × <c>illCostMultiplier</c>; умерший — 0.</summary>
        public static int MemberCost(FamilyMember member, FamilyBalance balance)
        {
            if (!member.Alive) return 0;
            int cost = member.Role == FamilyRole.Parent ? balance.ParentCost
                : member.Role == FamilyRole.Spouse ? balance.SpouseCost
                : balance.ChildCost;
            return member.IsIll ? WalletService.Coins(cost * balance.IllCostMultiplier) : cost;
        }

        /// <summary>Норма в неделю: сумма по живым родным.</summary>
        public static int Norm(Adventurer adventurer, FamilyBalance balance)
        {
            int norm = 0;
            foreach (FamilyMember member in adventurer.Family.Members) norm += MemberCost(member, balance);
            return norm;
        }

        /// <summary>
        /// Сколько человек отправит домой при <paramref name="norm"/>: Семейный — первым, норма × (1 + <c>familyExtraShare</c>), сколько
        /// есть в кошельке; обычный — только сверх резерва на жильё и еду (<see cref="FoodRules.Reserve"/>), не больше нормы.
        /// </summary>
        public static int Payment(Adventurer adventurer, int norm, DataRegistry data, bool winter = false)
        {
            if (norm <= 0) return 0;
            int wallet = adventurer.State.Wallet;
            if (IsFamilial(adventurer, data))
                return Math.Min(wallet, WalletService.Coins(norm * (1f + data.Balance.Family.FamilyExtraShare)));
            return Math.Max(0, Math.Min(norm, wallet - FoodRules.Reserve(adventurer, data.Balance, winter)));
        }

        /// <summary>Шанс заболеть в этом месяце по роли.</summary>
        public static float IllChance(FamilyRole role, FamilyBalance balance) =>
            role == FamilyRole.Parent ? balance.IllChanceParent
            : role == FamilyRole.Spouse ? balance.IllChanceSpouse
            : balance.IllChanceChild;

        /// <summary>Шанс смерти за неделю болезни: ниже, если норма этой недели заплачена полностью.</summary>
        public static float DeathChance(bool paidInFull, FamilyBalance balance) =>
            paidInFull ? balance.DeathChancePaid : balance.DeathChanceUnpaid;

        /// <summary>Стресс от вести о смерти: у Семейного сильнее.</summary>
        public static float DeathStress(Adventurer adventurer, DataRegistry data) =>
            IsFamilial(adventurer, data) ? data.Balance.Family.FamilyDeathStress : data.Balance.Family.DeathStress;

        /// <summary>Слово родства (отец, мать, муж, жена, сын, дочь) с падежами и родом; нет в данных — <c>null</c>.</summary>
        public static NounForms RelativeForms(FamilyMember member, NameList names)
        {
            if (names == null) return null;
            int index = (int)member.Role * 2 + (member.Gender == Gender.Female ? 1 : 0);
            return index < names.Relatives.Count ? names.Relatives[index] : null;
        }

        /// <summary>Метка <c>{родной}</c>: слово родства с падежами и родом; нет в данных — несклоняемое имя.</summary>
        public static TextValue RelativeValue(FamilyMember member, DataRegistry data)
        {
            NounForms forms = RelativeForms(member, data.HasDefinitions ? data.Names : null);
            return forms != null
                ? TextValue.Noun(forms)
                : TextValue.Word(member.Name, member.Gender == Gender.Female ? GrammaticalGender.Feminine : GrammaticalGender.Masculine);
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }

    /// <summary>Метки <c>{имя}</c> — человек и <c>{родной}</c> — его родной: для текстов о семье вне ленты.</summary>
    public sealed class RelativeTextSource : ITextSource
    {
        private readonly TextValue person;
        private readonly TextValue relative;

        public RelativeTextSource(Adventurer adventurer, FamilyMember member, DataRegistry data)
        {
            person = EventTextSource.PersonValue(adventurer, data);
            relative = FamilyRules.RelativeValue(member, data);
        }

        public bool TryGet(string label, out TextValue value)
        {
            value = label == "имя" ? person : label == "родной" ? relative : null;
            return value != null;
        }
    }
}
