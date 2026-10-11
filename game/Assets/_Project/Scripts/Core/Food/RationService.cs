using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Походные пайки: покупка при выходе, еда в пути и делёжка, потери, пайки погибшего, паёк в городе, когда нет денег,
    /// и общий запас постоянной группы при вступлении, выходе и распаде. Случайность — только бросок потери
    /// (<see cref="TryLose"/>), и только если у группы есть что терять. Числа — <see cref="FoodBalance"/>, правила —
    /// <see cref="RationRules"/>.
    /// <para>
    /// Чей запас: у члена постоянной группы, которая идёт на задание или живёт в городе, — общий запас группы; у остальных
    /// (группа под задание, гость, одиночка, беглец, идущий домой один) — личный.
    /// </para>
    /// </summary>
    public static class RationService
    {
        // ---------- Покупка ----------

        /// <summary>
        /// Пайки при выходе на задание. Нужно приёмов — по ожидаемому заданию с этого часа (<see cref="TravelPlan"/>) + запас.
        /// Каждый, кто не член идущей постоянной группы, покупает себе сам: нужно − личный запас. Постоянная группа покупает в
        /// общий запас: нужно (по запасу решающего) × члены − общий запас; поровну на каждого члена, кому не хватает на долю —
        /// вносит сколько может. Каждый купленный паёк — в доход таверны. Строка задания: купили / хватило / впритык.
        /// </summary>
        internal static void Buy(SimContext ctx, QuestRun run, Order order, IReadOnlyList<Adventurer> going, Party group)
        {
            FoodBalance food = ctx.Data.Balance.Food;
            int meals = TravelPlan.ExpectedMeals(order, ctx.Data, ctx.World.Time.Hour);
            bool permanent = group != null && group.IsPermanent && run.IsPermanentParty && run.PartyId == group.Id;

            var members = new List<Adventurer>();
            int bought = 0, cost = 0;
            bool shortage = false;
            foreach (Adventurer person in going)
            {
                if (permanent && person.PermanentPartyId == group.Id)
                {
                    members.Add(person);
                    continue;
                }
                int wanted = Math.Max(0, RationRules.Need(person, meals, food) - person.Rations.Total);
                (RationTier tier, int count) = RationRules.Afford(person, wanted, ctx.Data.Balance);
                int paid = Purchase(ctx, person, person.Rations, tier, count);
                WriteBuy(ctx, run, person, wanted, tier, count, paid, person.Rations);
                bought += count;
                cost += paid;
                shortage |= count < wanted;
            }

            if (members.Count > 0)
            {
                Adventurer decider = QuestParty.Leader(members, ctx.Data);
                int wanted = Math.Max(0, RationRules.Need(decider, meals, food) * members.Count - group.Rations.Total);
                RationTier tier = RationTier.Regular;
                for (int i = 0; i < members.Count; i++)
                {
                    int part = PartOf(wanted, members.Count, i);
                    if (RationRules.Afford(members[i], part, ctx.Data.Balance).Tier != RationTier.Regular && part > 0) tier = RationTier.Cheap;
                }
                int price = food.Ration(tier).Price;
                for (int i = 0; i < members.Count; i++)
                {
                    int part = PartOf(wanted, members.Count, i);
                    int count = price > 0 ? Math.Min(part, members[i].State.Wallet / price) : part;
                    int paid = Purchase(ctx, members[i], group.Rations, tier, count);
                    WriteBuy(ctx, run, members[i], part, tier, count, paid, group.Rations);
                    bought += count;
                    cost += paid;
                    shortage |= count < part;
                }
            }

            int people = going.Count;
            int days = people > 0 ? (int)Math.Round(GroupStock(ctx.World, run, going) / (double)(people * Math.Max(1, food.MealsPerDay)),
                MidpointRounding.AwayFromZero) : 0;
            string kind = shortage ? "short" : bought > 0 ? "bought" : "enough";
            QuestParty.Publish(ctx, SimEventType.RationsBought, run, EventImportance.Normal, QuestSystem.Ids(going))
                .With("kind", kind).With("count", Math.Max(1, days)).With("amount", bought).With("cost", cost);
        }

        /// <summary>Доля <paramref name="index"/>-го из <paramref name="parts"/>: поровну, остаток — первым.</summary>
        private static int PartOf(int total, int parts, int index) => total / parts + (index < total % parts ? 1 : 0);

        private static int Purchase(SimContext ctx, Adventurer payer, RationStock into, RationTier tier, int count)
        {
            if (count <= 0) return 0;
            int paid = WalletService.Pay(ctx, payer, RationRules.Cost(tier, count, ctx.Data.Balance.Food));
            into.Add(tier, count);
            TreasuryService.CountTavernRations(ctx, tier, count);
            return paid;
        }

        private static void WriteBuy(SimContext ctx, QuestRun run, Adventurer person, int wanted, RationTier tier, int count, int paid, RationStock stock)
        {
            if (!ctx.Log.IsOn(SimLogLevel.Debug)) return;
            StringBuilder line = ctx.Log.Begin(SimLogLevel.Debug);
            line.Append("rations quest #").Append(run.Id.ToString(CultureInfo.InvariantCulture)).Append(' ');
            AdventurerLog.AppendName(line, person)
                .Append(" want=").Append(wanted.ToString(CultureInfo.InvariantCulture))
                .Append(" buy=").Append(count.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(tier)
                .Append(" paid=").Append(paid.ToString(CultureInfo.InvariantCulture))
                .Append(" wallet=").Append(person.State.Wallet.ToString(CultureInfo.InvariantCulture))
                .Append(" stock=").Append(stock.Total.ToString(CultureInfo.InvariantCulture));
            ctx.Log.Commit();
        }

        // ---------- Еда в пути ----------

        /// <summary>
        /// Приём пищи на задании (и по пути домой одному): паёк из своего запаса (сначала дешёвый; у члена постоянной группы —
        /// из общего, кончился — из личного, если он есть); нет — паёк отдаёт товарищ по
        /// заданию (<see cref="FindDonor"/>), отношения пары растут; нет и его — приём без еды, а если у группы не осталось ни
        /// одного пайка — строка «припасы кончились» (раз за задание). Возвращает <c>true</c> — поел.
        /// </summary>
        internal static bool EatOnQuest(SimContext ctx, Adventurer adventurer)
        {
            FoodBalance food = ctx.Data.Balance.Food;
            AdventurerState state = adventurer.State;
            QuestRun run = state.QuestRunId != 0 && ctx.World.Quests.TryGetRun(state.QuestRunId, out QuestRun found) ? found : null;

            RationTier? tier = StockOnQuest(ctx.World, adventurer, run).TakeOne() ?? adventurer.Rations.TakeOne();
            Adventurer donor = null;
            if (!tier.HasValue && run != null)
            {
                List<Adventurer> present = QuestParty.Present(ctx.World, run);
                donor = FindDonor(present, adventurer, ctx.Data);
                if (donor != null)
                {
                    tier = donor.Rations.TakeOne();
                    RelationService.Change(ctx, donor.Id, adventurer.Id, food.ShareRelation);
                    QuestParty.Publish(ctx, SimEventType.RationsShared, run, EventImportance.Normal, donor.Id, adventurer.Id);
                }
            }

            if (!tier.HasValue)
            {
                WriteRation(ctx, adventurer, null, null);
                SimEvent missed = ctx.Events.Publish(SimEventType.QuestMealMissed, EventImportance.Normal, adventurer.Id);
                if (run != null)
                {
                    missed.With("quest", run.Id);
                    if (!run.RationsRanOut && GroupStock(ctx.World, run, QuestParty.Present(ctx.World, run)) == 0)
                    {
                        run.RationsRanOut = true;
                        QuestParty.Publish(ctx, SimEventType.RationsRanOut, run, EventImportance.Notable, adventurer.Id);
                    }
                }
                return false;
            }

            Eat(adventurer, food.Ration(tier.Value));
            WriteRation(ctx, adventurer, tier, donor);
            return true;
        }

        /// <summary>
        /// Кто отдаст паёк голодному: товарищ по заданию с личным запасом, кроме Жадного; Командные — первыми; среди них — с
        /// наибольшим запасом, при равенстве — кто раньше вышел.
        /// </summary>
        internal static Adventurer FindDonor(IReadOnlyList<Adventurer> present, Adventurer hungry, DataRegistry data)
        {
            Adventurer best = null;
            bool bestTeam = false;
            foreach (Adventurer member in present)
            {
                if (member.Id == hungry.Id || member.Rations.IsEmpty || RationRules.IsGreedy(member, data.Balance.Food)) continue;
                bool team = RationRules.IsTeamPlayer(member, data.Balance.Adventurers);
                if (best == null || (team && !bestTeam) || (team == bestTeam && member.Rations.Total > best.Rations.Total))
                {
                    best = member;
                    bestTeam = team;
                }
            }
            return best;
        }

        /// <summary>Запас, из которого человек ест на задании: член идущей постоянной группы — общий, остальные — личный.</summary>
        internal static RationStock StockOnQuest(WorldState world, Adventurer adventurer, QuestRun run)
        {
            if (run != null && run.IsPermanentParty && adventurer.PermanentPartyId == run.PartyId
                && world.Parties.TryGetParty(run.PartyId, out Party party))
                return party.Rations;
            return adventurer.Rations;
        }

        /// <summary>Сколько пайков у группы задания: личные запасы идущих + общий запас идущей постоянной группы.</summary>
        public static int GroupStock(WorldState world, QuestRun run, IReadOnlyList<Adventurer> present)
        {
            int total = 0;
            foreach (Adventurer member in present) total += member.Rations.Total;
            if (run.IsPermanentParty && world.Parties.TryGetParty(run.PartyId, out Party party)) total += party.Rations.Total;
            return total;
        }

        /// <summary>
        /// Мало еды на обратный путь: у группы пайков меньше, чем приёмов пищи на путь домой × людей. Путь домой — пройденный
        /// путь туда, если ещё не дошли, иначе весь путь; считается со следующего часа.
        /// </summary>
        public static bool IsShortForReturn(SimContext ctx, QuestRun run, IReadOnlyList<Adventurer> present)
        {
            if (present.Count == 0) return false;
            int hours = run.Phase == QuestPhase.TravelOut ? Math.Max(1, run.OutHoursDone) : Math.Max(1, run.TravelHours);
            int meals = TravelPlan.MealsDuring(ctx.Data.Balance.Time, ctx.Data.Balance.Food, ctx.World.Time.Hour + 1, hours);
            return GroupStock(ctx.World, run, present) < meals * present.Count;
        }

        // ---------- Потери и гибель ----------

        /// <summary>
        /// Потеря пайков (ступень лестницы провалов или провал события в пути): один бросок на группу с шансом
        /// <c>rationLossChance</c>; удалось — каждый запас группы (личные идущих и общий) теряет <c>rationLossShare</c> (вниз,
        /// но хотя бы 1). Пайков нет — броска нет. Строка задания «пропала часть припасов».
        /// </summary>
        internal static void TryLose(SimContext ctx, QuestRun run)
        {
            List<Adventurer> present = QuestParty.Present(ctx.World, run);
            if (present.Count == 0 || GroupStock(ctx.World, run, present) == 0) return;
            FoodBalance food = ctx.Data.Balance.Food;
            if (!ctx.RollChance(food.RationLossChance, "ration-loss", null, "quest", run.Id)) return;

            int lost = 0;
            foreach (Adventurer member in present) lost += LoseShare(member.Rations, food);
            if (run.IsPermanentParty && ctx.World.Parties.TryGetParty(run.PartyId, out Party party)) lost += LoseShare(party.Rations, food);
            QuestParty.Publish(ctx, SimEventType.QuestLoss, run, EventImportance.Notable, QuestSystem.Ids(present))
                .With("kind", "rations").With("amount", lost);
        }

        private static int LoseShare(RationStock stock, FoodBalance food)
        {
            if (stock.IsEmpty) return 0;
            int count = Math.Max(1, (int)Math.Floor(stock.Total * food.RationLossShare));
            return stock.Remove(count);
        }

        /// <summary>
        /// Пайки погибшего на задании: член идущей постоянной группы — в общий запас; иначе поровну живым товарищам по заданию
        /// (по типам, остаток от деления пропадает). Не осталось никого — пропадают.
        /// </summary>
        internal static void OnDeath(SimContext ctx, QuestRun run, Adventurer victim)
        {
            RationStock stock = victim.Rations;
            if (stock.IsEmpty) return;
            if (run.IsPermanentParty && victim.PermanentPartyId == run.PartyId && ctx.World.Parties.TryGetParty(run.PartyId, out Party party))
            {
                stock.MoveAllTo(party.Rations);
                return;
            }
            List<Adventurer> living = QuestParty.Present(ctx.World, run);
            living.RemoveAll(a => a.Id == victim.Id);
            SplitInto(stock, living);
        }

        /// <summary>Разделить запас поровну (по типам) между людьми; остаток от деления пропадает, запас пустеет.</summary>
        private static void SplitInto(RationStock stock, IReadOnlyList<Adventurer> people)
        {
            if (people.Count > 0)
            {
                for (int t = 0; t < Vocabulary.RationTierCount; t++)
                {
                    var tier = (RationTier)t;
                    int share = stock.GetCount(tier) / people.Count;
                    foreach (Adventurer person in people) person.Rations.Add(tier, share);
                }
            }
            stock.Clear();
        }

        // ---------- Город ----------

        /// <summary>
        /// В городе на еду нет денег даже на дешёвую — человек ест паёк: член постоянной группы — из общего запаса, остальные —
        /// из личного. Порция в доход таверны не идёт: паёк уже куплен. Возвращает <c>true</c> — поел.
        /// </summary>
        internal static bool EatInTown(SimContext ctx, Adventurer adventurer)
        {
            RationTier? tier = null;
            if (adventurer.PermanentPartyId != 0 && ctx.World.Parties.TryGetParty(adventurer.PermanentPartyId, out Party party)) tier = party.Rations.TakeOne();
            tier = tier ?? adventurer.Rations.TakeOne();
            if (!tier.HasValue) return false;

            Eat(adventurer, ctx.Data.Balance.Food.Ration(tier.Value));
            adventurer.State.LastMealTier = null;
            adventurer.State.LastMealWasRation = true;
            WriteRation(ctx, adventurer, tier, null);
            return true;
        }

        private static void Eat(Adventurer adventurer, MealBalance ration)
        {
            AdventurerState state = adventurer.State;
            state.Satiety = FoodRules.AfterMeal(state.Satiety, ration);
            state.TasteToday += ration.Taste;
        }

        private static void WriteRation(SimContext ctx, Adventurer adventurer, RationTier? tier, Adventurer donor)
        {
            if (!ctx.Log.IsOn(SimLogLevel.Debug)) return;
            StringBuilder line = ctx.Log.Begin(SimLogLevel.Debug);
            AdventurerLog.AppendName(line.Append("ration "), adventurer).Append(' ');
            if (tier.HasValue) line.Append(tier.Value);
            else line.Append("none");
            if (donor != null) AdventurerLog.AppendName(line.Append(" from "), donor);
            line.Append(" (satiety=").Append(AdventurerLog.Number(adventurer.State.Satiety)).Append(')');
            ctx.Log.Commit();
        }

        // ---------- Постоянная группа ----------

        /// <summary>Вступил в постоянную группу: личный запас — в общий.</summary>
        internal static void OnJoined(Party party, Adventurer person) => person.Rations.MoveAllTo(party.Rations);

        /// <summary>Ушёл из постоянной группы живым, группа осталась: уносит долю общего запаса (запас / члены до ухода, вниз).</summary>
        internal static void OnLeft(Party party, Adventurer person, int membersBefore) => party.Rations.MoveShareTo(person.Rations, membersBefore);

        /// <summary>Постоянная группа распалась: общий запас — поровну оставшимся участникам в личный (остаток пропадает).</summary>
        internal static void OnDisbanded(Party party, IReadOnlyList<Adventurer> members) => SplitInto(party.Rations, members);
    }
}
