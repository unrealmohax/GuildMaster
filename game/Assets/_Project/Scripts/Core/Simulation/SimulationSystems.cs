using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Системы такта в обязательном порядке (ТЗ 01 → «Порядок систем в такте»). Новая система встаёт на своё место:
    ///  1. CommandSystem   2. TimeSystem      3. OrderSystem     4. QuestSystem
    ///  5. ActivitySystem  6. StateSystem     7. HealthSystem    8. DecisionSystem
    ///  9. PartySystem    10. DilemmaSystem  11. EconomySystem  12. BuildingSystem, StaffSystem
    /// 13. RecruitSystem  14. DecreeSystem   15. FeedSystem     16. AutopauseSystem
    /// </summary>
    public static class SimulationSystems
    {
        public static List<ISimSystem> CreateDefault() => new List<ISimSystem>
        {
            new CommandSystem(),
            new TimeSystem(),
        };
    }
}
