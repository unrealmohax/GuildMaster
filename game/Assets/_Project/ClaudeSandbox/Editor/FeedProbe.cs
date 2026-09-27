using System.Collections.Generic;
using System.IO;
using System.Text;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using UnityEditor;
using UnityEngine;

namespace GuildMaster.ClaudeSandbox
{
    /// <summary>
    /// Песочница. Лента событий из меню GuildMaster → Sandbox → Feed — для вычитки глазами:
    /// <c>Dump Templates</c> — каждый вариант каждого шаблона для мужчины и женщины (Ян / Мара), названия — метками падежа
    /// («место-р») во всех четырёх родах; <c>Year Feed Seed 1</c> — лента гильдии за год с ботом «Простой».
    /// Файлы — в <c>Logs/</c> проекта (папка в .gitignore), путь — в консоль.
    /// </summary>
    public static class FeedProbe
    {
        private const string Menu = "GuildMaster/Sandbox/Feed/";

        private static readonly string[] NounLabels = { "группа", "место", "враг", "заказчик", "груз", "распоряжение", "постройка" };

        [MenuItem(Menu + "Dump Templates")]
        private static void DumpTemplates()
        {
            GameConfig config = EditorAssets.FindGameConfig();
            if (config == null) return;
            DataRegistry data = DataRegistry.FromConfig(config);
            NounForms yan = data.NameForms("Ян");
            NounForms mara = data.NameForms("Мара");

            var text = new StringBuilder();
            foreach (FeedTemplate template in config.FeedTemplates.Templates)
            {
                text.Append("## ").Append(template.Key).Append(' ').Append(template.Importance)
                    .Append(template.Conditions.Count > 0 ? " (условия: " + template.Conditions.Count + ")" : string.Empty).Append('\n');
                foreach (string variant in template.Variants)
                {
                    text.Append("   ").Append(variant).Append('\n');
                    foreach (bool female in new[] { false, true })
                    foreach (GrammaticalGender gender in variant.Contains("@") && !variant.Contains("@имя") ? AllGenders : OneGender)
                    {
                        var errors = new List<string>();
                        string rendered = TextRenderer.Render(variant, Source(yan, mara, female, gender), errors);
                        text.Append(female ? " ж " : " м ").Append(gender == GrammaticalGender.Masculine ? "  " : gender.ToString().Substring(0, 2))
                            .Append(' ').Append(rendered);
                        if (errors.Count > 0) text.Append("   !! ").Append(string.Join("; ", errors));
                        text.Append('\n');
                    }
                }
            }
            Write("feed_templates.txt", text.ToString());
        }

        [MenuItem(Menu + "Year Feed Seed 1")]
        private static void YearFeed()
        {
            GameConfig config = EditorAssets.FindGameConfig();
            if (config == null) return;
            HeadlessRun.Result result = HeadlessRun.Run(DataRegistry.FromConfig(config), 1u, 360, PlayerBots.Simple());
            var text = new StringBuilder();
            foreach (FeedEntry entry in result.Simulation.World.Feed.Guild)
            {
                text.Append(result.Simulation.Calendar.At(entry.TimeHours)).Append(' ').Append(FeedSystem.Mark(entry.Importance)).Append(' ')
                    .Append(entry.Text).Append("   (").Append(entry.TemplateKey).Append(")\n");
            }
            Write("feed_year_1.txt", text.ToString());
        }

        private static readonly GrammaticalGender[] OneGender = { GrammaticalGender.Masculine };

        private static readonly GrammaticalGender[] AllGenders =
            { GrammaticalGender.Masculine, GrammaticalGender.Feminine, GrammaticalGender.Neuter, GrammaticalGender.Plural };

        private static DictionarySourceProbe Source(NounForms yan, NounForms mara, bool female, GrammaticalGender nounGender)
        {
            TextValue man = TextValue.Person("Ян", yan, Gender.Male);
            TextValue woman = TextValue.Person("Мара", mara, Gender.Female);
            var source = new DictionarySourceProbe();
            source.Values["имя"] = female ? woman : man;
            source.Values["напарник"] = female ? man : woman;
            source.Values["лекарь"] = female ? man : woman;
            source.Values["щит"] = female ? woman : man;
            source.Values["решающий"] = female ? woman : man;
            source.Values["число"] = TextValue.Number(2);
            source.Values["всего"] = TextValue.Number(4);
            source.Values["сумма"] = TextValue.Number(50);
            source.Values["доход"] = TextValue.Number(300);
            source.Values["расход"] = TextValue.Number(200);
            source.Values["архетип"] = TextValue.Word(female ? "Разведчица" : "Разведчик");
            source.Values["причина"] = TextValue.Word("пустой кошелёк, устал");
            foreach (string label in NounLabels)
            {
                GrammaticalGender gender = label == "группа" ? GrammaticalGender.Feminine : nounGender;
                source.Values[label] = TextValue.Noun(new NounForms(label, label + "-р", label + "-д", label + "-в", label + "-т", label + "-п", gender));
            }
            return source;
        }

        private static void Write(string name, string text)
        {
            string folder = LogFiles.DefaultFolder;
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, text, new UTF8Encoding(false));
            Debug.Log("[FeedProbe] " + path);
        }

        private sealed class DictionarySourceProbe : ITextSource
        {
            public readonly Dictionary<string, TextValue> Values = new Dictionary<string, TextValue>();

            public bool TryGet(string label, out TextValue value) => Values.TryGetValue(label, out value);
        }
    }
}
