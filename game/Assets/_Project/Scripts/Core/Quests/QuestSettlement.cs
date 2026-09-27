using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Конец задания: группа вернулась или не вернулся никто. Для игрока результат — «выполнено / нет»; уровень результата
    /// (<see cref="QuestResult"/>) скрыт и даёт строки ленты и очки ранга. По порядку:
    /// <list type="number">
    /// <item>уровень; заказ — выполнен или провален (архив);</item>
    /// <item>выполнено — награда целиком (у обычного заказа — минус комиссия в казну; событийное задание платит гильдия),
    /// доплата из казны; делят вернувшиеся по <see cref="Adventurer.PowerScore"/>, постоянная группа — поровну (погибшие,
    /// беглецы, повернувшие назад — не получают); из доли награды гасится долг;</item>
    /// <item>выполнено — трофеи трактирщику (бонус, если не потерян, добыча, если не потеряна, тайник из пещеры): деньги извне,
    /// казну не трогают; Беспринципный утаивает часть; делятся так же;</item>
    /// <item>находка, мимо которой прошли, — рассказать Регистратору: награда из казны и событийное задание;</item>
    /// <item>очки ранга вернувшимся, репутация гильдии (выполнено — плюс, нет — минус, пропорционально рангу; Хвастун +), опыт,
    /// отношения, счётчики;</item>
    /// <item>приобретённые черты (Кошмары, срыв Ветерана), раскрытия (Хвастун, Честный, Беспринципный);</item>
    /// <item>строки: «вернулись», трофеи; погибли все — «не вернулась», «выжил только беглец»; катастрофа — автопауза;</item>
    /// <item>группа: под задание распадается, постоянная считает задание; выполнено — может сложиться постоянная группа
    /// (<see cref="PartyService.AfterQuest"/>).</item>
    /// </list>
    /// Экзамен на повышение — без награды, трофеев, очков, репутации и опыта.
    /// </summary>
    public static class QuestSettlement
    {
        public static void Finish(SimContext ctx, QuestRun run)
        {
            if (run.Phase == QuestPhase.Returned) return;

            DataRegistry data = ctx.Data;
            List<Adventurer> returned = QuestParty.Present(ctx.World, run);
            run.Phase = QuestPhase.Returned;
            run.PhaseHoursLeft = 0;
            run.ReturnedAtHours = ctx.World.Time.TotalHours;
            run.Result = ResultOf(run);
            ctx.World.Quests.Finish(run);
            foreach (Adventurer member in returned) QuestSystem.LeaveQuest(member);

            ctx.World.Orders.TryGetOrder(run.OrderId, out Order order);
            CloseOrder(ctx, run, order);

            if (!run.IsPromotion && order != null)
            {
                if (run.Completed && returned.Count > 0) Pay(ctx, run, order, returned);
                if (run.Discovery != null && run.Discovery.State == DiscoveryState.Skipped && returned.Count > 0) Report(ctx, run, order, returned);
                Reward(ctx, run, returned);
            }

            int[] ids = QuestSystem.Ids(returned);
            QuestParty.Publish(ctx, SimEventType.QuestReturned, run, Importance(run.Result), ids)
                .With("kind", ResultKey(run.Result))
                .With("done", run.Completed)
                .With("promotion", run.IsPromotion)
                .With("count", returned.Count)
                .With("total", run.Departed.Count);

            if (returned.Count == 0 && run.Dead.Count > 0 && run.Departed.Count > 1)
            {
                QuestParty.Publish(ctx, SimEventType.QuestLost, run, EventImportance.Important, run.Dead[0]);
                if (run.Fled.Count > 0) QuestParty.Publish(ctx, SimEventType.OnlyFugitiveSurvived, run, EventImportance.Important, run.Fled[0]);
            }
            if (run.Result == QuestResult.Catastrophe)
                QuestParty.Publish(ctx, SimEventType.QuestCatastrophe, run, EventImportance.Important, run.Departed[0]);

            PartyService.AfterQuest(ctx, run, returned);
        }

        /// <summary>
        /// Скрытый уровень: выполнено без провалов раундов и без ран — блестящий; без провалов, но с ранами или гибелью — успех;
        /// после провалов — частичный; не выполнено без гибели — провал, с гибелью (или погибли все) — катастрофа.
        /// </summary>
        public static QuestResult ResultOf(QuestRun run)
        {
            if (run.Completed)
            {
                if (run.FailedRounds > 0) return QuestResult.Partial;
                return run.HadWounds || run.Dead.Count > 0 ? QuestResult.Success : QuestResult.Brilliant;
            }
            return run.Dead.Count > 0 ? QuestResult.Catastrophe : QuestResult.Fail;
        }

        private static void CloseOrder(SimContext ctx, QuestRun run, Order order)
        {
            if (order == null) return;
            OrderBoard board = ctx.World.Orders;
            order.Status = run.Completed ? OrderStatus.Done : OrderStatus.Failed;
            order.ClosedAtHours = ctx.World.Time.TotalHours;
            board.Close(order, ctx.Data.Balance.Orders.ClosedOrdersLimit);
            if (run.IsPromotion) return;
            board.Totals = run.Completed ? board.Totals.AddDone() : board.Totals.AddFailed();
        }

        // ---------- Выплаты ----------

        private static void Pay(SimContext ctx, QuestRun run, Order order, List<Adventurer> returned)
        {
            DataRegistry data = ctx.Data;
            int reward = order.Reward;
            int commission = 0;
            if (order.IsEventQuest)
            {
                TreasuryService.Debit(ctx, LedgerCategories.EventQuests, reward, "order #" + order.Id, order.Id);
            }
            else
            {
                commission = WalletService.Share(reward, ctx.World.Treasury.Commission);
                TreasuryService.Credit(ctx, LedgerCategories.Commission, commission, "order #" + order.Id, order.Id);
            }
            TreasuryService.Debit(ctx, LedgerCategories.Surcharges, order.Surcharge, "order #" + order.Id, order.Id);

            int pool = reward - commission + order.Surcharge;
            int[] shares = Split(pool, returned, run.IsPermanentParty);
            for (int i = 0; i < returned.Count; i++)
            {
                if (shares[i] > 0) WalletService.ReceiveIncome(ctx, returned[i], shares[i], IncomeKind.Reward);
            }

            int trophies = Trophies(ctx, run, order);
            if (trophies > 0) HandInTrophies(ctx, run, returned, trophies);

            QuestParty.Publish(ctx, SimEventType.QuestPaid, run, EventImportance.Normal, QuestSystem.Ids(returned))
                .With("reward", reward).With("commission", commission).With("surcharge", order.Surcharge).With("trophies", trophies);
        }

        /// <summary>Трофеи: бонус (если не потерян), добыча (если не потеряна) и тайник из пещеры.</summary>
        private static int Trophies(SimContext ctx, QuestRun run, Order order)
        {
            OrdersBalance orders = ctx.Data.Balance.Orders;
            int bonus = run.BonusLost ? 0 : WalletService.Share(order.Reward, orders.BonusRewardShare);
            int loot = 0;
            if (!run.LootLost)
            {
                float share = ctx.Rng.Range(orders.LootShare.Min, orders.LootShare.Max);
                loot = (int)Math.Round(order.Reward * share, MidpointRounding.AwayFromZero);
            }
            int cache = run.Discovery != null ? run.Discovery.Loot : 0;
            return bonus + loot + cache;
        }

        /// <summary>
        /// Трофеи сданы трактирщику и поделены. Беспринципный (полюс оси) первым тайно забирает <c>skimShare</c>; с шансом
        /// <c>skimCaughtChance</c> это замечают — раскрытие. Честный на крайнем полюсе, в гильдии не меньше
        /// <c>honestNoSkimDays</c>, раскрывается, сдав трофеи честно.
        /// </summary>
        private static void HandInTrophies(SimContext ctx, QuestRun run, List<Adventurer> returned, int trophies)
        {
            AdventurersBalance people = ctx.Data.Balance.Adventurers;
            Adventurer skimmer = null;
            foreach (Adventurer member in returned)
            {
                float principles = member.GetAxis(AxisId.Principles);
                if (principles < 0f && !AxisMath.IsNeutral(principles, people))
                {
                    skimmer = member;
                    break;
                }
            }

            int left = trophies;
            if (skimmer != null)
            {
                int skimmed = WalletService.Share(trophies, people.SkimShare);
                left -= skimmed;
                WalletService.ReceiveIncome(ctx, skimmer, skimmed, IncomeKind.Loot);
                bool caught = ctx.RollChance(people.SkimCaughtChance, "skim-caught", skimmer);
                QuestParty.Publish(ctx, SimEventType.LootSkimmed, run, EventImportance.Normal, skimmer.Id)
                    .With("amount", skimmed).With("caught", caught);
                if (caught) RevealService.TryRevealAxis(ctx, skimmer, AxisId.Principles, RevealTrigger.UnprincipledCaughtSkimming);
            }

            int[] shares = Split(left, returned, run.IsPermanentParty);
            for (int i = 0; i < returned.Count; i++)
            {
                if (shares[i] > 0) WalletService.ReceiveIncome(ctx, returned[i], shares[i], IncomeKind.Loot);
            }
            QuestParty.Publish(ctx, SimEventType.LootHandedIn, run, EventImportance.Normal, QuestSystem.Ids(returned)).With("amount", left);

            long honestDays = ctx.Calendar.DaysToHours(people.HonestNoSkimDays);
            foreach (Adventurer member in returned)
            {
                if (member == skimmer || !AxisMath.IsOnExtremePole(member.GetAxis(AxisId.Principles), AxisPole.Positive, people)) continue;
                if (ctx.World.Time.TotalHours - member.JoinedAtHours >= honestDays)
                    RevealService.TryRevealAxis(ctx, member, AxisId.Principles, RevealTrigger.HonestChoice);
            }
        }

        /// <summary>
        /// Делёж суммы пропорционально <see cref="Adventurer.PowerScore"/> (все по нулям — поровну) или, если
        /// <paramref name="equal"/>, поровну: доли вниз, остаток — по одной монете самым сильным (поровну — по порядку).
        /// </summary>
        public static int[] Split(int amount, IReadOnlyList<Adventurer> people, bool equal = false)
        {
            var shares = new int[people.Count];
            if (amount <= 0 || people.Count == 0) return shares;

            float total = 0f;
            foreach (Adventurer person in people) total += Math.Max(0f, person.PowerScore);
            int given = 0;
            for (int i = 0; i < people.Count; i++)
            {
                float weight = !equal && total > 0f ? Math.Max(0f, people[i].PowerScore) / total : 1f / people.Count;
                shares[i] = (int)Math.Floor(amount * (double)weight + 1e-6);
                given += shares[i];
            }

            var order = new List<int>();
            for (int i = 0; i < people.Count; i++) order.Add(i);
            if (!equal) order.Sort((a, b) =>
            {
                int byPower = people[b].PowerScore.CompareTo(people[a].PowerScore);
                return byPower != 0 ? byPower : a.CompareTo(b);
            });
            for (int k = 0; given < amount; k = (k + 1) % order.Count, given++) shares[order[k]]++;
            return shares;
        }

        // ---------- Находка ----------

        /// <summary>
        /// Рассказали Регистратору о находке: награда группе — доля награды задания, из казны; событийное задание ждёт ранга
        /// от игрока.
        /// </summary>
        private static void Report(SimContext ctx, QuestRun run, Order order, List<Adventurer> returned)
        {
            QuestDiscovery discovery = run.Discovery;
            DiscoveryDefinition definition = ctx.Data.Get<DiscoveryDefinition>(discovery.DiscoveryId);
            int fee = (int)Math.Round(order.Reward * definition.ReportRewardShare, MidpointRounding.AwayFromZero);
            TreasuryService.Debit(ctx, LedgerCategories.Discoveries, fee, "quest #" + run.Id, run.Id);
            int[] shares = Split(fee, returned, run.IsPermanentParty);
            for (int i = 0; i < returned.Count; i++)
            {
                if (shares[i] > 0) WalletService.ReceiveIncome(ctx, returned[i], shares[i], IncomeKind.Loot);
            }
            discovery.State = DiscoveryState.Reported;

            Adventurer teller = returned.Find(a => a.Id == discovery.SpotterId) ?? returned[0];
            QuestParty.Publish(ctx, SimEventType.Discovery, run, EventImportance.Normal, teller.Id)
                .With("discovery", definition.Id).With("kind", "reported").With("amount", fee);

            Order eventQuest = EventQuests.Create(ctx, run, definition, discovery);
            if (eventQuest != null) discovery.EventOrderId = eventQuest.Id;
        }

        // ---------- Очки, репутация, опыт, черты ----------

        private static void Reward(SimContext ctx, QuestRun run, List<Adventurer> returned)
        {
            DataRegistry data = ctx.Data;
            RanksBalance ranks = data.Balance.Ranks;
            GuildBalance guild = data.Balance.Guild;
            int rankNumber = QuestMath.RankNumber(run.Rank);

            if (run.Completed)
            {
                float points = ranks.For(run.Rank).RankPoints;
                if (run.Result == QuestResult.Brilliant) points *= ranks.BrilliantPointsMultiplier;
                else if (run.Result == QuestResult.Partial) points *= ranks.PartialPointsMultiplier;
                foreach (Adventurer member in returned) GuildRanks.AddPoints(ctx, member, points);
            }

            ReputationService.Change(ctx, (run.Completed ? guild.SuccessReputationPerRank : guild.FailureReputationPerRank) * rankNumber,
                run.Completed ? "quest done" : "quest failed");
            if (run.Completed)
            {
                foreach (Adventurer member in returned)
                {
                    if (TraitRules.FindWithHook(member, TraitHook.BraggartReputation, data) != null)
                        ReputationService.Change(ctx, data.Balance.Traits.BraggartReputationPerSuccess, "braggart");
                }
            }

            Experience(ctx, run, returned);
            Relations(ctx, run);
            foreach (Adventurer member in returned)
            {
                if (run.Completed) member.QuestsCompleted++;
                else member.QuestsFailed++;
            }
            AcquireTraits(ctx, run, returned);
        }

        /// <summary>
        /// Опыт по осям типа задания (главные, второстепенные, далеко — Выживание): дошедшим — по итогу, повернувшим назад —
        /// как за провал. Соперники в одной группе — × <c>rivalExperienceMultiplier</c>. Хладнокровие — за задание с провалом
        /// раунда, Слаженность — за групповое (члену постоянной группы, которая шла вместе, — больше).
        /// </summary>
        private static void Experience(SimContext ctx, QuestRun run, List<Adventurer> returned)
        {
            DataRegistry data = ctx.Data;
            if (!data.TryGet(run.TypeId, out QuestTypeDefinition type)) return;

            var axes = new List<StatId>(type.MainAxes);
            foreach (StatId stat in type.SecondaryAxes)
            {
                if (!axes.Contains(stat)) axes.Add(stat);
            }
            if (run.Distance == OrderDistance.Far && !axes.Contains(StatId.Survival)) axes.Add(StatId.Survival);

            var everyone = new List<Adventurer>();
            foreach (int id in run.Departed)
            {
                if (ctx.World.Adventurers.TryGetActive(id, out Adventurer member)) everyone.Add(member);
            }

            bool group = run.Departed.Count > 1;
            foreach (Adventurer member in returned)
            {
                float multiplier = HasRivalAlong(member, everyone, data) ? data.Balance.Traits.RivalExperienceMultiplier : 1f;
                Growth.ApplyQuestExperience(ctx, member, axes, run.Completed, multiplier);
                if (run.FailedRounds > 0) Growth.ApplyHardQuestComposure(ctx, member);
                if (group) Growth.ApplyGroupQuestCohesion(ctx, member, permanentParty: run.IsPermanentParty && member.PermanentPartyId == run.PartyId);
            }
            foreach (int id in run.TurnedBack)
            {
                if (ctx.World.Adventurers.TryGetActive(id, out Adventurer member)) Growth.ApplyQuestExperience(ctx, member, axes, false);
            }
        }

        private static bool HasRivalAlong(Adventurer member, List<Adventurer> party, DataRegistry data)
        {
            foreach (Adventurer other in party)
            {
                if (other.Id != member.Id && QuestMath.ArePartners(member, other, TraitHook.RivalPartner, data)) return true;
            }
            return false;
        }

        /// <summary>
        /// Каждая пара вышедших вместе и живых: +1 совместное задание; выполнили — +1 совместный успех и отношения +, был провал
        /// раунда — отношения +.
        /// </summary>
        private static void Relations(SimContext ctx, QuestRun run)
        {
            AdventurersBalance people = ctx.Data.Balance.Adventurers;
            for (int i = 0; i < run.Departed.Count; i++)
            {
                for (int j = i + 1; j < run.Departed.Count; j++)
                {
                    int a = run.Departed[i];
                    int b = run.Departed[j];
                    if (!ctx.World.Adventurers.IsActive(a) || !ctx.World.Adventurers.IsActive(b)) continue;
                    RelationService.AddJointQuest(ctx, a, b);
                    if (run.Completed)
                    {
                        RelationService.AddJointSuccess(ctx, a, b);
                        RelationService.Change(ctx, a, b, people.JointSuccessRelation);
                    }
                    if (run.FailedRounds > 0) RelationService.Change(ctx, a, b, people.JointHardQuestRelation);
                }
            }
        }

        /// <summary>
        /// Кошмары — шанс <c>nightmaresAcquireChance</c> после катастрофы или гибели товарища (запоминается тип задания).
        /// Ветеран войны после задания с провалом раунда — шанс срыва в запой. Хвастун, переоценивший себя, раскрывается,
        /// если задание не выполнено.
        /// </summary>
        private static void AcquireTraits(SimContext ctx, QuestRun run, List<Adventurer> returned)
        {
            DataRegistry data = ctx.Data;
            TraitsBalance traits = data.Balance.Traits;
            SpecialTraitDefinition nightmares = TraitRules.FindByHook(data, TraitHook.NightmaresRefuseSimilar);
            bool grim = run.Result == QuestResult.Catastrophe || run.Dead.Count > 0;

            foreach (Adventurer member in returned)
            {
                if (grim && nightmares != null && !member.HasTrait(nightmares.Id)
                    && ctx.RollChance(traits.NightmaresAcquireChance, "nightmares", member)
                    && TraitService.TryAcquire(ctx, member, nightmares.Id)
                    && member.TryGetTrait(nightmares.Id, out TraitInstance acquired))
                {
                    acquired.SourceQuestTypeId = run.TypeId;
                }

                TraitInstance veteran = TraitRules.FindWithHook(member, TraitHook.VeteranBinge, data);
                if (veteran != null && run.FailedRounds > 0 && member.State.Breakdown == BreakdownKind.None
                    && ctx.RollChance(traits.VeteranBingeChance, "veteran-binge", member))
                {
                    StateService.StartBreakdown(ctx, member, BreakdownKind.Binge);
                }

                if (!run.Completed && QuestParty.Contains(run.Overreached, member.Id))
                {
                    TraitInstance braggart = TraitRules.FindWithHook(member, TraitHook.BraggartReputation, data);
                    if (braggart != null) RevealService.TryRevealTrait(ctx, member, braggart.TraitId, RevealTrigger.BraggartOverreached);
                }
            }
        }

        private static EventImportance Importance(QuestResult result)
        {
            switch (result)
            {
                case QuestResult.Catastrophe: return EventImportance.Important;
                case QuestResult.Success: return EventImportance.Normal;
                default: return EventImportance.Notable;
            }
        }

        /// <summary>Ключ уровня для строк и лога: brilliant / success / partial / fail / catastrophe.</summary>
        public static string ResultKey(QuestResult result)
        {
            switch (result)
            {
                case QuestResult.Brilliant: return "brilliant";
                case QuestResult.Success: return "success";
                case QuestResult.Partial: return "partial";
                case QuestResult.Fail: return "fail";
                case QuestResult.Catastrophe: return "catastrophe";
                default: return "none";
            }
        }
    }
}
