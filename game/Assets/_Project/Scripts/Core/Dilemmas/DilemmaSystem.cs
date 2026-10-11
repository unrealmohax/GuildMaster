using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Обращения в такте (после решений людей). Каждый час: снять открытые, если тот, кого они касаются, ушёл из гильдии;
    /// по сроку ответа — вариант по умолчанию; по событиям такта — «Беглец вернулся» (без лимита открытых) и «Ссора из-за добычи»
    /// (после группового задания с трофеями). Раз в сутки в час <c>checkHour</c> — триггеры по состоянию мира: просьба в долг,
    /// раненый рвётся на задание, влюблённые просят одну группу, Трактирщик предлагает праздник.
    /// Обращения вытекают из состояния: триггер проверяет условия, перезарядку (на человека или на гильдию), лимит открытых
    /// (<c>maxOpen</c>) и одно открытое обращение этой дилеммы на человека. Бросок тратится, только когда все условия выполнены.
    /// Ответ игрока — <see cref="AnswerDilemmaCommand"/>.
    /// </summary>
    public sealed class DilemmaSystem : ISimSystem
    {
        private readonly List<SimEvent> tickEvents = new List<SimEvent>();

        public string Name => nameof(DilemmaSystem);

        public void Tick(SimContext ctx)
        {
            if (!ctx.Data.HasDefinitions) return;

            WithdrawStale(ctx);
            TimeOut(ctx);

            tickEvents.Clear();
            foreach (SimEvent simEvent in ctx.Events.Events)
            {
                if (simEvent.Type == SimEventType.DeserterReturned || simEvent.Type == SimEventType.LootHandedIn) tickEvents.Add(simEvent);
            }
            foreach (SimEvent simEvent in tickEvents)
            {
                if (simEvent.Type == SimEventType.DeserterReturned) DeserterReturned(ctx, simEvent);
                else LootDispute(ctx, simEvent);
            }

            if (ctx.World.Time.Hour == ctx.Data.Balance.Dilemmas.CheckHour) DailyTriggers(ctx);
        }

        private static void WithdrawStale(SimContext ctx)
        {
            DilemmaBook book = ctx.World.Dilemmas;
            if (book.Open.Count == 0) return;
            foreach (Dilemma dilemma in new List<Dilemma>(book.Open))
            {
                bool gone = (dilemma.SubjectId != 0 && !ctx.World.Adventurers.TryGetActive(dilemma.SubjectId, out _))
                    || (dilemma.PartnerId != 0 && !ctx.World.Adventurers.TryGetActive(dilemma.PartnerId, out _))
                    || (dilemma.StaffId != 0 && !HasStaff(ctx.World, dilemma.StaffId));
                if (gone) DilemmaService.Withdraw(ctx, dilemma);
            }
        }

        private static void TimeOut(SimContext ctx)
        {
            DilemmaBook book = ctx.World.Dilemmas;
            if (book.Open.Count == 0) return;
            long now = ctx.World.Time.TotalHours;
            foreach (Dilemma dilemma in new List<Dilemma>(book.Open))
            {
                if (now < dilemma.DeadlineAtHours || !ctx.Data.TryGet(dilemma.DefinitionId, out DilemmaDefinition definition)) continue;
                DilemmaService.Answer(ctx, dilemma, definition.TimeoutOption, timedOut: true);
            }
        }

        // ---------- По событиям ----------

        /// <summary>Беглец вернулся и просит оставить его: брошенные — кто остался в группе, когда он сбежал. Лимит открытых не действует.</summary>
        private static void DeserterReturned(SimContext ctx, SimEvent simEvent)
        {
            DilemmaDefinition definition = DilemmaRules.Find(ctx.Data, DilemmaTrigger.DeserterReturned);
            if (definition == null || simEvent.Participants.Count == 0) return;
            if (!ctx.World.Adventurers.TryGetActive(simEvent.Participants[0], out Adventurer deserter)) return;
            if (ctx.World.Dilemmas.IsAwaiting(DilemmaTrigger.DeserterReturned, deserter.Id)) return;

            simEvent.TryGet("quest", out int questId);
            var abandoned = new List<int>();
            if (ctx.World.Quests.TryGetRun(questId, out QuestRun run)) abandoned.AddRange(run.GetAbandonedBy(deserter.Id));
            DilemmaService.Open(ctx, definition, d =>
            {
                d.SubjectId = deserter.Id;
                d.QuestRunId = questId;
                d.SetAbandoned(abandoned);
            });
        }

        /// <summary>
        /// После группового задания с трофеями: {A} — заметивший утаивание, иначе самый жадный (ось Деньги не ниже
        /// <c>lootDisputeMoneyAxis</c>); {B} — с худшими отношениями к {A}, ниже <c>lootDisputeRelationBelow</c>. Шанс
        /// <c>lootDisputeChance</c>.
        /// </summary>
        private static void LootDispute(SimContext ctx, SimEvent simEvent)
        {
            DilemmaDefinition definition = DilemmaRules.Find(ctx.Data, DilemmaTrigger.LootDispute);
            if (definition == null || simEvent.Participants.Count < 2 || !CanOpenMore(ctx)) return;
            simEvent.TryGet("quest", out int questId);
            if (!ctx.World.Quests.TryGetRun(questId, out QuestRun run)) return;

            var returned = new List<Adventurer>();
            foreach (int id in simEvent.Participants)
            {
                if (ctx.World.Adventurers.TryGetActive(id, out Adventurer member)) returned.Add(member);
            }
            if (returned.Count < 2) return;

            DilemmasBalance balance = ctx.Data.Balance.Dilemmas;
            Adventurer a = null;
            if (run.SkimCaught) a = returned.Find(m => m.Id == run.SkimmerId);
            if (a == null)
            {
                foreach (Adventurer member in returned)
                {
                    float money = member.GetAxis(AxisId.Money);
                    if (money >= balance.LootDisputeMoneyAxis && (a == null || money > a.GetAxis(AxisId.Money))) a = member;
                }
            }
            if (a == null || !IsFree(ctx, definition, a)) return;

            Adventurer b = null;
            float worst = balance.LootDisputeRelationBelow;
            foreach (Adventurer member in returned)
            {
                if (member == a || ctx.World.Dilemmas.IsAwaiting(definition.Trigger, member.Id)) continue;
                float relation = ctx.World.Relations.GetValue(a.Id, member.Id);
                if (relation >= worst) continue;
                worst = relation;
                b = member;
            }
            if (b == null || !ctx.RollChance(balance.LootDisputeChance, "loot-dispute", a, "relation", worst)) return;

            DilemmaService.Open(ctx, definition, d =>
            {
                d.SubjectId = a.Id;
                d.PartnerId = b.Id;
                d.QuestRunId = run.Id;
                d.Place = run.Place;
            });
        }

        // ---------- Раз в сутки ----------

        private static void DailyTriggers(SimContext ctx)
        {
            foreach (DilemmaDefinition definition in ctx.Data.All<DilemmaDefinition>())
            {
                switch (definition.Trigger)
                {
                    case DilemmaTrigger.LoanRequest: LoanRequests(ctx, definition); break;
                    case DilemmaTrigger.WoundedWantsQuest: WoundedWantQuests(ctx, definition); break;
                    case DilemmaTrigger.LoversSameParty: Lovers(ctx, definition); break;
                    case DilemmaTrigger.TavernFeast: TavernFeast(ctx, definition); break;
                }
            }
        }

        /// <summary>
        /// Просьба в долг: долга перед гильдией нет и либо семья в нужде, либо кошелёк меньше расходов на <c>loanWalletDays</c> дней
        /// и (Семейный, Пьяница или шанс <c>loanWeeklyChance</c> в неделю — бросок раз в сутки). Сумма — расходы на <c>loanSumDays</c>
        /// дней. Причина — <see cref="PickReason"/>; Семейного обращение раскрывает.
        /// </summary>
        private static void LoanRequests(SimContext ctx, DilemmaDefinition definition)
        {
            BalanceSettings balance = ctx.Data.Balance;
            float daily = DilemmaRules.DailyFromWeekly(balance.Dilemmas.LoanWeeklyChance);
            foreach (Adventurer adventurer in new List<Adventurer>(ctx.World.Adventurers.Active))
            {
                if (!CanOpenMore(ctx)) return;
                if (!IsFree(ctx, definition, adventurer) || adventurer.State.DebtToGuild > 0) continue;
                if (!adventurer.State.FamilyInNeed)
                {
                    int dailyCost = WalletService.DailyLivingCost(adventurer, balance);
                    if (adventurer.State.Wallet >= balance.Dilemmas.LoanWalletDays * dailyCost) continue;

                    bool family = TraitRules.FindWithHook(adventurer, TraitHook.FamilySendsMoneyHome, ctx.Data) != null;
                    bool drunkard = TraitRules.FindWithHook(adventurer, TraitHook.DrunkardSkipsDay, ctx.Data) != null;
                    if (!family && !drunkard && !ctx.RollChance(daily, "loan-request", adventurer, "wallet", adventurer.State.Wallet)) continue;
                }

                string reason = PickReason(ctx, definition, adventurer);
                DilemmaService.Open(ctx, definition, d =>
                {
                    d.SubjectId = adventurer.Id;
                    d.Amount = DilemmaRules.LoanAmount(adventurer, balance);
                    d.Reason = reason;
                });
            }
        }

        /// <summary>
        /// Причина просьбы, первая подходящая по порядку: болеет родной (метка <c>{родной}</c>), черта человека, семья в нужде; иначе
        /// нейтральная наугад (бросок — только если нейтральных больше одной).
        /// </summary>
        private static string PickReason(SimContext ctx, DilemmaDefinition definition, Adventurer adventurer)
        {
            if (adventurer.Family.TryGetIll(out FamilyMember ill))
            {
                foreach (DilemmaReason reason in definition.Reasons)
                {
                    if (reason != null && reason.Condition == DilemmaReasonCondition.FamilyMemberIll) return IllReason(ctx, reason, adventurer, ill);
                }
            }
            foreach (DilemmaReason reason in definition.Reasons)
            {
                if (reason?.RequiresTrait != null && adventurer.HasTrait(reason.RequiresTrait.Id)) return reason.Text;
            }
            var neutral = new List<string>();
            foreach (DilemmaReason reason in definition.Reasons)
            {
                if (reason == null) continue;
                if (reason.Condition == DilemmaReasonCondition.FamilyInNeed && reason.RequiresTrait == null && adventurer.State.FamilyInNeed) return reason.Text;
                if (reason.IsNeutral) neutral.Add(reason.Text);
            }
            if (neutral.Count == 0) return string.Empty;
            return neutral.Count == 1 ? neutral[0] : neutral[ctx.Rng.Range(0, neutral.Count)];
        }

        /// <summary>Причина «болеет родной»: метки <c>{имя}</c> и <c>{родной}</c> подставляются сразу. Ошибки разметки — в лог.</summary>
        private static string IllReason(SimContext ctx, DilemmaReason reason, Adventurer adventurer, FamilyMember ill)
        {
            var errors = new List<string>();
            string text = TextRenderer.Render(reason.Text, new RelativeTextSource(adventurer, ill, ctx.Data), errors);
            foreach (string error in errors) ctx.Log.Write(SimLogLevel.Error, "loan reason: {0}", error);
            return text;
        }

        /// <summary>
        /// Раненый рвётся на задание: тяжёлая рана (без разрешения ходить с ней) и кошелёк меньше расходов на <c>woundedWalletDays</c>
        /// дней или Семейный.
        /// </summary>
        private static void WoundedWantQuests(SimContext ctx, DilemmaDefinition definition)
        {
            BalanceSettings balance = ctx.Data.Balance;
            foreach (Adventurer adventurer in ctx.World.Adventurers.Active)
            {
                if (!CanOpenMore(ctx)) return;
                AdventurerState state = adventurer.State;
                if (!state.IsLaidUpByWound() || !IsFree(ctx, definition, adventurer)) continue;
                int dailyCost = WalletService.DailyLivingCost(adventurer, balance);
                bool poor = state.Wallet < balance.Dilemmas.WoundedWalletDays * dailyCost;
                bool family = TraitRules.FindWithHook(adventurer, TraitHook.FamilySendsMoneyHome, ctx.Data) != null;
                if (!poor && !family) continue;

                DilemmaService.Open(ctx, definition, d => d.SubjectId = adventurer.Id);
            }
        }

        /// <summary>
        /// Влюблённые просят одну группу: пара с нераскрытой чертой Влюблённого, оба в гильдии не меньше <c>loversMinDaysInGuild</c>
        /// дней, не на задании. Раз на пару: обращение раскрывает черту, и пара больше не подходит.
        /// </summary>
        private static void Lovers(SimContext ctx, DilemmaDefinition definition)
        {
            long now = ctx.World.Time.TotalHours;
            long minHours = ctx.Calendar.DaysToHours(ctx.Data.Balance.Dilemmas.LoversMinDaysInGuild);
            foreach (Adventurer adventurer in ctx.World.Adventurers.Active)
            {
                if (!CanOpenMore(ctx)) return;
                TraitInstance lover = TraitRules.FindWithHook(adventurer, TraitHook.LoverPartner, ctx.Data);
                if (lover == null || lover.Revealed || lover.PartnerId == 0) continue;
                if (!ctx.World.Adventurers.TryGetActive(lover.PartnerId, out Adventurer partner)) continue;
                if (now - adventurer.JoinedAtHours < minHours || now - partner.JoinedAtHours < minHours) continue;
                if (!IsFree(ctx, definition, adventurer) || !IsFree(ctx, definition, partner)) continue;

                DilemmaService.Open(ctx, definition, d =>
                {
                    d.SubjectId = adventurer.Id;
                    d.PartnerId = partner.Id;
                });
            }
        }

        /// <summary>
        /// Трактирщик предлагает праздник: Трактирщик нанят, средний стресс людей выше <c>feastAverageStress</c> или гибель в гильдии
        /// за последние <c>feastRecentDeathDays</c> дней. Перезарядка — на гильдию.
        /// </summary>
        private static void TavernFeast(SimContext ctx, DilemmaDefinition definition)
        {
            if (!CanOpenMore(ctx) || ctx.World.Dilemmas.IsOpen(definition.Id)) return;
            if (!StaffRules.TryGetWithEffect(ctx.World, ctx.Data, StaffLevelEffect.TavernStressRelief, out StaffMember innkeeper)) return;
            if (IsCoolingDown(ctx, definition, 0)) return;

            DilemmasBalance balance = ctx.Data.Balance.Dilemmas;
            IReadOnlyList<Adventurer> people = ctx.World.Adventurers.Active;
            if (people.Count == 0) return;
            float stress = 0f;
            foreach (Adventurer adventurer in people) stress += adventurer.State.Stress;
            bool tense = stress / people.Count > balance.FeastAverageStress;
            if (!tense && !DilemmaRules.HadRecentDeath(ctx.World, ctx.Calendar, ctx.World.Time.TotalHours, balance.FeastRecentDeathDays)) return;

            DilemmaService.Open(ctx, definition, d => d.StaffId = innkeeper.Id);
        }

        // ---------- Общие условия ----------

        private static bool CanOpenMore(SimContext ctx) => ctx.World.Dilemmas.Open.Count < ctx.Data.Balance.Dilemmas.MaxOpen;

        /// <summary>Человек может обратиться с этой дилеммой: не на задании, такой же не ждёт ответа, перезарядка прошла.</summary>
        private static bool IsFree(SimContext ctx, DilemmaDefinition definition, Adventurer adventurer) =>
            !adventurer.State.IsOnQuest()
            && !ctx.World.Dilemmas.IsAwaiting(definition.Trigger, adventurer.Id)
            && !IsCoolingDown(ctx, definition, definition.CooldownPerGuild ? 0 : adventurer.Id);

        private static bool IsCoolingDown(SimContext ctx, DilemmaDefinition definition, int adventurerId)
        {
            if (!ctx.World.Dilemmas.TryGetLastArrival(definition.Id, adventurerId, out long last)) return false;
            long cooldown = ctx.Calendar.DaysToHours(DilemmaRules.CooldownDays(definition, ctx.Data.Balance.Dilemmas));
            return ctx.World.Time.TotalHours - last < cooldown;
        }

        private static bool HasStaff(WorldState world, int staffId)
        {
            foreach (StaffMember member in world.Staff.Members)
            {
                if (member.Id == staffId) return true;
            }
            return false;
        }
    }
}
