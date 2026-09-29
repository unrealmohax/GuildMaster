using System.Globalization;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Значение метки для подстановки: слово с падежными формами и родом (человек, название) или число.
    /// Незаполненная форма падежа заменяется именительным.
    /// </summary>
    public sealed class TextValue
    {
        private readonly NounForms forms;
        private readonly string plain;

        private TextValue(NounForms forms, string plain, GrammaticalGender gender, bool isPerson, TextLink link = default)
        {
            this.forms = forms;
            this.plain = plain ?? string.Empty;
            Gender = gender;
            IsPerson = isPerson;
            Link = link;
        }

        /// <summary>Род для скобок <c>[м|ж]</c> и <c>[м|ж|ср|мн]</c>; не задан — мужской.</summary>
        public GrammaticalGender Gender { get; }

        /// <summary>Человек: скобки рода без привязки <c>@</c> берут род ближайшего человека.</summary>
        public bool IsPerson { get; }

        /// <summary>На что указывает значение (человек, заказ, задание…); нет — <see cref="TextLink.IsNone"/>.</summary>
        public TextLink Link { get; }

        /// <summary>То же значение со ссылкой на объект.</summary>
        public TextValue WithLink(TextLink link) => new TextValue(forms, plain, Gender, IsPerson, link);

        /// <summary>Человек: имя с формами из списка имён (<paramref name="forms"/> может быть <c>null</c> — тогда имя не склоняется).</summary>
        public static TextValue Person(string name, NounForms forms, Gender gender) =>
            new TextValue(forms, name, gender == Core.Gender.Female ? GrammaticalGender.Feminine : GrammaticalGender.Masculine, true);

        /// <summary>Название с формами и родом из данных.</summary>
        public static TextValue Noun(NounForms forms) =>
            new TextValue(forms, forms?.Nominative, forms?.Gender ?? GrammaticalGender.Unspecified, false);

        /// <summary>Несклоняемое слово.</summary>
        public static TextValue Word(string text, GrammaticalGender gender = GrammaticalGender.Unspecified) =>
            new TextValue(null, text, gender, false);

        public static TextValue Number(long number) =>
            new TextValue(null, number.ToString(CultureInfo.InvariantCulture), GrammaticalGender.Unspecified, false);

        /// <summary>Форма в падеже; нет формы — именительный.</summary>
        public string Get(GrammaticalCase grammaticalCase)
        {
            if (forms != null)
            {
                string form = forms.Get(grammaticalCase);
                if (!string.IsNullOrEmpty(form)) return form;
                if (!string.IsNullOrEmpty(forms.Nominative)) return forms.Nominative;
            }
            return plain;
        }

        public override string ToString() => Get(GrammaticalCase.Nominative);
    }

    /// <summary>Откуда движок подстановки берёт значения меток.</summary>
    public interface ITextSource
    {
        /// <summary>Значение метки (<c>имя</c>, <c>место</c>…); нет источника — <c>false</c>.</summary>
        bool TryGet(string label, out TextValue value);
    }
}
