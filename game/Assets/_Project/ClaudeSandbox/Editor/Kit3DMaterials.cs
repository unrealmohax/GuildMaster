using System.Collections.Generic;
using System.IO;
using GuildMaster.ClaudeSandbox.Kit3D;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuildMaster.ClaudeSandbox
{
    /// <summary>
    /// Материалы кита домов из фото-текстур Poly Haven: перепаковка карт под URP Lit, материалы GM_* и подмена ими материалов FBX
    /// по имени слота. У набора текстур три карты: цвет (diff), нормали в OpenGL-виде (nor_gl) и arm — затенение (R), шероховатость (G),
    /// металл (B). URP Lit читает металл из R, затенение из G, гладкость из A, поэтому arm перепаковывается в одну карту _mao.
    /// </summary>
    public static class Kit3DMaterials
    {
        private const string Root = "Assets/_Project/ClaudeSandbox/Kit3D";
        private const string TexturesPath = Root + "/Textures";
        private const string MaterialsPath = Root + "/Materials";
        private const string ModelsPath = Root + "/Models";

        private readonly struct Textured
        {
            public Textured(string material, string set, float metres, float tint = 1f, float gloss = 1f, float metresAlong = 0f,
                Color? hue = null, float relief = 1f, float occlusion = -1f, float offsetAlong = 0f)
            {
                Material = material;
                Set = set;
                Metres = metres;
                Tint = tint;
                Gloss = gloss;
                MetresAlong = metresAlong > 0 ? metresAlong : metres;
                Hue = hue ?? Color.white;
                Relief = relief;
                Occlusion = occlusion >= 0 ? occlusion : relief;
                OffsetAlong = offsetAlong;
            }

            public string Material { get; }
            public string Set { get; }

            /// <summary>Сколько метров поверхности покрывает одна копия текстуры.</summary>
            public float Metres { get; }

            /// <summary>То же по V — вдоль волокна у дерева. Растянутое волокно не видно, а повтор сучков на балке — заметен.</summary>
            public float MetresAlong { get; }

            /// <summary>Множитель яркости цвета текстуры: брус темнее заполнения, чтобы каркас читался издалека.</summary>
            public float Tint { get; }

            /// <summary>Оттенок поверх цвета текстуры: из одной фотографии камня — и бурый цоколь, и серо-синий сланец.</summary>
            public Color Hue { get; }

            /// <summary>Множитель гладкости из карты: у старого сухого дерева блеск карты слишком сильный — на фасках он даёт полосы.</summary>
            public float Gloss { get; }

            /// <summary>Сила рельефа — карты нормалей. Ниже 1 — поверхность глаже, без ряби вблизи.</summary>
            public float Relief { get; }

            /// <summary>Сила затенения в порах; если не задана — как у рельефа.</summary>
            public float Occlusion { get; }

            /// <summary>Сдвиг текстуры по V в долях копии: ряд кладки ложится ровно на низ модуля.</summary>
            public float OffsetAlong { get; }
        }

        private readonly struct Plain
        {
            public Plain(string material, Color color, float smoothness)
            {
                Material = material;
                Color = color;
                Smoothness = smoothness;
            }

            public string Material { get; }
            public Color Color { get; }
            public float Smoothness { get; }
        }

        private static readonly Textured[] TexturedMaterials =
        {
            new Textured("GM_Timber", "rough_wood", 0.5f, 0.6f, 0.4f, 1.6f),
            // Вертикальные доски с гвоздями — дверное полотно.
            new Textured("GM_Door", "medieval_wood", 2f, 0.8f, 0.5f),
            new Textured("GM_Plank", "weathered_brown_planks", 1.8f, 1f, 0.6f),
            // Пол этажей из тех же досок, своим материалом — настраивается отдельно от досок на стенах.
            new Textured("GM_Floor", "weathered_brown_planks", 1.8f, 1f, 0.6f),
            // Две побелки — ровная и шершавая; дому выпадает одна из них, со временем она осыпается до камня.
            new Textured("GM_Daub", "white_stucco", 2f),
            new Textured("GM_Daub_Rough", "white_stucco_02", 1.8f),
            // Кладка трубы и каменного этажа (подобрана автором в материале): камни мельче настоящего, ряды чуть выше, чем шире,
            // рельеф 0,75 и слабое затенение в порах.
            new Textured("GM_Stone", "medieval_blocks_03", 1f / 0.85f, 1f, 1f, 1f / 0.82f, null, 0.75f, 0.35f, 0.08f),
            // Гладкий мелкозернистый камень без швов кладки — для отдельных камней цоколя, крыльца, обкладок и углов.
            new Textured("GM_Rock", "rock_01", 2f),
            // Плитки сланца — отдельные детали, у каждой свой кусок текстуры; поэтому камень без рисунка плиток, с красно-бурым оттенком
            // и ослабленным рельефом.
            new Textured("GM_Roof_Slate", "rock_01", 1f, 1f, 0.5f, 0f, new Color(0.85f, 0.33f, 0.28f), 0.35f, 1f),
            // Солома в настоящем размере (0,54 м) с камеры поселения сливается в серый шум — растянута, чтобы читались пучки.
            new Textured("GM_Roof_Thatch", "thatch_roof_angled", 1.5f),
            // Рама и переплёт окна: светлое выцветшее дерево, почти матовое, со слабым рельефом и без затенения в порах.
            new Textured("GM_Sash", "rough_wood", 1f, 1f, 0.05f, 0f, new Color(0.481f, 0.446f, 0.406f), 0.2f, 0f),
        };

        private static readonly Plain[] PlainMaterials =
        {
            new Plain("GM_Lead", new Color(0.12f, 0.12f, 0.13f), 0.4f),
            new Plain("GM_Mortar", new Color(0.30f, 0.28f, 0.25f), 0.05f),
            // Закопчённое нутро дымохода: почти чёрное и матовое, чтобы отверстие читалось глубоким.
            new Plain("GM_Soot", new Color(0.035f, 0.03f, 0.028f), 0f),
            new Plain("GM_Rope", new Color(0.45f, 0.38f, 0.25f), 0.15f),
            new Plain("GM_Wattle", new Color(0.22f, 0.17f, 0.11f), 0.1f),
        };

        /// <summary>Слот материала в FBX → материал кита. У сланцевой и соломенной крыш свои модули и свои слоты.</summary>
        private static readonly (string slot, string material)[] Remap =
        {
            ("GM_Timber", "GM_Timber"), ("GM_Door", "GM_Door"), ("GM_Plank", "GM_Plank"), ("GM_Daub", "GM_Daub"), ("GM_Stone", "GM_Stone"),
            ("GM_Rock", "GM_Rock"), ("GM_Mortar", "GM_Mortar"), ("GM_Lead", "GM_Lead"), ("GM_Sash", "GM_Sash"),
            ("GM_Roof", "GM_Roof_Slate"), ("GM_Thatch", "GM_Roof_Thatch"), ("GM_Window", "GM_Window"), ("GM_Rope", "GM_Rope"),
            ("GM_Wattle", "GM_Wattle"), ("GM_Grime", "GM_Grime"), ("GM_Soot", "GM_Soot"),
        };

        /// <summary>Стекло окна: тёмное зелёно-серое, полупрозрачное, с полным бликом.</summary>
        private static readonly Color GlassColor = new Color(0.188f, 0.236f, 0.235f, 0.867f);

        /// <summary>Высота пыли у двери в метрах: столько занимает её лист в модуле, развёртка по высоте — в метрах.</summary>
        private const float GrimeHeight = 0.45f;

        /// <summary>Шейдер сланца: черепица, которая с возрастом дома зарастает мхом и травой.</summary>
        private const string RoofMossShader = "GuildMaster/Kit3D/Roof Moss";

        /// <summary>Фото-текстура мха с листвой для зарастающей черепицы.</summary>
        private const string MossSet = "forest_leaves_02";

        /// <summary>Шейдер побелки: с износом дома она осыпается до того, что под ней.</summary>
        private const string PlasterWearShader = "GuildMaster/Kit3D/Plaster Wear";

        /// <summary>Варианты побелки: первый стоит в модулях (слот GM_Daub), генератор заменяет его тем, что выпал дому.</summary>
        private static readonly string[] Plasters = { "GM_Daub", "GM_Daub_Rough" };

        /// <summary>Фото-текстура того, что под побелкой: старое дерево обрешётки.</summary>
        private const string UnderSet = "rough_wood";

        private const string KitPath = Root + "/HouseKit.asset";

        /// <summary>Материал пола этажей: генератор строит пол сам, в модулях кита его нет.</summary>
        private const string FloorMaterial = "GM_Floor";

        [MenuItem("GuildMaster/Sandbox/Kit3D/Build Materials")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(MaterialsPath)) AssetDatabase.CreateFolder(Root, "Materials");

            // Шейдеры сланца и побелки меняются до настройки карт: у них те же имена свойств и ключевых слов, что у URP Lit.
            var slate = LoadOrCreate("GM_Roof_Slate");
            slate.shader = Shader.Find(RoofMossShader);
            var plasters = new List<Material>();
            foreach (var name in Plasters)
            {
                var plaster = LoadOrCreate(name);
                plaster.shader = Shader.Find(PlasterWearShader);
                plasters.Add(plaster);
            }

            foreach (var t in TexturedMaterials) BuildTextured(t);
            BuildRoofMoss(slate);
            foreach (var plaster in plasters) BuildPlasterWear(plaster);
            SetKitMaterials(plasters, LoadOrCreate(FloorMaterial));
            foreach (var p in PlainMaterials) BuildPlain(p);
            BuildGlass();
            BuildGrime();
            AssetDatabase.SaveAssets();

            var remapped = RemapModels();
            File.WriteAllText("Temp/kit3d_materials.txt", $"materials {TexturedMaterials.Length + PlainMaterials.Length + 2}, models remapped {remapped}");
        }

        private static void BuildTextured(Textured t)
        {
            var folder = $"{TexturesPath}/{t.Set}";
            var diff = $"{folder}/{t.Set}_diff_1k.jpg";
            var normal = $"{folder}/{t.Set}_nor_gl_1k.jpg";
            var arm = $"{folder}/{t.Set}_arm_1k.jpg";
            var mao = $"{folder}/{t.Set}_mao_1k.png";

            PackMao(arm, mao);
            SetImport(diff, TextureImporterType.Default, true);
            SetImport(normal, TextureImporterType.NormalMap, false);
            SetImport(mao, TextureImporterType.Default, false);

            var mat = LoadOrCreate(t.Material);
            var maoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(mao);
            mat.SetColor("_BaseColor", new Color(t.Tint * t.Hue.r, t.Tint * t.Hue.g, t.Tint * t.Hue.b));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diff));
            mat.SetTextureScale("_BaseMap", new Vector2(1f / t.Metres, 1f / t.MetresAlong));
            mat.SetTextureOffset("_BaseMap", new Vector2(0f, t.OffsetAlong));
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            mat.SetFloat("_BumpScale", t.Relief);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", maoTex);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetFloat("_Smoothness", t.Gloss);
            mat.SetTexture("_OcclusionMap", maoTex);
            mat.SetFloat("_OcclusionStrength", t.Occlusion);
            mat.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(mat);
        }

        /// <summary>
        /// Мох на сланце: карты мха перепакованы так же, как у остальных наборов, мох мелкий (0,25 м на копию), рельеф чуть
        /// слабее, цвет — зеленее фото. Сам материал не зарастает (_Growth 0) — зарастают его копии на домах, по возрасту дома.
        /// </summary>
        private static void BuildRoofMoss(Material mat)
        {
            var folder = $"{TexturesPath}/{MossSet}";
            var diff = $"{folder}/{MossSet}_diff_1k.jpg";
            var normal = $"{folder}/{MossSet}_nor_gl_1k.jpg";
            var arm = $"{folder}/{MossSet}_arm_1k.jpg";
            var mao = $"{folder}/{MossSet}_mao_1k.png";

            PackMao(arm, mao);
            SetImport(diff, TextureImporterType.Default, true);
            SetImport(normal, TextureImporterType.NormalMap, false);
            SetImport(mao, TextureImporterType.Default, false);

            mat.SetTexture("_MossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diff));
            mat.SetTexture("_MossBumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            mat.SetTexture("_MossMaoMap", AssetDatabase.LoadAssetAtPath<Texture2D>(mao));
            // В фото много сухой рыжей листвы — оттенок уводит её в оливковый, мох — в тёмно-зелёный.
            mat.SetColor("_MossColor", new Color(0.55f, 0.68f, 0.4f));
            mat.SetFloat("_MossBumpScale", 0.8f);
            mat.SetFloat("_MossSmoothness", 1f);
            mat.SetFloat("_MossMetres", 0.25f);
            mat.SetFloat("_MossSpacing", 2.5f);
            mat.SetFloat("_MossSeedChance", 0.4f);
            mat.SetFloat("_MossRadius", 2f);
            mat.SetFloat("_MossOpacity", 0.5f);
            mat.SetFloat("_MossMinUp", 0.25f);
            mat.SetFloat("_MossDamp", 0.15f);
            mat.SetFloat("_Growth", 0f);
            EditorUtility.SetDirty(mat);
        }

        /// <summary>
        /// Что под побелкой — старое дерево (подобрано автором в материале): серо-голубоватое, матовое, 1,5 м на копию. Сколы растут из
        /// источников на стене. Сам материал целый (_Wear 0) — осыпаются его копии на домах, по износу дома.
        /// </summary>
        private static void BuildPlasterWear(Material mat)
        {
            var folder = $"{TexturesPath}/{UnderSet}";
            var diff = $"{folder}/{UnderSet}_diff_1k.jpg";
            var normal = $"{folder}/{UnderSet}_nor_gl_1k.jpg";
            var arm = $"{folder}/{UnderSet}_arm_1k.jpg";
            var mao = $"{folder}/{UnderSet}_mao_1k.png";

            PackMao(arm, mao);
            SetImport(diff, TextureImporterType.Default, true);
            SetImport(normal, TextureImporterType.NormalMap, false);
            SetImport(mao, TextureImporterType.Default, false);

            mat.SetTexture("_UnderMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diff));
            mat.SetTexture("_UnderBumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            mat.SetTexture("_UnderMaoMap", AssetDatabase.LoadAssetAtPath<Texture2D>(mao));
            mat.SetColor("_UnderColor", new Color(0.9292f, 0.9456f, 1f));
            mat.SetFloat("_UnderBumpScale", 1f);
            mat.SetFloat("_UnderSmoothness", 0f);
            mat.SetFloat("_UnderMetres", 1.5f);
            mat.SetFloat("_WearSpacing", 1.2f);
            mat.SetFloat("_WearSources", 0.4f);
            mat.SetFloat("_WearRadius", 0.6f);
            mat.SetFloat("_WearSoft", 0.15f);
            mat.SetFloat("_WearLow", 0.1f);
            mat.SetFloat("_WearShade", 0.5f);
            mat.SetFloat("_WearStain", 0.3f);
            mat.SetFloat("_Wear", 0f);
            EditorUtility.SetDirty(mat);
        }

        /// <summary>Варианты побелки и пол — в каталог кита: из него генератор выбирает побелку дому и стелет пол этажей.</summary>
        private static void SetKitMaterials(List<Material> plasters, Material floor)
        {
            var kit = AssetDatabase.LoadAssetAtPath<HouseKit>(KitPath);
            if (kit == null) return;
            kit.SetPlasters(plasters);
            kit.SetFloor(floor);
            EditorUtility.SetDirty(kit);
        }

        private static void BuildPlain(Plain p)
        {
            var mat = LoadOrCreate(p.Material);
            // Однотонный материал — без карт: карты, оставшиеся от ручной настройки, перекрыли бы цвет.
            mat.SetTexture("_BaseMap", null);
            mat.SetTexture("_BumpMap", null);
            mat.DisableKeyword("_NORMALMAP");
            mat.SetColor("_BaseColor", p.Color);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", p.Smoothness);
            EditorUtility.SetDirty(mat);
        }

        /// <summary>Стекло: прозрачное, блик и отражение неба сохраняются в полную силу — по ним стекло и читается.</summary>
        private static void BuildGlass()
        {
            var mat = LoadOrCreate("GM_Window");
            mat.SetColor("_BaseColor", GlassColor);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_SpecularHighlights", 1f);
            mat.SetFloat("_EnvironmentReflections", 1f);
            mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            MakeTransparent(mat, true);
            EditorUtility.SetDirty(mat);
        }

        /// <summary>
        /// Пыль у двери: прозрачный материал с картой, которую рисует этот же код, — серо-бурая (светлее тёмного дерева) внизу, к верху сходит на нет
        /// рваным краем. По ширине карта повторяется, по высоте — растянута на <see cref="GrimeHeight"/> и не повторяется.
        /// </summary>
        private static void BuildGrime()
        {
            var folder = $"{TexturesPath}/grime";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(TexturesPath, "grime");
            var path = $"{folder}/grime_gradient.png";

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (var x = 0; x < size; x++)
            {
                // Шум по кругу — чтобы карта без шва повторялась по ширине.
                var angle = 2 * Mathf.PI * x / size;
                var edge = 0.45f + 0.45f * Mathf.PerlinNoise(3 + 1.5f * Mathf.Cos(angle), 3 + 1.5f * Mathf.Sin(angle));
                for (var y = 0; y < size; y++)
                {
                    var v = (float)y / (size - 1);
                    var fade = Mathf.Pow(Mathf.Clamp01(1 - v / edge), 1.3f);
                    var speck = 0.7f + 0.3f * Mathf.PerlinNoise(10 + 4 * Mathf.Cos(angle), 10 + 4 * Mathf.Sin(angle) + v * 6);
                    pixels[y * size + x] = new Color32(112, 98, 80, (byte)(255 * 0.75f * fade * speck));
                }
            }

            tex.SetPixels32(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            var mat = LoadOrCreate("GM_Grime");
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            mat.SetTextureScale("_BaseMap", new Vector2(1f, 1f / GrimeHeight));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0f);
            // Блик и отражение неба не умножаются на альфу: с ними весь лист светится дымкой там, где пыли нет.
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            MakeTransparent(mat, false);
            EditorUtility.SetDirty(mat);
        }

        /// <summary>
        /// Прозрачность URP Lit: смешивание по альфе, без записи глубины и без тени. С сохранением блика (как у стекла) блик и отражение
        /// не ослабляются альфой.
        /// </summary>
        private static void MakeTransparent(Material mat, bool preserveSpecular)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)(preserveSpecular ? BlendMode.One : BlendMode.SrcAlpha));
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_BlendModePreserveSpecular", preserveSpecular ? 1f : 0f);
            if (preserveSpecular) mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            else mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetShaderPassEnabled("ShadowCaster", false);
            mat.SetShaderPassEnabled("DepthOnly", false);
        }

        /// <summary>arm (R затенение, G шероховатость, B металл) → R металл, G затенение, A гладкость. Файл читается как есть, мимо импорта.</summary>
        private static void PackMao(string armPath, string maoPath)
        {
            var arm = new Texture2D(2, 2, TextureFormat.RGB24, false, true);
            arm.LoadImage(File.ReadAllBytes(armPath));
            var src = arm.GetPixels32();
            var dst = new Color32[src.Length];
            for (var i = 0; i < src.Length; i++)
            {
                dst[i] = new Color32(src[i].b, src[i].r, 0, (byte)(255 - src[i].g));
            }

            var mao = new Texture2D(arm.width, arm.height, TextureFormat.RGBA32, false, true);
            mao.SetPixels32(dst);
            File.WriteAllBytes(maoPath, mao.EncodeToPNG());
            Object.DestroyImmediate(arm);
            Object.DestroyImmediate(mao);
            AssetDatabase.ImportAsset(maoPath);
        }

        private static void SetImport(string path, TextureImporterType type, bool srgb)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }

        private static Material LoadOrCreate(string name)
        {
            var path = $"{MaterialsPath}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material LoadMaterial(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/{name}.mat");

        private static int RemapModels()
        {
            var count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsPath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                foreach (var (slot, material) in Remap)
                {
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot), LoadMaterial(material));
                }

                importer.SaveAndReimport();
                count++;
            }

            return count;
        }
    }
}
