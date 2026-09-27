using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Метки описания заказа: <c>{заказчик}</c>, <c>{место}</c>, <c>{враг}</c>, <c>{груз}</c> — названия из заказа.</summary>
    public sealed class OrderTextSource : ITextSource
    {
        private readonly Order order;

        public OrderTextSource(Order order)
        {
            this.order = order;
        }

        public bool TryGet(string label, out TextValue value)
        {
            NounForms forms;
            switch (label)
            {
                case "заказчик": forms = order.Client; break;
                case "место": forms = order.Place; break;
                case "враг": forms = order.Enemy; break;
                case "груз": forms = order.Cargo; break;
                default: forms = null; break;
            }
            value = forms != null ? TextValue.Noun(forms) : null;
            return value != null;
        }
    }
}
