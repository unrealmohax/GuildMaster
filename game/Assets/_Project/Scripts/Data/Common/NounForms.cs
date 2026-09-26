using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Шесть падежных форм слова и его род для подстановок: <c>{место:р}</c> → «у старой мельницы»,
    /// <c>{постройка} готов[|а|о]@постройка</c> → «Общежитие готово».
    /// В прототипе пока заполнены именительный падеж и род; остальные формы не заполнены.
    /// </summary>
    [Serializable]
    public sealed class NounForms
    {
        [SerializeField] private string nominative;
        [SerializeField] private string genitive;
        [SerializeField] private string dative;
        [SerializeField] private string accusative;
        [SerializeField] private string instrumental;
        [SerializeField] private string prepositional;
        [SerializeField] private GrammaticalGender gender;

        public NounForms()
        {
        }

        public NounForms(string nominative, GrammaticalGender gender)
        {
            this.nominative = nominative;
            this.gender = gender;
        }

        public string Nominative => nominative;
        public GrammaticalGender Gender => gender;

        /// <summary>Форма в падеже; пустая, если её ещё не заполнили.</summary>
        public string Get(GrammaticalCase grammaticalCase)
        {
            switch (grammaticalCase)
            {
                case GrammaticalCase.Nominative: return nominative;
                case GrammaticalCase.Genitive: return genitive;
                case GrammaticalCase.Dative: return dative;
                case GrammaticalCase.Accusative: return accusative;
                case GrammaticalCase.Instrumental: return instrumental;
                case GrammaticalCase.Prepositional: return prepositional;
                default: throw new ArgumentOutOfRangeException(nameof(grammaticalCase), grammaticalCase, null);
            }
        }

        public override string ToString() => nominative;
    }
}
