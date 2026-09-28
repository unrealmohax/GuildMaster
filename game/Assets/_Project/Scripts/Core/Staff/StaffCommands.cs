namespace GuildMaster.Core
{
    /// <summary>
    /// Игрок предлагает кандидату в персонал зарплату. Не меньше просимой — нанят; меньше — согласие с шансом
    /// (<see cref="StaffRules.AcceptChance"/>), отказ — кандидат уходит. Нанятый работает сразу.
    /// </summary>
    public sealed class OfferSalaryCommand : ICommand
    {
        public OfferSalaryCommand(int candidateId, int amount)
        {
            CandidateId = candidateId;
            Amount = amount;
        }

        public int CandidateId { get; }
        public int Amount { get; }

        public void Apply(SimContext ctx) => StaffService.Offer(ctx, CandidateId, Amount);
    }

    /// <summary>Игрок увольняет сотрудника — без выходного пособия. Должность становится вакансией.</summary>
    public sealed class DismissStaffCommand : ICommand
    {
        public DismissStaffCommand(int staffId)
        {
            StaffId = staffId;
        }

        public int StaffId { get; }

        public void Apply(SimContext ctx) => StaffService.Dismiss(ctx, StaffId);
    }
}
