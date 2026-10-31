using System.Collections.Generic;
using System.IO;
using GuildMaster.ClaudeSandbox.Kit3D;
using UnityEditor;
using UnityEngine;

namespace GuildMaster.ClaudeSandbox
{
    /// <summary>
    /// Меню кита домов: собрать каталог модулей из папки моделей, вывести габариты модулей (по ним видно, как оси Blender
    /// легли в Unity), пересобрать все дома на сцене.
    /// </summary>
    public static class Kit3DMenu
    {
        private const string Root = "Assets/_Project/ClaudeSandbox/Kit3D";
        private const string Models = Root + "/Models";
        private const string KitPath = Root + "/HouseKit.asset";

        [MenuItem("GuildMaster/Sandbox/Kit3D/Build Kit Catalog")]
        public static void BuildCatalog()
        {
            var kit = AssetDatabase.LoadAssetAtPath<HouseKit>(KitPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<HouseKit>();
                AssetDatabase.CreateAsset(kit, KitPath);
            }

            var entries = new List<HouseKit.Entry>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Models }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                entries.Add(new HouseKit.Entry { name = Path.GetFileNameWithoutExtension(path), model = model });
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            kit.SetModules(entries);
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            Debug.Log($"Kit3D: {entries.Count} modules in {KitPath}");
        }

        [MenuItem("GuildMaster/Sandbox/Kit3D/Log Module Bounds")]
        public static void LogBounds()
        {
            var kit = AssetDatabase.LoadAssetAtPath<HouseKit>(KitPath);
            if (kit == null)
            {
                Debug.LogWarning("Kit3D: no catalog, run Build Kit Catalog");
                return;
            }

            var lines = new List<string>();
            foreach (var name in new[] { "corner_post", "wall_door", "prop", "roof_d4", "gable_d4" })
            {
                var model = kit.Get(name);
                var filter = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
                if (filter == null) continue;
                var t = filter.transform;
                var b = filter.sharedMesh.bounds;
                lines.Add($"Kit3D bounds {name}: min {b.min:F2} max {b.max:F2}; child pos {t.localPosition:F2} rot {t.localEulerAngles:F1} scale {t.localScale:F2}");
            }

            foreach (var line in lines) Debug.Log(line);
            File.WriteAllLines("Temp/kit3d_bounds.txt", lines);
        }

        /// <summary>
        /// Пробная сцена кита: земля, солнце, камера вида поселения (наклон 55°, угол обзора 30°), ряд домов во всех стадиях
        /// и квартал случайных домов по сидам. Пересоздаётся целиком; открытая до этого сцена закрывается без сохранения.
        /// </summary>
        [MenuItem("GuildMaster/Sandbox/Kit3D/Build Test Scene")]
        public static void BuildTestScene()
        {
            // Новая сцена закрывает открытые без вопроса — несохранённые правки в них пропали бы.
            for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                if (!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) continue;
                File.WriteAllText("Temp/kit3d_scene.txt", "refused: an open scene has unsaved changes");
                return;
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);

            // Каталог грузится после новой сцены: смена сцены выгружает неиспользуемые ассеты, загруженная раньше ссылка пустеет.
            var kit = AssetDatabase.LoadAssetAtPath<HouseKit>(KitPath);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(-14, 0, 12);
            ground.transform.localScale = new Vector3(8, 1, 8);
            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.27f, 0.24f, 0.18f) };
            AssetDatabase.CreateAsset(groundMat, Root + "/Ground.mat");
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50, 150, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.48f);

            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.fieldOfView = 30;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.5f, 0.52f, 0.55f);
            var target = new Vector3(-12, 0, 10);
            var rotation = Quaternion.Euler(55, 225, 0);
            cam.transform.SetPositionAndRotation(target - rotation * Vector3.forward * 70, rotation);

            AddTestHouses(kit);
            Kit3DLighting.Apply(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, Root + "/KitTest.unity");
        }

        /// <summary>
        /// Заново расставляет дома пробной сцены в активной сцене: убирает ряд стадий и квартал и ставит их по текущим размерам.
        /// Остальное — земля, свет, камеры — не трогается. Сцену не сохраняет.
        /// </summary>
        [MenuItem("GuildMaster/Sandbox/Kit3D/Re-lay Test Houses")]
        public static void RelayTestHouses()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name.StartsWith("Stage ") || go.name.StartsWith("House ")) Object.DestroyImmediate(go);
            }

            AddTestHouses(AssetDatabase.LoadAssetAtPath<HouseKit>(KitPath));
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>
        /// Ряд домов во всех стадиях (6 × 6 клеток) и квартал случайных домов по сидам: 5–10 клеток вдоль конька, 5–7 в глубину,
        /// как у настоящих домов. Шаг квартала — по самому большому дому с навесом сбоку и крыльцом спереди.
        /// </summary>
        private static void AddTestHouses(HouseKit kit)
        {
            var stages = new[] { HouseStage.Planned, HouseStage.Construction, HouseStage.Ready, HouseStage.Ready, HouseStage.Abandoned };
            for (var i = 0; i < stages.Length; i++)
            {
                AddHouse(kit, $"Stage {stages[i]}", new Vector3(-i * 9, 0, 0), 6, 6, i == 3 ? 2 : 1, 10 + i, stages[i]);
            }

            var rng = new System.Random(7);
            for (var row = 0; row < 3; row++)
            {
                for (var col = 0; col < 4; col++)
                {
                    var w = rng.Next(5, 11);
                    var d = rng.Next(5, 8);
                    var floors = rng.Next(1, 3);
                    var stage = rng.NextDouble() < 0.15 ? HouseStage.Abandoned : HouseStage.Ready;
                    AddHouse(kit, $"House {row}-{col}", new Vector3(-col * 12.5f, 0, 12 + row * 11), w, d, floors, 100 + row * 10 + col, stage);
                }
            }
        }

        private static void AddHouse(HouseKit kit, string name, Vector3 position, int w, int d, int floors, int seed, HouseStage stage)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var house = go.AddComponent<HouseGenerator>();
            var so = new SerializedObject(house);
            so.FindProperty("kit").objectReferenceValue = kit;
            so.ApplyModifiedPropertiesWithoutUndo();
            house.Set(w, d, floors, seed, stage);
        }

        [MenuItem("GuildMaster/Sandbox/Kit3D/Rebuild Houses")]
        public static void RebuildHouses()
        {
            foreach (var house in Object.FindObjectsByType<HouseGenerator>(FindObjectsInactive.Exclude))
            {
                // Пристройку пересобирает основной дом; уже снесённую его пересборкой — пропустить.
                if (house == null || house.IsWing) continue;
                house.Rebuild();
            }
        }

        /// <summary>Пишет в Temp/kit3d_houses.txt, сколько модулей собрал каждый дом на сцене и видит ли он каталог.</summary>
        [MenuItem("GuildMaster/Sandbox/Kit3D/Log Houses")]
        public static void LogHouses()
        {
            var main = AssetDatabase.LoadMainAssetAtPath(KitPath);
            var lines = new List<string>
            {
                $"catalog {KitPath}: main {(main != null ? main.GetType().FullName : "null")}, " +
                $"as HouseKit {AssetDatabase.LoadAssetAtPath<HouseKit>(KitPath) != null}, type {AssetDatabase.GetMainAssetTypeAtPath(KitPath)}",
            };
            foreach (var house in Object.FindObjectsByType<HouseGenerator>(FindObjectsInactive.Include))
            {
                var kit = new SerializedObject(house).FindProperty("kit").objectReferenceValue as HouseKit;
                var modules = house.GetComponentsInChildren<MeshRenderer>(true).Length;
                lines.Add($"{house.name}: active {house.isActiveAndEnabled}, kit {(kit != null ? kit.Modules.Count.ToString() : "null")}, " +
                          $"wall_door {(kit != null && kit.Get("wall_door") != null)}, children {house.transform.childCount}, renderers {modules}");
            }

            File.WriteAllLines("Temp/kit3d_houses.txt", lines);
        }
    }
}
