#if UNITY_EDITOR
using GuildMaster.Data;
using UnityEditor;

namespace GuildMaster.Debugging
{
    /// <summary>Поиск ассетов данных в редакторе для отладочных окон и меню.</summary>
    public static class EditorAssets
    {
        /// <summary>Первый GameConfig в проекте; <c>null</c>, если его нет.</summary>
        public static GameConfig FindGameConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(GameConfig));
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
#endif
