using System.Collections.Generic;
using System.Text;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Движок подстановки текстов: ленты, описания, обращения.
    /// <list type="bullet">
    /// <item><c>{метка}</c> — значение в именительном падеже, <c>{метка:р}</c> — в падеже (р, д, в, т, п).</item>
    /// <item><c>[м|ж]</c> — форма по роду человека: ближайшего предыдущего человека-метки в том же предложении,
    /// а если перед скобкой его нет — ближайшего следующего.</item>
    /// <item><c>[м|ж]@имя</c> — явная привязка к метке. Названия — только с привязкой и тремя-четырьмя формами
    /// <c>[м|ж|ср|мн]@постройка</c>; нужной формы нет — мужская. Метки нет в строке — род берётся из источника
    /// (текст, который потом встаёт в другую строку: «жадн[ый|ая]@имя — мало платят»).</item>
    /// </list>
    /// Метка без источника остаётся в строке как есть, скобка без владельца — мужская форма; обе — ошибка в списке.
    /// Значение в начале предложения пишется с заглавной буквы. Значения со ссылкой (<see cref="TextValue.Link"/>) дают места
    /// в готовом тексте (<see cref="TextSpan"/>) — по ним интерфейс делает имена кликабельными.
    /// </summary>
    public static class TextRenderer
    {
        private static readonly char[] SentenceEnds = { '.', '!', '?', '…' };

        private enum TokenKind
        {
            Literal,
            Placeholder,
            Bracket,
        }

        private sealed class Token
        {
            public TokenKind Kind;
            public string Text;
            public int Sentence;

            // Метка
            public string Label;
            public GrammaticalCase Case;
            public TextValue Value;

            // Скобка
            public string[] Forms;
            public string Owner;
        }

        /// <summary>Подставить значения. Ошибки дописываются в <paramref name="errors"/> (может быть <c>null</c>).</summary>
        public static string Render(string template, ITextSource source, List<string> errors) => Render(template, source, errors, null);

        /// <summary>
        /// Подставить значения и дописать в <paramref name="spans"/> (может быть <c>null</c>) места значений со ссылкой.
        /// </summary>
        public static string Render(string template, ITextSource source, List<string> errors, List<TextSpan> spans)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;

            List<Token> tokens = Parse(template, errors);
            foreach (Token token in tokens)
            {
                if (token.Kind != TokenKind.Placeholder) continue;
                if (!source.TryGet(token.Label, out token.Value) || token.Value == null)
                {
                    token.Value = null;
                    errors?.Add($"метка {token.Text} без источника: «{template}»");
                }
            }

            var result = new StringBuilder(template.Length + 16);
            bool sentenceStart = true;
            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];
                switch (token.Kind)
                {
                    case TokenKind.Literal:
                        result.Append(token.Text);
                        sentenceStart = UpdateSentenceStart(sentenceStart, token.Text);
                        break;
                    case TokenKind.Placeholder:
                        if (token.Value == null)
                        {
                            result.Append(token.Text);
                        }
                        else
                        {
                            string text = token.Value.Get(token.Case);
                            if (sentenceStart && text.Length > 0) text = char.ToUpperInvariant(text[0]) + text.Substring(1);
                            if (spans != null && text.Length > 0 && !token.Value.Link.IsNone)
                                spans.Add(new TextSpan(result.Length, text.Length, token.Value.Link));
                            result.Append(text);
                        }
                        sentenceStart = false;
                        break;
                    case TokenKind.Bracket:
                        string form = BracketForm(tokens, i, template, source, errors);
                        result.Append(form);
                        sentenceStart = UpdateSentenceStart(sentenceStart, form);
                        break;
                }
            }
            return result.ToString();
        }

        private static bool UpdateSentenceStart(bool sentenceStart, string text)
        {
            foreach (char c in text)
            {
                if (System.Array.IndexOf(SentenceEnds, c) >= 0) sentenceStart = true;
                else if (!char.IsWhiteSpace(c)) sentenceStart = false;
            }
            return sentenceStart;
        }

        private static string BracketForm(List<Token> tokens, int index, string template, ITextSource source, List<string> errors)
        {
            Token bracket = tokens[index];
            string[] forms = bracket.Forms;

            TextValue owner;
            if (bracket.Owner != null)
            {
                owner = FindLabel(tokens, bracket.Owner);
                if (owner == null && !HasLabel(tokens, bracket.Owner) && source.TryGet(bracket.Owner, out TextValue outside)) owner = outside;
                if (owner == null)
                {
                    if (!HasLabel(tokens, bracket.Owner)) errors?.Add($"{bracket.Text}: метки {{{bracket.Owner}}} в строке нет: «{template}»");
                    return forms[0];
                }
            }
            else
            {
                Token person = FindPerson(tokens, index);
                if (person == null)
                {
                    errors?.Add($"{bracket.Text}: нет человека в том же предложении: «{template}»");
                    return forms[0];
                }
                if (person.Value == null) return forms[0];
                owner = person.Value;
            }

            int form = owner.IsPerson || forms.Length == 2
                ? (owner.Gender == GrammaticalGender.Feminine ? 1 : 0)
                : GenderIndex(owner.Gender);
            return form < forms.Length ? forms[form] : forms[0];
        }

        private static int GenderIndex(GrammaticalGender gender)
        {
            switch (gender)
            {
                case GrammaticalGender.Feminine: return 1;
                case GrammaticalGender.Neuter: return 2;
                case GrammaticalGender.Plural: return 3;
                default: return 0;
            }
        }

        /// <summary>Ближайший предыдущий человек в предложении скобки; нет — ближайший следующий.</summary>
        private static Token FindPerson(List<Token> tokens, int index)
        {
            int sentence = tokens[index].Sentence;
            for (int i = index - 1; i >= 0 && tokens[i].Sentence == sentence; i--)
            {
                if (IsPersonToken(tokens[i])) return tokens[i];
            }
            for (int i = index + 1; i < tokens.Count && tokens[i].Sentence == sentence; i++)
            {
                if (IsPersonToken(tokens[i])) return tokens[i];
            }
            return null;
        }

        private static bool IsPersonToken(Token token) =>
            token.Kind == TokenKind.Placeholder && (token.Value != null ? token.Value.IsPerson : TextPlaceholders.IsPerson(token.Label));

        private static TextValue FindLabel(List<Token> tokens, string label)
        {
            foreach (Token token in tokens)
            {
                if (token.Kind == TokenKind.Placeholder && token.Label == label && token.Value != null) return token.Value;
            }
            return null;
        }

        private static bool HasLabel(List<Token> tokens, string label)
        {
            foreach (Token token in tokens)
            {
                if (token.Kind == TokenKind.Placeholder && token.Label == label) return true;
            }
            return false;
        }

        private static List<Token> Parse(string template, List<string> errors)
        {
            var tokens = new List<Token>();
            var literal = new StringBuilder();
            int sentence = 0;

            void FlushLiteral()
            {
                if (literal.Length == 0) return;
                tokens.Add(new Token { Kind = TokenKind.Literal, Text = literal.ToString(), Sentence = sentence });
                literal.Clear();
            }

            int i = 0;
            while (i < template.Length)
            {
                char c = template[i];
                int close = c == '{' ? template.IndexOf('}', i + 1) : c == '[' ? template.IndexOf(']', i + 1) : -1;
                if (close < 0)
                {
                    if (c == '{' || c == '[') errors?.Add($"непарная скобка {c}: «{template}»");
                    literal.Append(c);
                    if (System.Array.IndexOf(SentenceEnds, c) >= 0)
                    {
                        FlushLiteral();
                        sentence++;
                    }
                    i++;
                    continue;
                }

                FlushLiteral();
                string body = template.Substring(i + 1, close - i - 1);
                if (c == '{')
                {
                    tokens.Add(Placeholder(template.Substring(i, close - i + 1), body, sentence, template, errors));
                    i = close + 1;
                }
                else
                {
                    int end = close + 1;
                    string owner = null;
                    if (end < template.Length && template[end] == '@')
                    {
                        int start = end + 1;
                        end = start;
                        while (end < template.Length && char.IsLetterOrDigit(template[end])) end++;
                        owner = end > start ? template.Substring(start, end - start) : null;
                    }
                    tokens.Add(new Token
                    {
                        Kind = TokenKind.Bracket,
                        Text = template.Substring(i, end - i),
                        Sentence = sentence,
                        Forms = body.Split('|'),
                        Owner = owner,
                    });
                    i = end;
                }
            }
            FlushLiteral();
            return tokens;
        }

        private static Token Placeholder(string text, string body, int sentence, string template, List<string> errors)
        {
            var token = new Token { Kind = TokenKind.Placeholder, Text = text, Sentence = sentence, Case = GrammaticalCase.Nominative };
            int colon = body.IndexOf(':');
            token.Label = colon < 0 ? body : body.Substring(0, colon);
            if (colon >= 0)
            {
                string suffix = body.Substring(colon + 1);
                if (TextPlaceholders.CaseSuffixes.TryGetValue(suffix, out GrammaticalCase grammaticalCase)) token.Case = grammaticalCase;
                else errors?.Add($"неизвестный падеж в {text}: «{template}»");
            }
            return token;
        }
    }
}
