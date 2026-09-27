using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Системы такта в обязательном порядке. Новая система встаёт на своё место:
    ///  1. CommandSystem   2. TimeSystem      3. OrderSystem     4. QuestSystem
    ///  5. ActivitySystem  6. StateSystem     7. HealthSystem    8. DecisionSystem
    ///  9. PartySystem    10. DilemmaSystem  11. EconomySystem  12. BuildingSystem, StaffSystem
    /// 13. AdventurerSystem, RecruitSystem   14. DecreeSystem   15. FeedSystem     16. AutopauseSystem
    /// Шаг 13 дополнен AdventurerSystem. Перед FeedSystem — MonthReportSystem: отчёт месяца собирается после всех систем,
    /// которые меняют мир.
    /// </summary>
    public static class SimulationSystems
    {
        public static List<ISimSystem> CreateDefault() => new List<ISimSystem>
        {
            new CommandSystem(),
            new TimeSystem(),
            new ActivitySystem(),
            new StateSystem(),
            new HealthSystem(),
            new DecisionSystem(),
            new EconomySystem(),
            new AdventurerSystem(),
            new RecruitSystem(),
            new MonthReportSystem(), // после всех систем, которые меняют мир
            new FeedSystem(), // после всех систем, которые публикуют события
            new AutopauseSystem(), // всегда последняя: видит события всех систем такта
        };
    }
}
