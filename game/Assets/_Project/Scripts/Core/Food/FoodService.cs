using System.Globalization;
using System.Text;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Еда и сытость человека. Вызывает <see cref="StateSystem"/> каждый час. На задании сытость не меняется и человек не ест:
    /// группа кормится в пути сама. Случайных чисел не тратит.
    /// </summary>
    public static class FoodService
    {
        /// <summary>
        /// Час человека вне задания: в час приёма пищи — приём (<see cref="Eat"/>), затем сытость убывает на
        /// <c>satietyDecayPerHour</c>. Голодный — стресс <c>hungryStressPerHour</c> (× черты). Переход в голод — событие
        /// <see cref="SimEventType.HungerStarted"/>, в истощение — <see cref="SimEventType.Exhausted"/> (автопауза).
        /// </summary>
        internal static void ApplyHour(SimContext ctx, Adventurer adventurer)
        {
            AdventurerState state = adventurer.State;
            if (state.IsOnQuest()) return;

            FoodBalance food = ctx.Data.Balance.Food;
            HungerStage before = FoodRules.StageOf(state.Satiety, food);
            if (food.IsMealHour(ctx.World.Time.Hour)) Eat(ctx, adventurer);
            state.Satiety = StateRules.Clamp(state.Satiety - food.SatietyDecayPerHour);

            HungerStage after = FoodRules.StageOf(state.Satiety, food);
            if (after >= HungerStage.Hungry) StateService.AddStress(ctx, adventurer, food.HungryStressPerHour);
            if (after == HungerStage.Starving && before < HungerStage.Starving)
                ctx.Events.Publish(SimEventType.HungerStarted, EventImportance.Notable, adventurer.Id).With("satiety", state.Satiety);
            else if (after == HungerStage.Exhausted && before != HungerStage.Exhausted)
                ctx.Events.Publish(SimEventType.Exhausted, EventImportance.Important, adventurer.Id);
        }

        /// <summary>
        /// Приём пищи в таверне гильдии: тип — по бюджету (<see cref="FoodRules.Choose"/>), оплата из кошелька, порция — в доход
        /// таверны, сытость и вкус дня. Новичка по распоряжению кормит гильдия простой едой (статья «Распоряжения»); если по бюджету
        /// он выбрал бы роскошную — платит её сам. Нет денег даже на дешёвую — приём пропущен, кошелёк отмечается пустым.
        /// </summary>
        internal static void Eat(SimContext ctx, Adventurer adventurer)
        {
            AdventurerState state = adventurer.State;
            FoodBalance food = ctx.Data.Balance.Food;
            MealTier? choice = FoodRules.Choose(adventurer, ctx.Data, out int spare);
            bool guildPays = choice != MealTier.Luxury && DecreeRules.GuildPaysLiving(adventurer, ctx.World, ctx.Data);

            if (!guildPays && !choice.HasValue)
            {
                state.LastMealTier = null;
                WalletService.MarkUnpaid(ctx, adventurer, food.Cheap.Price);
                if (ctx.Log.IsOn(SimLogLevel.Debug))
                {
                    StringBuilder line = ctx.Log.Begin(SimLogLevel.Debug);
                    AdventurerLog.AppendName(line.Append("meal "), adventurer)
                        .Append(" skipped (wallet=").Append(state.Wallet.ToString(CultureInfo.InvariantCulture)).Append(')');
                    ctx.Log.Commit();
                }
                ctx.Events.Publish(SimEventType.MealSkipped, EventImportance.Normal, adventurer.Id);
                return;
            }

            MealTier tier = guildPays ? MealTier.Simple : choice.Value;
            MealBalance meal = food.Meal(tier);
            if (guildPays) DecreeService.PayMeal(ctx, adventurer, meal.Price);
            else WalletService.Pay(ctx, adventurer, meal.Price);

            state.Satiety = FoodRules.AfterMeal(state.Satiety, meal);
            state.TasteToday += meal.Taste;
            state.LastMealTier = tier;
            TreasuryService.CountTavernMeal(ctx, tier);

            if (ctx.Log.IsOn(SimLogLevel.Debug))
            {
                StringBuilder line = ctx.Log.Begin(SimLogLevel.Debug);
                AdventurerLog.AppendName(line.Append("meal "), adventurer)
                    .Append(' ').Append(tier).Append(' ').Append(meal.Price.ToString(CultureInfo.InvariantCulture))
                    .Append(" (wallet=").Append(state.Wallet.ToString(CultureInfo.InvariantCulture))
                    .Append(", spare=").Append(spare.ToString(CultureInfo.InvariantCulture));
                if (guildPays) line.Append(", guild pays");
                line.Append(')');
                ctx.Log.Commit();
            }
            ctx.Events.Publish(SimEventType.MealEaten, EventImportance.Normal, adventurer.Id)
                .With("tier", tier)
                .With("guildPays", guildPays);
        }

        /// <summary>Сутки (00:00): истощённый вне задания теряет <c>exhaustionContentmentPerDay</c> довольства, разово.</summary>
        internal static void ApplyDay(SimContext ctx, Adventurer adventurer)
        {
            FoodBalance food = ctx.Data.Balance.Food;
            if (adventurer.State.IsOnQuest() || FoodRules.StageOf(adventurer, food) != HungerStage.Exhausted) return;
            StateService.ChangeContentment(ctx, adventurer, -food.ExhaustionContentmentPerDay);
        }
    }
}
