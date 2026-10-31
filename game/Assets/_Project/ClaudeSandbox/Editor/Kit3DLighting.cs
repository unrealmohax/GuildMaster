using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GuildMaster.ClaudeSandbox
{
    /// <summary>
    /// Свет пробной сцены кита под концепт поселения: пасмурный холодный день. Небо, рассеянный свет трёх цветов (небо, горизонт, земля),
    /// мягкое солнце, дымка, постобработка (тонмаппинг ACES, приглушённый цвет, холодный баланс белого, лёгкая виньетка).
    /// Отдельно — настройки проекта для реализма: затенение в углах (SSAO) в рендерере, мягкие тени и два каскада в URP-ассете.
    /// </summary>
    public static class Kit3DLighting
    {
        public const string SettlementCamera = "Settlement Camera";
        public const string DetailCamera = "Detail Camera";

        private const string Root = "Assets/_Project/ClaudeSandbox/Kit3D";
        private const string LightingPath = Root + "/Lighting";
        private const string ScenePath = Root + "/KitTest.unity";
        private const string RendererPath = "Assets/Settings/UniversalRenderer.asset";
        private const string PipelinePath = "Assets/Settings/UniversalRP.asset";

        private static readonly Color FogColor = new Color(0.58f, 0.61f, 0.64f);

        /// <summary>Свет в загруженной пробной сцене; сцена может быть открыта рядом с другой, та не трогается.</summary>
        [MenuItem("GuildMaster/Sandbox/Kit3D/Apply Lighting")]
        public static void ApplyToTestScene()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
            {
                File.WriteAllText("Temp/kit3d_lighting.txt", "KitTest is not loaded");
                return;
            }

            // Настройки освещения принадлежат активной сцене, новые объекты создаются в ней же.
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            SceneManager.SetActiveScene(previous);
            File.WriteAllText("Temp/kit3d_lighting.txt", "applied");
        }

        /// <summary>Ставит свет в активную сцену: солнце, небо, рассеянный свет, дымку, объём постобработки и обе камеры.</summary>
        public static void Apply(Scene scene)
        {
            // GetComponent в редакторе может вернуть «пустышку», которая равна null только для Unity, поэтому без ??.
            var sunGo = Find(scene, "Sun") ?? new GameObject("Sun");
            var sun = sunGo.GetComponent<Light>();
            if (sun == null) sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;
            sun.transform.rotation = Quaternion.Euler(48, 140, 0);

            // Земля пробной сцены — однотонная утоптанная грязь; темнее — и после тонмаппинга она уходит в чёрное.
            var ground = Find(scene, "Ground");
            if (ground != null)
            {
                var mat = ground.GetComponent<MeshRenderer>().sharedMaterial;
                mat.color = new Color(0.36f, 0.32f, 0.26f);
                mat.SetFloat("_Smoothness", 0.1f);
                EditorUtility.SetDirty(mat);
            }

            RenderSettings.skybox = SkyMaterial();
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.60f, 0.65f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.50f, 0.49f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.22f, 0.19f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.005f;

            var volumeGo = Find(scene, "Global Volume") ?? new GameObject("Global Volume");
            var volume = volumeGo.GetComponent<Volume>();
            if (volume == null) volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = VolumeProfileAsset();

            var settlement = Find(scene, SettlementCamera) ?? Find(scene, "Main Camera");
            if (settlement != null)
            {
                settlement.name = SettlementCamera;
                SetupCamera(settlement.GetComponent<Camera>());
            }

            var detail = Find(scene, DetailCamera);
            if (detail == null)
            {
                detail = new GameObject(DetailCamera);
                detail.AddComponent<Camera>();
            }

            var detailCamera = detail.GetComponent<Camera>();
            detailCamera.fieldOfView = 40;
            detailCamera.enabled = false;
            detail.transform.position = new Vector3(-17, 10, 6);
            detail.transform.LookAt(new Vector3(-23, 6, 0));
            SetupCamera(detailCamera);
        }

        private static void SetupCamera(Camera camera)
        {
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.farClipPlane = 600;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }

        private static GameObject Find(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == name) return go;
            }

            return null;
        }

        private static Material SkyMaterial()
        {
            EnsureFolder();
            var path = LightingPath + "/OvercastSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, path);
            }

            sky.SetFloat("_SunDisk", 0f);
            sky.SetFloat("_AtmosphereThickness", 0.7f);
            sky.SetColor("_SkyTint", new Color(0.45f, 0.47f, 0.5f));
            sky.SetColor("_GroundColor", FogColor);
            sky.SetFloat("_Exposure", 1.1f);
            EditorUtility.SetDirty(sky);
            return sky;
        }

        /// <summary>Профиль постобработки пересоздаётся целиком, чтобы в нём были только перечисленные здесь эффекты.</summary>
        private static VolumeProfile VolumeProfileAsset()
        {
            EnsureFolder();
            var path = LightingPath + "/OvercastVolume.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.35f);
            color.contrast.Override(8f);
            color.saturation.Override(-18f);
            var white = profile.Add<WhiteBalance>(true);
            white.temperature.Override(-8f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.5f);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.15f);

            foreach (var component in profile.components)
            {
                component.name = component.GetType().Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(LightingPath)) AssetDatabase.CreateFolder(Root, "Lighting");
        }

        /// <summary>
        /// Настройки проекта: SSAO в рендерере (радиус под дома, а не под мелкие предметы), мягкие тени и два каскада теней в URP-ассете.
        /// </summary>
        [MenuItem("GuildMaster/Sandbox/Kit3D/Apply Render Settings")]
        public static void ApplyRenderSettings()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            ScreenSpaceAmbientOcclusion ssao = null;
            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature is ScreenSpaceAmbientOcclusion found) ssao = found;
            }

            if (ssao == null)
            {
                ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "ScreenSpaceAmbientOcclusion";
                AssetDatabase.AddObjectToAsset(ssao, renderer);
                renderer.rendererFeatures.Add(ssao);
            }

            var so = new SerializedObject(ssao);
            so.FindProperty("m_Settings.Intensity").floatValue = 1.6f;
            so.FindProperty("m_Settings.Radius").floatValue = 0.4f;
            so.FindProperty("m_Settings.DirectLightingStrength").floatValue = 0.3f;
            so.FindProperty("m_Settings.Falloff").floatValue = 200f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Рендерер хранит рядом со списком функций их локальные id; без них функция при загрузке считается потерянной.
            var rso = new SerializedObject(renderer);
            var features = rso.FindProperty("m_RendererFeatures");
            var map = rso.FindProperty("m_RendererFeatureMap");
            map.arraySize = features.arraySize;
            for (var i = 0; i < features.arraySize; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(features.GetArrayElementAtIndex(i).objectReferenceValue, out _, out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }

            rso.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var pso = new SerializedObject(pipeline);
            pso.FindProperty("m_SoftShadowsSupported").boolValue = true;
            pso.FindProperty("m_ShadowCascadeCount").intValue = 2;
            pso.FindProperty("m_Cascade2Split").floatValue = 0.35f;
            pso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();

            File.WriteAllText("Temp/kit3d_render.txt",
                $"features {renderer.rendererFeatures.Count}, soft {pipeline.supportsSoftShadows}, cascades {pipeline.shadowCascadeCount}");
        }
    }
}
