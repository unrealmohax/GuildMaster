using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GuildMaster.Core;
using NUnit.Framework;
using UnityEngine;

namespace GuildMaster.Tests
{
    /// <summary>Правила зависимостей между сборками, проверенные машиной.</summary>
    public sealed class ArchitectureTests
    {
        private static readonly string ScriptsPath = Path.Combine(Application.dataPath, "_Project", "Scripts");
        private static readonly Assembly CoreAssembly = typeof(Simulation).Assembly;

        [Serializable]
        private sealed class AssemblyDefinition
        {
            public string name;
            public string[] references;
        }

        [TestCase("Data")]
        [TestCase("Core", "GuildMaster.Data")]
        [TestCase("UI", "GuildMaster.Core", "GuildMaster.Data", "UnityEngine.UI", "Unity.TextMeshPro", "Unity.InputSystem")]
        [TestCase("Bootstrap", "GuildMaster.Core", "GuildMaster.Data", "GuildMaster.UI", "Unity.InputSystem")]
        [TestCase("Debugging", "GuildMaster.Core", "GuildMaster.Data", "GuildMaster.UI")]
        public void AssemblyReferences_MatchSpec(string folder, params string[] expected)
        {
            string path = Path.Combine(ScriptsPath, folder, $"GuildMaster.{folder}.asmdef");
            var definition = JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(path));

            Assert.AreEqual($"GuildMaster.{folder}", definition.name);
            CollectionAssert.AreEquivalent(expected, definition.references ?? Array.Empty<string>());
        }

        [Test]
        public void Core_DoesNotReferenceUiBootstrapOrDebugging()
        {
            string[] referenced = CoreAssembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            CollectionAssert.DoesNotContain(referenced, "GuildMaster.UI");
            CollectionAssert.DoesNotContain(referenced, "GuildMaster.Bootstrap");
            CollectionAssert.DoesNotContain(referenced, "GuildMaster.Debugging");
        }

        [Test]
        public void Core_HasNoUnityObjects()
        {
            Type[] unityTypes = CoreAssembly.GetTypes().Where(t => typeof(UnityEngine.Object).IsAssignableFrom(t)).ToArray();
            CollectionAssert.IsEmpty(unityTypes, "Core must be plain C#: no MonoBehaviour or ScriptableObject");
        }

        [Test]
        public void Core_DoesNotUseUnityRandomTimeOrScenes()
        {
            var forbidden = new[]
            {
                new Regex(@"\bUnityEngine\.Random\b"),
                new Regex(@"\bRandom\.(Range|value|InitState|insideUnitCircle|insideUnitSphere|onUnitSphere|rotation|state)\b"),
                new Regex(@"\bnew\s+(System\.)?Random\s*\("),
                new Regex(@"\bUnityEngine\.Time\b"),
                new Regex(@"\bTime\.(time|deltaTime|unscaledDeltaTime|unscaledTime|realtimeSinceStartup|frameCount|fixedDeltaTime|timeScale)\b"),
                new Regex(@"\bDateTime\.(Now|UtcNow|Today)\b"),
                new Regex(@"\bStopwatch\b"),
                new Regex(@"\bSceneManager\b|\bUnityEngine\.SceneManagement\b"),
                new Regex(@"\bMonoBehaviour\b"),
            };

            var lineComment = new Regex(@"//.*$", RegexOptions.Multiline);
            string[] files = Directory.GetFiles(Path.Combine(ScriptsPath, "Core"), "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files);

            var violations = (
                from file in files
                let code = lineComment.Replace(File.ReadAllText(file), string.Empty)
                from pattern in forbidden
                where pattern.IsMatch(code)
                select $"{Path.GetFileName(file)}: {pattern}").ToArray();

            CollectionAssert.IsEmpty(violations);
        }

        [Test]
        public void WorldState_CanOnlyBeChangedFromCore()
        {
            Type[] stateTypes =
            {
                typeof(WorldState), typeof(IdGenerator), typeof(AutopauseState),
                typeof(AdventurerRoster), typeof(Adventurer), typeof(AdventurerState), typeof(Condition), typeof(TraitInstance), typeof(Candidate),
                typeof(RelationBook), typeof(Relation),
                typeof(FeedState), typeof(FeedEntry),
                typeof(Treasury), typeof(Bankruptcy), typeof(LedgerEntry),
                typeof(MonthReportHistory), typeof(MonthReport), typeof(ReportSection), typeof(ReportLine), typeof(ReportItem),
                typeof(GuildState), typeof(OrderBoard), typeof(Order),
                typeof(QuestBook), typeof(QuestRun), typeof(QuestDiscovery), typeof(Straggler),
                typeof(PartyBook), typeof(Party),
                typeof(BuildingBook), typeof(Building),
                typeof(StaffRoster), typeof(StaffMember), typeof(StaffCandidate),
                typeof(DecreeBook), typeof(ActiveDecree), typeof(DecreeSpan),
            };
            foreach (Type type in stateTypes)
            {
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    MethodInfo setter = property.GetSetMethod(nonPublic: false);
                    Assert.IsNull(setter, $"{type.Name}.{property.Name} has a public setter");
                }
                Assert.IsEmpty(type.GetFields(BindingFlags.Public | BindingFlags.Instance), $"{type.Name} has public fields");
                Assert.IsEmpty(
                    type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName && !IsQuery(m)),
                    $"{type.Name} has public methods that may change it");
            }
        }

        /// <summary>Публичный метод части мира допустим, только если это вопрос: Is…/Has…/Get…/TryGet… с результатом.</summary>
        private static bool IsQuery(MethodInfo method) =>
            method.ReturnType != typeof(void) && Regex.IsMatch(method.Name, "^(Is|Has|Get|TryGet)[A-Z]");
    }
}
