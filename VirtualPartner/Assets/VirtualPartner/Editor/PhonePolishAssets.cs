using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VirtualPartner.Runtime.PhoneOS;

namespace VirtualPartner.EditorTools
{
    public static class PhonePolishAssets
    {
        private const string Folder="Assets/VirtualPartner/UI/PhoneOS/Resources/PhonePolish";
        public static void Prepare()
        {
            foreach(var path in Directory.GetFiles(Folder,"*.png"))ImportSprite(path.Replace('\\','/'),0);
            var theme=AssetDatabase.LoadAssetAtPath<PhoneVisualTheme>(Folder+"/Theme.asset");
            if(theme==null){theme=ScriptableObject.CreateInstance<PhoneVisualTheme>();AssetDatabase.CreateAsset(theme,Folder+"/Theme.asset");}
            const string fonts="Assets/VirtualPartner/UI/PhoneOS/Fonts/";
            theme.regular=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fonts+"NotoSans-Regular SDF.asset");
            theme.semibold=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fonts+"NotoSans-SemiBold SDF.asset");
            theme.light=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fonts+"NotoSans-Light SDF.asset");
            theme.card=Rounded("Card",24);theme.bubble=Rounded("Bubble",16);theme.control=Rounded("Control",14);
            EditorUtility.SetDirty(theme);AssetDatabase.SaveAssets();
        }
        private static void ImportSprite(string path,int border)
        {
            AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;
            importer.spriteBorder=Vector4.one*border;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        }
        private static Sprite Rounded(string name,int radius)
        {
            string path=Folder+"/"+name+".png";var tex=new Texture2D(128,128,TextureFormat.RGBA32,false);
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {var q=new Vector2(Mathf.Max(Mathf.Abs(x-63.5f)-(64-radius),0),Mathf.Max(Mathf.Abs(y-63.5f)-(64-radius),0));tex.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(radius-q.magnitude)));}
            tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());Object.DestroyImmediate(tex);ImportSprite(path,radius+1);return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        [MenuItem("VirtualPartner/Phone OS/Apply Representative Page Polish")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play Mode first.");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new System.InvalidOperationException("Save current user scene edits first.");
            Prepare();
            foreach(var stage in new[]{"LiveStage","VisualStage"})
            {
                var folder="Assets/VirtualPartner/UI/PhoneOS/"+stage;
                foreach(var name in new[]{"PhoneVisualRoot","momotalk","settings"})
                {
                    var path=folder+"/"+name+".prefab";if(!File.Exists(path))continue;
                    var root=PrefabUtility.LoadPrefabContents(path);
                    try{var shell=root.GetComponent<PhonePresentationShell>();if(shell!=null)PhoneVisualPolish.ApplyShell(shell);var app=root.GetComponent<PhonePreviewApp>();if(app!=null)PhoneVisualPolish.ApplyApp(app);PrefabUtility.SaveAsPrefabAsset(root,path);}
                    finally{PrefabUtility.UnloadPrefabContents(root);}
                }
                foreach(var id in new[]{"Momotalk","Camera","Settings","Debug"})
                {var def=AssetDatabase.LoadAssetAtPath<PhoneAppDefinition>(folder+"/"+id+".asset");if(def==null)continue;var so=new SerializedObject(def);so.FindProperty("icon").objectReferenceValue=PhoneVisualTheme.AppIcon(id.ToLowerInvariant());so.ApplyModifiedPropertiesWithoutUndo();}
            }
            foreach(var shell in Object.FindObjectsByType<PhonePresentationShell>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {PhoneVisualPolish.ApplyShell(shell);PrefabUtility.RecordPrefabInstancePropertyModifications(shell);EditorSceneManager.MarkSceneDirty(shell.gameObject.scene);}
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[PhonePolish] Updated representative pages; character and service objects retained.");
        }
    }
}
