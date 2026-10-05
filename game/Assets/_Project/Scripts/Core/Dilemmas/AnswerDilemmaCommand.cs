namespace GuildMaster.Core
{
    /// <summary>
    /// Игрок отвечает на обращение вариантом. Закрытое обращение, скрытый вариант или вариант, на который в казне не хватает денег, —
    /// команда ничего не делает.
    /// </summary>
    public sealed class AnswerDilemmaCommand : ICommand
    {
        public AnswerDilemmaCommand(int dilemmaId, int optionIndex)
        {
            DilemmaId = dilemmaId;
            OptionIndex = optionIndex;
        }

        public int DilemmaId { get; }
        public int OptionIndex { get; }

        public void Apply(SimContext ctx)
        {
            if (!ctx.Data.HasDefinitions || !ctx.World.Dilemmas.TryGetOpen(DilemmaId, out Dilemma dilemma)) return;
            DilemmaService.Answer(ctx, dilemma, OptionIndex, timedOut: false);
        }
    }
}
