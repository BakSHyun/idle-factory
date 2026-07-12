using UnityEditor;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 앱 아이콘 설정 — 에디터 메뉴(Soyoz > Set App Icon) 또는
    /// 배치 모드(-executeMethod IdleGame.EditorTools.IconSetter.Apply)로 실행.
    /// </summary>
    public static class IconSetter
    {
        private const string IconPath = "Assets/Icon/app_icon.png";

        [MenuItem("Soyoz/Set App Icon")]
        public static void Apply()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            if (importer != null && !importer.isReadable)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                Debug.LogError($"[IconSetter] 아이콘 없음: {IconPath}");
                return;
            }
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown,
                new[] { icon }, IconKind.Application);
            PlayerSettings.productName = "꼬마 차사 키우기";
            PlayerSettings.companyName = "Soyoz";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,
                "com.soyoz.kkomachasa");
            AssetDatabase.SaveAssets();
            Debug.Log("[IconSetter] 앱 아이콘·제품명·패키지명 설정 완료");
        }
    }
}
