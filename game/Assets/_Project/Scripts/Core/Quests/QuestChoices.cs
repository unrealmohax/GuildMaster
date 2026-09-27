using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Решения на задании — точка «по событию» модели решений. Решает решающий (<see cref="QuestParty.Leader"/>) по своим
    /// мотивам (<see cref="Motives"/>): ценность варианта = Σ вес мотива × оценка; лучший — с шансом <c>bestChoiceChance</c>,
    /// иначе второй. Коэффициенты — <see cref="DecisionsBalance"/>. Здесь же — оценки заказа по мотивам (их берёт и взятие
    /// заказа с доски). <c>p</c> — шанс, каким его видит решающий: профиль группы против требований × его ошибка оценки риска
    /// (у заказа ещё × неточность описания).
    /// </summary>
    public static class QuestChoices
    {
        /// <summary>
        /// Доля человека в награде заказа, какой он её ждёт: награда − комиссия + доплата, делённые на <paramref name="partySize"/>.
        /// У событийного задания комиссии нет, у экзамена награды нет.
        /// </summary>
        public static float ExpectedShare(Order order, float commission, int partySize)
        {
            if (order.IsPromotion) return 0f;
            float reward = order.IsEventQuest ? order.Reward : order.Reward * (1f - commission);
            return (reward + order.Surcharge) / Math.Max(1, partySize);
        }

        /// <summary>Расходы человека на жизнь за <c>moneyExpenseDays</c> дней — мерило «много ли это денег».</summary>
        public static float ExpenseScale(Adventurer adventurer, DataRegistry data) =>
            Math.Max(1f, data.Balance.Decisions.MoneyExpenseDays * WalletService.DailyLivingCost(adventurer, false, data.Balance.Expenses));

        /// <summary>
        /// Оценки заказа по мотивам (индекс — <see cref="Motive"/>): Деньги = min(1, доля / расходы) × p, Слава = ранг заказа /
        /// ранг человека (не больше 1; экзамену — ещё +<c>promotionGloryBonus</c>), Безопасность = p² (экзамену —
        /// <c>promotionSafety</c>: он без ран), Отдых = 1 − усталость/100 − <c>orderRestOffset</c>; Товарищи и Утешение — 0 (соло).
        /// </summary>
        public static void OrderScores(Adventurer adventurer, Order order, float perceived, float commission, DataRegistry data, float[] scores)
        {
            DecisionsBalance decisions = data.Balance.Decisions;
            float share = ExpectedShare(order, commission, 1);
            scores[(int)Motive.Money] = Math.Min(1f, share / ExpenseScale(adventurer, data)) * perceived;
            float glory = Math.Min(1f, QuestMath.RankNumber(order.Rank) / (float)QuestMath.RankNumber(adventurer.GuildRank));
            scores[(int)Motive.Glory] = order.IsPromotion ? glory + decisions.PromotionGloryBonus : glory;
            scores[(int)Motive.Safety] = order.IsPromotion ? decisions.PromotionSafety : perceived * perceived;
            scores[(int)Motive.Rest] = 1f - adventurer.State.Fatigue / 100f - decisions.OrderRestOffset;
        }

        /// <summary>
        /// Решение группы после проваленного раунда: продолжить или отступить. Строки: решение (продолжить / сомнение / спор)
        /// и отступление ([З], автопауза). Возвращает <c>true</c> — продолжают.
        /// </summary>
        public static bool DecideAfterFailure(SimContext ctx, QuestRun run, Order order)
        {
            List<Adventurer> present = QuestParty.Present(ctx.World, run);
            Adventurer leader = QuestParty.Leader(present, ctx.Data);
            float[] group = QuestMath.GroupProfile(present, ctx.Data, run.Panicked, run.Rushing);

            Choice leaderChoice = FailureChoice(ctx, run, order, present, group, leader);
            bool keepGoing = Pick(ctx, leader, leaderChoice, "after-failure " + run.FailedRounds, run);

            // Остальные: у кого предпочтение противоположное — сомнение (слабое) или спор (сильное).
            Adventurer strongest = null;
            float strongestGap = 0f;
            foreach (Adventurer member in present)
            {
                if (member.Id == leader.Id) continue;
                Choice own = FailureChoice(ctx, run, order, present, group, member);
                float gap = own.Continue - own.Retreat; // > 0 — хочет продолжать
                if ((gap > 0f) == keepGoing || gap == 0f) continue;
                if (strongest == null || Math.Abs(gap) > strongestGap)
                {
                    strongest = member;
                    strongestGap = Math.Abs(gap);
                }
            }

            int[] ids = QuestSystem.Ids(present);
            if (strongest != null && strongestGap >= ctx.Data.Balance.Decisions.ArgueThreshold)
            {
                // «{имя} рвался вперёд, {напарник} — назад»: первым — кто за «продолжить».
                Adventurer forward = keepGoing ? leader : strongest;
                Adventurer back = keepGoing ? strongest : leader;
                QuestParty.Publish(ctx, SimEventType.PartyDecision, run, EventImportance.Notable, forward.Id, back.Id)
                    .With("kind", "argue").With("reasons", leaderChoice.Reasons);
            }
            else if (strongest != null && keepGoing)
            {
                QuestParty.Publish(ctx, SimEventType.PartyDecision, run, EventImportance.Notable, strongest.Id)
                    .With("kind", "doubt").With("reasons", leaderChoice.Reasons);
            }
            else if (keepGoing)
            {
                QuestParty.Publish(ctx, SimEventType.PartyDecision, run, EventImportance.Notable, ids)
                    .With("kind", "continue").With("reasons", leaderChoice.Reasons);
            }

            if (!keepGoing)
            {
                QuestParty.Publish(ctx, SimEventType.PartyRetreated, run, EventImportance.Notable, leader.Id)
                    .With("failed", run.FailedRounds).With("reasons", leaderChoice.Reasons);
            }
            return keepGoing;
        }

        /// <summary>
        /// Продолжить: Деньги = оценка заказа по Деньгам (без потерянного бонуса) × p, Слава = оценка по Славе × p, Безопасность = p²
        /// и −<c>deadlyFailureSafetyPenalty</c>, если следующая ступень лестницы грозит гибелью, Отдых = 1 − усталость/100 −
        /// <c>orderRestOffset</c>. Отступить: Безопасность = <c>retreatSafetyPerFailure</c> × провалов + <c>retreatWoundedSafety</c>,
        /// если есть раненые (не больше 1), Отдых = усталость/100.
        /// </summary>
        private static Choice FailureChoice(SimContext ctx, QuestRun run, Order order, List<Adventurer> present, float[] group, Adventurer viewer)
        {
            DataRegistry data = ctx.Data;
            DecisionsBalance decisions = data.Balance.Decisions;
            float[] perceived = QuestMath.PerceivedRequirements(order.Profile, order.DescriptionAccuracy, viewer, data);
            float p = QuestMath.Overlap(data.Stats.RadarOrder, perceived, group);

            var goOn = new float[Vocabulary.MotiveCount];
            float share = ExpectedShare(order, ctx.World.Treasury.Commission, present.Count);
            goOn[(int)Motive.Money] = Math.Min(1f, share / ExpenseScale(viewer, data)) * p;
            goOn[(int)Motive.Glory] = Math.Min(1f, QuestMath.RankNumber(order.Rank) / (float)QuestMath.RankNumber(viewer.GuildRank)) * p;
            FailureStep next = QuestRounds.StepFor(data.Balance.Rounds, run.FailedRounds + 1);
            goOn[(int)Motive.Safety] = p * p - (next.Death != FailureDeath.None ? decisions.DeadlyFailureSafetyPenalty : 0f);
            goOn[(int)Motive.Rest] = 1f - viewer.State.Fatigue / 100f - decisions.OrderRestOffset;

            bool wounded = false;
            foreach (Adventurer member in present) wounded |= member.State.Conditions.Count > 0;
            var back = new float[Vocabulary.MotiveCount];
            back[(int)Motive.Safety] = Math.Min(1f, decisions.RetreatSafetyPerFailure * run.FailedRounds + (wounded ? decisions.RetreatWoundedSafety : 0f));
            back[(int)Motive.Rest] = viewer.State.Fatigue / 100f;

            return Evaluate(viewer, data, "Continue", goOn, "Retreat", back, p);
        }

        /// <summary>
        /// Находка: исследовать или пройти мимо. Исследовать — Деньги = min(1, ожидаемый тайник на человека / расходы) × p,
        /// Слава = <c>exploreGlory</c> × p, Безопасность = p². Пройти мимо — Безопасность <c>skipDiscoverySafety</c>, Деньги =
        /// min(1, награда за сообщение на человека / расходы). Возвращает <c>true</c> — исследовать.
        /// </summary>
        public static bool DecideExplore(SimContext ctx, QuestRun run, DiscoveryDefinition discovery, float[] profile, int reward)
        {
            DataRegistry data = ctx.Data;
            DecisionsBalance decisions = data.Balance.Decisions;
            List<Adventurer> present = QuestParty.Present(ctx.World, run);
            Adventurer leader = QuestParty.Leader(present, data);
            float[] perceived = QuestMath.PerceivedRequirements(profile, 1f, leader, data);
            float p = QuestMath.Overlap(data.Stats.RadarOrder, perceived, QuestMath.GroupProfile(present, data));

            float expectedLoot = 0f;
            foreach (OutcomeChance outcome in discovery.SuccessOutcomes)
            {
                if (outcome.Kind == OutcomeKind.Loot)
                    expectedLoot += outcome.Chance * (outcome.LootShareOfReward.Min + outcome.LootShareOfReward.Max) / 2f * reward;
            }
            float scale = ExpenseScale(leader, data);
            var explore = new float[Vocabulary.MotiveCount];
            explore[(int)Motive.Money] = Math.Min(1f, expectedLoot / present.Count / scale) * p;
            explore[(int)Motive.Glory] = decisions.ExploreGlory * p;
            explore[(int)Motive.Safety] = p * p;

            var skip = new float[Vocabulary.MotiveCount];
            skip[(int)Motive.Safety] = decisions.SkipDiscoverySafety;
            skip[(int)Motive.Money] = Math.Min(1f, discovery.ReportRewardShare * reward / present.Count / scale);

            Choice choice = Evaluate(leader, data, "Explore", explore, "Skip", skip, p);
            return Pick(ctx, leader, choice, "discovery", run);
        }

        private static Choice Evaluate(Adventurer viewer, DataRegistry data, string firstName, float[] first, string secondName, float[] second, float perceived)
        {
            MotiveWeights weights = Motives.Weigh(viewer, data, false);
            Clamp(first);
            Clamp(second);
            float firstValue = Value(weights, first);
            float secondValue = Value(weights, second);
            bool firstBetter = firstValue >= secondValue;
            float[] best = firstBetter ? first : second;
            string reasons = Reasons(weights, best, data.Balance.Decisions.MaxReasons);
            return new Choice(firstName, firstValue, secondName, secondValue, perceived, reasons, weights);
        }

        /// <summary>Лучший вариант с шансом <c>bestChoiceChance</c>, иначе второй; строка в лог. <c>true</c> — выбран первый.</summary>
        private static bool Pick(SimContext ctx, Adventurer leader, Choice choice, string point, QuestRun run)
        {
            bool firstBetter = choice.Continue >= choice.Retreat;
            bool best = ctx.RollChance(ctx.Data.Balance.Decisions.BestChoiceChance, "decision-best", leader);
            bool first = best ? firstBetter : !firstBetter;

            if (ctx.Log.IsOn(SimLogLevel.Info))
            {
                StringBuilder line = ctx.Log.Begin(SimLogLevel.Info);
                line.Append("quest #").Append(run.Id.ToString(CultureInfo.InvariantCulture)).Append(' ');
                AdventurerLog.AppendName(line.Append("decide "), leader)
                    .Append(' ').Append(point).Append(": ")
                    .Append(first ? choice.FirstName : choice.SecondName).Append(' ')
                    .Append(AdventurerLog.Number(first ? choice.Continue : choice.Retreat))
                    .Append(" (").Append(best ? "best" : "second").Append("; ")
                    .Append(first ? choice.SecondName : choice.FirstName).Append(' ')
                    .Append(AdventurerLog.Number(first ? choice.Retreat : choice.Continue))
                    .Append(") p=").Append(AdventurerLog.Number(choice.Perceived))
                    .Append(" because ").Append(choice.Reasons.Length > 0 ? choice.Reasons : "-");
                ctx.Log.Commit();
            }
            return first;
        }

        private static float Value(MotiveWeights weights, float[] scores)
        {
            float value = 0f;
            for (int m = 0; m < scores.Length; m++) value += weights[(Motive)m] * scores[m];
            return value;
        }

        private static void Clamp(float[] scores)
        {
            for (int i = 0; i < scores.Length; i++) scores[i] = Math.Max(-DecisionActions.ScoreLimit, Math.Min(DecisionActions.ScoreLimit, scores[i]));
        }

        private static string Reasons(MotiveWeights weights, float[] scores, int max)
        {
            List<(Motive Motive, float Contribution)> reasons = DecisionSystem.MainReasons(weights, scores, max);
            var text = new StringBuilder();
            foreach ((Motive motive, float contribution) in reasons)
            {
                if (text.Length > 0) text.Append(", ");
                text.Append(motive).Append(' ').Append(AdventurerLog.Number(contribution));
            }
            return text.ToString();
        }

        /// <summary>Два варианта решения: первый («продолжить», «исследовать») и второй, их ценности и главные причины лучшего.</summary>
        private readonly struct Choice
        {
            public Choice(string firstName, float first, string secondName, float second, float perceived, string reasons, MotiveWeights weights)
            {
                FirstName = firstName;
                Continue = first;
                SecondName = secondName;
                Retreat = second;
                Perceived = perceived;
                Reasons = reasons;
                Weights = weights;
            }

            public string FirstName { get; }
            public float Continue { get; }
            public string SecondName { get; }
            public float Retreat { get; }
            public float Perceived { get; }
            public string Reasons { get; }
            public MotiveWeights Weights { get; }
        }
    }
}
