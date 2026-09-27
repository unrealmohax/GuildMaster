using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Все числа баланса, по разделам. В коде констант баланса нет.
    /// Значения по умолчанию в коде — стартовые числа из документов: ими заполняется новый ассет;
    /// дальше источник истины — ассет, правится без перекомпиляции.
    /// </summary>
    [CreateAssetMenu(menuName = "GuildMaster/Balance Settings", fileName = "BalanceSettings")]
    public sealed class BalanceSettings : ScriptableObject
    {
        [SerializeField] private TimeBalance time = new TimeBalance();
        [SerializeField] private OrdersBalance orders = new OrdersBalance();
        [SerializeField] private RanksBalance ranks = new RanksBalance();
        [SerializeField] private RoundsBalance rounds = new RoundsBalance();
        [SerializeField] private TravelBalance travel = new TravelBalance();
        [SerializeField] private AdventurersBalance adventurers = new AdventurersBalance();
        [SerializeField] private GrowthBalance growth = new GrowthBalance();
        [SerializeField] private ExpensesBalance expenses = new ExpensesBalance();
        [SerializeField] private StateBalance state = new StateBalance();
        [SerializeField] private HealthBalance health = new HealthBalance();
        [SerializeField] private DecisionsBalance decisions = new DecisionsBalance();
        [SerializeField] private TensionBalance tension = new TensionBalance();
        [SerializeField] private TraitsBalance traits = new TraitsBalance();
        [SerializeField] private GuildBalance guild = new GuildBalance();
        [SerializeField] private StaffBalance staff = new StaffBalance();
        [SerializeField] private EconomyBalance economy = new EconomyBalance();
        [SerializeField] private DecreesBalance decrees = new DecreesBalance();
        [SerializeField] private DilemmasBalance dilemmas = new DilemmasBalance();
        [SerializeField] private FeedBalance feed = new FeedBalance();

        public TimeBalance Time => time;
        public OrdersBalance Orders => orders;
        public RanksBalance Ranks => ranks;
        public RoundsBalance Rounds => rounds;
        public TravelBalance Travel => travel;
        public AdventurersBalance Adventurers => adventurers;
        public GrowthBalance Growth => growth;
        public ExpensesBalance Expenses => expenses;
        public StateBalance State => state;
        public HealthBalance Health => health;
        public DecisionsBalance Decisions => decisions;
        public TensionBalance Tension => tension;
        public TraitsBalance Traits => traits;
        public GuildBalance Guild => guild;
        public StaffBalance Staff => staff;
        public EconomyBalance Economy => economy;
        public DecreesBalance Decrees => decrees;
        public DilemmasBalance Dilemmas => dilemmas;
        public FeedBalance Feed => feed;
    }
}
