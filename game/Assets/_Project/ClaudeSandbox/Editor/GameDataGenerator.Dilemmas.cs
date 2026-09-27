using System.Collections.Generic;
using System.Linq;
using GuildMaster.Data;

namespace GuildMaster.ClaudeSandbox
{
    // Дилеммы прототипа. Суммы и сдвиги — из таблиц дилемм.
    // {сумма} в тексте варианта — сумма его эффекта (Treasury, GiveLoan, PayWeekAndTreatment); её подставляет игра.
    public static partial class GameDataGenerator
    {
        private static readonly string[] DilemmaIds =
        {
            "LoanRequest", "DeserterReturned", "LootDispute", "WoundedWantsQuest", "LoversSameParty", "TavernFeast",
        };

        private static void FillDilemmas(Dictionary<string, DilemmaDefinition> d, Dictionary<string, SpecialTraitDefinition> traits)
        {
            Dilemma(d["LoanRequest"], 1, "Просьба в долг", DilemmaSource.Adventurer, DilemmaTrigger.LoanRequest,
                "{имя} просит в долг {сумма}. {причина}", timeout: 2,
                new[]
                {
                    Option("Дать всю сумму", "−{сумма} из казны, довольство и лояльность {имя:р} растут", "dilemma.loan.full",
                        Effect(DilemmaEffectKind.GiveLoan, DilemmaTarget.Subject, 1f),
                        Effect(DilemmaEffectKind.Contentment, DilemmaTarget.Subject, 10f),
                        Effect(DilemmaEffectKind.Loyalty, DilemmaTarget.Subject, 5f),
                        Flag(MemoryFlag.TookLoan)),
                    Option("Дать половину", "−{сумма} из казны, довольство {имя:р} немного растёт", "dilemma.loan.half",
                        Effect(DilemmaEffectKind.GiveLoan, DilemmaTarget.Subject, 0.5f),
                        Effect(DilemmaEffectKind.Contentment, DilemmaTarget.Subject, 3f),
                        Flag(MemoryFlag.TookLoan)),
                    Refusal("Отказать", "Довольство и лояльность {имя:р} падают", "dilemma.loan.refuse",
                        Effect(DilemmaEffectKind.Contentment, DilemmaTarget.Subject, -10f),
                        Effect(DilemmaEffectKind.Loyalty, DilemmaTarget.Subject, -3f)),
                });
            Set(d["LoanRequest"], "reasons", new List<DilemmaReason>
            {
                Reason("Говорит, что нечем платить за жильё."),
                Reason("Говорит, что нужно на снаряжение."),
                Reason("Обещает вернуть с первой же награды."),
                Reason("Дома ждут, а платить нечем.", traits["Family"]),
            });
            // Семейный раскрывается, когда звучит его причина: раскрытие срабатывает, только если черта есть.
            Set(d["LoanRequest"], "arrivalEffects", new List<DilemmaEffect> { RevealTrait(traits["Family"], DilemmaTarget.Subject) });

            Dilemma(d["DeserterReturned"], 2, "Беглец вернулся", DilemmaSource.Adventurer, DilemmaTrigger.DeserterReturned,
                "{имя} вернул[ся|ась]. Стоит у дверей, не поднимая глаз. Просит оставить [его|её]@имя в гильдии.", timeout: 2,
                new[]
                {
                    Option("Простить", "{имя} остаётся; брошенные на задании будут относиться к [нему|ней]@имя хуже", "dilemma.deserter.forgive",
                        Effect(DilemmaEffectKind.Relation, DilemmaTarget.Abandoned, -10f),
                        Flag(MemoryFlag.Pardoned)),
                    Option("Наказать штрафом", "Половина кошелька {имя:р} — в казну, лояльность падает; {имя} остаётся", "dilemma.deserter.fine",
                        Effect(DilemmaEffectKind.WalletToTreasury, DilemmaTarget.Subject, 0.5f),
                        Effect(DilemmaEffectKind.Loyalty, DilemmaTarget.Subject, -15f),
                        Flag(MemoryFlag.Punished)),
                    Refusal("Выгнать", "{имя} уходит из гильдии; брошенные довольны", "dilemma.deserter.expel",
                        Effect(DilemmaEffectKind.Expel, DilemmaTarget.Subject, 0f),
                        Effect(DilemmaEffectKind.Contentment, DilemmaTarget.Abandoned, 5f)),
                });

            Dilemma(d["LootDispute"], 3, "Ссора из-за добычи", DilemmaSource.Adventurer, DilemmaTrigger.LootDispute,
                "{имя} и {напарник} не поделили добычу после задания у {место:р}. Оба пришли к вам.", timeout: 3,
                new[]
                {
                    Option("Поддержать {имя:в}", "Довольство {имя:р} растёт, {напарник:р} — падает; они поссорятся", "dilemma.loot.subject",
                        Effect(DilemmaEffectKind.Contentment, DilemmaTarget.Subject, 5f),
                        Effect(DilemmaEffectKind.Contentment, DilemmaTarget.Partner, -10f),
                        Effect(DilemmaEffectKind.Relation, DilemmaTarget.SubjectAndPartner, -10f),
                        RevealPole(AxisId.Money, AxisPole.Positive, DilemmaTarget.Subject)),
                    Option("Поддержать {напарник:в}", "Довольство {напарник:р} растёт, {имя:р} — падает; утаенное вернётся в общий делёж", "dilemma.loot.partner",
                        Effect(DilemmaEffectKind.Contentment, DilemmaTarget.Partner, 5f),
                        Effect(DilemmaEffectKind.Contentment, DilemmaTarget.Subject, -10f),
                        Effect(DilemmaEffectKind.ReturnSkimmedLoot, DilemmaTarget.Subject, 0f),
                        RevealPole(AxisId.Principles, AxisPole.Positive, DilemmaTarget.Partner)),
                    Option("Разделить поровну, доплатить из казны", "−{сумма} из казны, оба немного довольны", "dilemma.loot.split",
                        Effect(DilemmaEffectKind.Treasury, DilemmaTarget.Guild, -30f),
                        Effect(DilemmaEffectKind.Contentment, DilemmaTarget.SubjectAndPartner, 3f),
                        Effect(DilemmaEffectKind.Relation, DilemmaTarget.SubjectAndPartner, -3f)),
                    Hidden("dilemma.loot.timeout",
                        Effect(DilemmaEffectKind.Relation, DilemmaTarget.SubjectAndPartner, -15f),
                        Effect(DilemmaEffectKind.Contentment, DilemmaTarget.SubjectAndPartner, -5f)),
                });

            Dilemma(d["WoundedWantsQuest"], 4, "Раненый рвётся на задание", DilemmaSource.Adventurer, DilemmaTrigger.WoundedWantsQuest,
                "{имя} ещё не оправил[ся|ась] от раны, но просит отпустить [его|её] на задание. Говорит, деньги нужны сейчас.", timeout: 1,
                new[]
                {
                    Option("Разрешить", "{имя} сможет брать задания с тяжёлой раной, довольство растёт", "dilemma.wounded.allow",
                        Effect(DilemmaEffectKind.AllowQuestWhileWounded, DilemmaTarget.Subject, 0.6f),
                        Effect(DilemmaEffectKind.Contentment, DilemmaTarget.Subject, 5f),
                        Flag(MemoryFlag.WoundedQuestAllowed)),
                    Refusal("Запретить", "Довольство {имя:р} падает", "dilemma.wounded.forbid",
                        Effect(DilemmaEffectKind.Contentment, DilemmaTarget.Subject, -10f)),
                    Option("Оплатить лечение и дать на неделю", "−{сумма} из казны, лояльность и довольство {имя:р} растут", "dilemma.wounded.pay",
                        Effect(DilemmaEffectKind.PayWeekAndTreatment, DilemmaTarget.Subject, 0f),
                        Effect(DilemmaEffectKind.Loyalty, DilemmaTarget.Subject, 10f),
                        Effect(DilemmaEffectKind.Contentment, DilemmaTarget.Subject, 10f)),
                });

            Dilemma(d["LoversSameParty"], 10, "Влюблённые просят одну группу", DilemmaSource.Adventurer, DilemmaTrigger.LoversSameParty,
                "{имя} и {напарник} просят ставить их в одну группу.", timeout: 1,
                new[]
                {
                    Option("Разрешить", "Почти всегда будут ходить вместе", "dilemma.lovers.allow",
                        Effect(DilemmaEffectKind.AllowSameParty, DilemmaTarget.SubjectAndPartner, 0.5f),
                        Flag(MemoryFlag.LoversTogether, DilemmaTarget.SubjectAndPartner)),
                    Refusal("Запретить", "Не смогут ходить в одной группе; довольство и лояльность обоих падают", "dilemma.lovers.forbid",
                        Effect(DilemmaEffectKind.ForbidSameParty, DilemmaTarget.SubjectAndPartner, 0f),
                        Effect(DilemmaEffectKind.Contentment, DilemmaTarget.SubjectAndPartner, -15f),
                        Effect(DilemmaEffectKind.Loyalty, DilemmaTarget.SubjectAndPartner, -5f),
                        Flag(MemoryFlag.LoversSeparated, DilemmaTarget.SubjectAndPartner)),
                });
            Set(d["LoversSameParty"], "oncePerPair", true);
            Set(d["LoversSameParty"], "arrivalEffects", new List<DilemmaEffect> { RevealTrait(traits["Lover"], DilemmaTarget.SubjectAndPartner) });

            Dilemma(d["TavernFeast"], 13, "Трактирщик предлагает праздник", DilemmaSource.Staff, DilemmaTrigger.TavernFeast,
                "Трактирщик предлагает устроить вечер: люди на пределе.", timeout: 2,
                new[]
                {
                    Option("Провести праздник", "−{сумма} из казны, у всех в таверне вечером стресс сильно спадёт", "dilemma.feast.full",
                        Effect(DilemmaEffectKind.Treasury, DilemmaTarget.Guild, -150f),
                        Effect(DilemmaEffectKind.Stress, DilemmaTarget.AllInTavern, -20f),
                        Effect(DilemmaEffectKind.Relation, DilemmaTarget.AllInTavern, 2f),
                        Effect(DilemmaEffectKind.Fatigue, DilemmaTarget.AllInTavern, 10f)),
                    Option("Скромно", "−{сумма} из казны, у всех в таверне вечером стресс немного спадёт", "dilemma.feast.modest",
                        Effect(DilemmaEffectKind.Treasury, DilemmaTarget.Guild, -60f),
                        Effect(DilemmaEffectKind.Stress, DilemmaTarget.AllInTavern, -8f)),
                    Refusal("Отказать", "Ничего не изменится", "dilemma.feast.refuse",
                        Effect(DilemmaEffectKind.ContentmentIfRecentDeath, DilemmaTarget.AllAdventurers, -3f)),
                });
            Set(d["TavernFeast"], "cooldownDays", 30);
            Set(d["TavernFeast"], "cooldownPerGuild", true);
        }

        private static void Dilemma(DilemmaDefinition asset, int number, string title, DilemmaSource source, DilemmaTrigger trigger,
            string body, int timeout, DilemmaOption[] options)
        {
            Def(asset, asset.name, title);
            Set(asset, "number", number);
            Set(asset, "source", source);
            Set(asset, "trigger", trigger);
            Set(asset, "cooldownDays", 0);
            Set(asset, "cooldownPerGuild", false);
            Set(asset, "oncePerPair", false);
            Set(asset, "bodyTemplate", body);
            Set(asset, "reasons", new List<DilemmaReason>());
            Set(asset, "arrivalEffects", new List<DilemmaEffect>());
            Set(asset, "options", options.ToList());
            Set(asset, "timeoutOption", timeout);
        }

        private static DilemmaOption Option(string text, string visible, string feedKey, params DilemmaEffect[] effects) =>
            Make<DilemmaOption>(("text", text), ("visibleConsequencesText", visible), ("answerFeedKey", feedKey),
                ("effects", effects.ToList()), ("isRefusal", false), ("playerSelectable", true));

        private static DilemmaOption Refusal(string text, string visible, string feedKey, params DilemmaEffect[] effects) =>
            Make<DilemmaOption>(("text", text), ("visibleConsequencesText", visible), ("answerFeedKey", feedKey),
                ("effects", effects.ToList()), ("isRefusal", true), ("playerSelectable", true));

        /// <summary>Исход «без ответа», которого нет среди вариантов игрока.</summary>
        private static DilemmaOption Hidden(string feedKey, params DilemmaEffect[] effects) =>
            Make<DilemmaOption>(("text", "(без ответа)"), ("visibleConsequencesText", ""), ("answerFeedKey", feedKey),
                ("effects", effects.ToList()), ("isRefusal", false), ("playerSelectable", false));

        private static DilemmaEffect Effect(DilemmaEffectKind kind, DilemmaTarget target, float value) =>
            Make<DilemmaEffect>(("kind", kind), ("target", target), ("value", value));

        private static DilemmaEffect Flag(MemoryFlag flag, DilemmaTarget target = DilemmaTarget.Subject) =>
            Make<DilemmaEffect>(("kind", DilemmaEffectKind.SetMemoryFlag), ("target", target), ("flag", flag));

        private static DilemmaEffect RevealPole(AxisId axis, AxisPole pole, DilemmaTarget target) =>
            Make<DilemmaEffect>(("kind", DilemmaEffectKind.RevealAxisPole), ("target", target), ("axis", axis), ("pole", pole));

        private static DilemmaEffect RevealTrait(SpecialTraitDefinition trait, DilemmaTarget target) =>
            Make<DilemmaEffect>(("kind", DilemmaEffectKind.RevealTrait), ("target", target), ("trait", trait));

        private static DilemmaReason Reason(string text, SpecialTraitDefinition requiresTrait = null) =>
            Make<DilemmaReason>(("text", text), ("requiresTrait", requiresTrait));
    }
}
