using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameDataEditor;
using ChronoArkMod;
using ChronoArkMod.Plugin;

namespace FreeRecruitMod
{
    /// <summary>
    /// Native Chrono Ark Plugin for Free Recruit Mod
    /// Shows all available characters in campfire recruit with sliding window navigation
    /// </summary>
    [PluginConfig("FreeRecruitMod", "Free Recruit Mod", "1.0.0")]
    public class FreeRecruitModPlugin : ChronoArkPlugin
    {
        private Harmony harmony;
        public static bool ModEnabled = true;

        public override void Initialize()
        {
            this.harmony = new Harmony(base.GetGuid());
            
            // Patch Init with Prefix to completely replace character setup
            var initMethod = AccessTools.Method(
                typeof(CharSelect_CampUI), 
                "Init", 
                new System.Type[] { typeof(Camp) }
            );
            var initPrefix = AccessTools.Method(
                typeof(CharSelect_CampUI_Init_Patch), 
                "Prefix"
            );
            this.harmony.Patch(initMethod, prefix: new HarmonyMethod(initPrefix));
            
            // Patch navigation methods
            var nextMethod = AccessTools.Method(typeof(CharSelect_CampUI), "SetListNextTr");
            var nextPrefix = AccessTools.Method(typeof(CharSelect_CampUI_Navigation_Patch), "SetListNextTr_Prefix");
            this.harmony.Patch(nextMethod, prefix: new HarmonyMethod(nextPrefix));
            
            var prevMethod = AccessTools.Method(typeof(CharSelect_CampUI), "SetListPrevTr");
            var prevPrefix = AccessTools.Method(typeof(CharSelect_CampUI_Navigation_Patch), "SetListPrevTr_Prefix");
            this.harmony.Patch(prevMethod, prefix: new HarmonyMethod(prevPrefix));
        }
        
        public override void Dispose()
        {
            if (this.harmony != null)
            {
                this.harmony.UnpatchAll(this.harmony.Id);
            }
        }
    }
    
    /// <summary>
    /// Patch for Init - COMPLETELY REPLACES the original method
    /// </summary>
    public static class CharSelect_CampUI_Init_Patch
    {
        // Static storage for the full character list and current window position
        public static List<GDECharacterData> ShuffledCharacters = new List<GDECharacterData>();
        public static int WindowStartIndex = 0;
        
        /// <summary>
        /// Prefix that completely replaces the original Init method
        /// Returns false to skip original entirely
        /// </summary>
        public static bool Prefix(CharSelect_CampUI __instance, Camp _Camp)
        {
            if (!FreeRecruitModPlugin.ModEnabled)
                return true; // Run original if mod disabled
            
            if (_Camp == null)
                return true; // Run original if no camp
            
            // Get all available characters
            ShuffledCharacters = GetAvailableCharacters();
            
            if (ShuffledCharacters.Count == 0)
                return true; // Run original if no characters
            
            // Shuffle
            var random = new System.Random();
            ShuffledCharacters = ShuffledCharacters.OrderBy(x => random.Next()).ToList();
            WindowStartIndex = 0;
            
            // Build the UI immediately (instead of letting original do it)
            BuildInitialUI(__instance, _Camp);
            
            // Skip original Init - we've handled everything
            return false;
        }
        
        static void BuildInitialUI(CharSelect_CampUI __instance, Camp _Camp)
        {
            var windowCharacters = GetWindowCharacters();
            
            if (windowCharacters.Count == 0)
                return;
            
            var charListField = AccessTools.Field(typeof(CharSelect_CampUI), "CharList");
            var charDocumentPrefabField = AccessTools.Field(typeof(CharSelect_CampUI), "CharDocumentPrefab");
            var alignField = AccessTools.Field(typeof(CharSelect_CampUI), "Align");
            var faceListField = AccessTools.Field(typeof(CharSelect_CampUI), "FaceList");
            var mainField = AccessTools.Field(typeof(CharSelect_CampUI), "Main");
            
            var charList = charListField.GetValue(__instance) as List<CharacterDocument>;
            var charDocumentPrefab = charDocumentPrefabField.GetValue(__instance) as GameObject;
            var align = alignField.GetValue(__instance) as Transform;
            var faceList = faceListField.GetValue(__instance) as List<CharSelect_Face>;
            
            if (charList == null || charDocumentPrefab == null || align == null || faceList == null)
                return;
            
            // Set the Main field (Camp reference)
            mainField.SetValue(__instance, _Camp);
            
            // Clear any existing
            foreach (var doc in charList)
            {
                if (doc != null && doc.gameObject != null)
                    UnityEngine.Object.Destroy(doc.gameObject);
            }
            charList.Clear();
            
            foreach (var face in faceList)
            {
                if (face != null)
                    face.gameObject.SetActive(false);
            }
            
            // Create CharacterDocuments
            for (int i = 0; i < windowCharacters.Count; i++)
            {
                var charData = windowCharacters[i];
                
                var docObj = UnityEngine.Object.Instantiate(charDocumentPrefab, align);
                var doc = docObj.GetComponent<CharacterDocument>();
                
                if (doc == null)
                {
                    UnityEngine.Object.Destroy(docObj);
                    continue;
                }
                
                var docMainField = AccessTools.Field(typeof(CharacterDocument), "Main");
                var docIndexField = AccessTools.Field(typeof(CharacterDocument), "Index");
                docMainField.SetValue(doc, __instance);
                docIndexField.SetValue(doc, i);
                doc.Init(charData);
                
                charList.Add(doc);
                
                if (i < faceList.Count)
                {
                    SetupFace(faceList[i], charData);
                }
            }
            
            // Call MoveCharacterDoc to position them correctly
            var moveCharacterDocMethod = AccessTools.Method(typeof(CharSelect_CampUI), "MoveCharacterDoc");
            moveCharacterDocMethod?.Invoke(__instance, new object[] { charList.Count, true });
            
            UpdateGamepadLayout(__instance, charList);
            
            // Show only the selected character (index 0), hide others
            ShowOnlySelectedCharacter(__instance);
            
            // Show only the selected face, hide others
            ShowOnlySelectedFace(__instance);
        }
        
        static void ShowOnlySelectedCharacter(CharSelect_CampUI __instance)
        {
            var charListField = AccessTools.Field(typeof(CharSelect_CampUI), "CharList");
            var charList = charListField.GetValue(__instance) as List<CharacterDocument>;
            
            if (charList == null) return;
            
            for (int i = 0; i < charList.Count; i++)
            {
                var doc = charList[i];
                if (doc != null)
                {
                    // Only show the selected character (Index == 0)
                    bool isSelected = (doc.Index == 0);
                    doc.gameObject.SetActive(isSelected);
                }
            }
        }
        
        static void ShowOnlySelectedFace(CharSelect_CampUI __instance)
        {
            var charListField = AccessTools.Field(typeof(CharSelect_CampUI), "CharList");
            var charList = charListField.GetValue(__instance) as List<CharacterDocument>;
            
            var faceListField = AccessTools.Field(typeof(CharSelect_CampUI), "FaceList");
            var faceList = faceListField.GetValue(__instance) as List<CharSelect_Face>;
            
            if (charList == null || faceList == null) return;
            
            for (int i = 0; i < faceList.Count && i < charList.Count; i++)
            {
                // Show only the face corresponding to selected character (Index == 0)
                bool isSelected = (charList[i].Index == 0);
                faceList[i].gameObject.SetActive(isSelected);
            }
        }
        
        public static List<GDECharacterData> GetAvailableCharacters()
        {
            var available = new List<GDECharacterData>();
            List<string> dataKeys = new List<string>();
            GDEDataManager.GetAllDataKeysBySchema(GDESchemaKeys.Character, out dataKeys);
            
            foreach (string key in dataKeys)
            {
                if (key == GDEItemKeys.Character_TW_Blue || key == GDEItemKeys.Character_TW_Red)
                    continue;
                
                if (PlayData.TSavedata.DonAliveChars.Contains(key))
                    continue;
                
                var charData = new GDECharacterData(key);
                
                if (charData.Off)
                    continue;
                
                if (charData.Lock && !SaveManager.IsUnlock(charData.Key, SaveManager.NowData.unlockList.UnlockCharacter))
                    continue;
                
                if (SaveManager.NowData.storydata.MainStoryProgress >= 7 && 
                    PlayData.TSavedata.NowPlayMode == EPlayMode.StoryMode && 
                    string.IsNullOrEmpty(PlayData.SpalcialRule))
                {
                    if (key == "Azar" || key == "Phoenix")
                        continue;
                }
                
                available.Add(charData);
            }
            
            return available;
        }
        
        public static List<GDECharacterData> GetWindowCharacters()
        {
            var window = new List<GDECharacterData>();
            int count = ShuffledCharacters.Count;
            
            if (count == 0) return window;
            
            for (int i = 0; i < 4 && i < count; i++)
            {
                int index = (WindowStartIndex + i) % count;
                window.Add(ShuffledCharacters[index]);
            }
            
            return window;
        }
        
        static void SetupFace(CharSelect_Face face, GDECharacterData charData)
        {
            var faceAlignField = AccessTools.Field(typeof(CharSelect_Face), "FaceAlign");
            var faceAlign = faceAlignField.GetValue(face) as Transform;
            
            if (faceAlign != null)
            {
                foreach (Transform child in faceAlign)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
            
            string facePath = "";
            if (CharacterSkinData.SkinCheck(charData.Key))
            {
                bool isFaceSmallEmpty = false;
                facePath = CharacterSkinData.GetFaceChar(charData, out isFaceSmallEmpty);
                if (isFaceSmallEmpty && faceAlign != null)
                    faceAlign.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            }
            else
            {
                facePath = charData.FaceSmallChar_Path.IsEmpty() ? charData.FaceOriginChar_Path : charData.FaceSmallChar_Path;
                if (charData.FaceSmallChar_Path.IsEmpty() && faceAlign != null)
                    faceAlign.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            }
            
            if (!string.IsNullOrEmpty(facePath) && faceAlign != null)
            {
                AddressableLoadManager.Instantiate(facePath, AddressableLoadManager.ManageType.Stage, faceAlign);
            }
            
            var faceKeyField = AccessTools.Field(typeof(CharSelect_Face), "Key");
            faceKeyField.SetValue(face, charData.Key);
            face.gameObject.SetActive(true);
            
            var offMethod = AccessTools.Method(typeof(CharSelect_Face), "Off");
            offMethod?.Invoke(face, null);
        }
        
        static void UpdateGamepadLayout(CharSelect_CampUI __instance, List<CharacterDocument> charList)
        {
            var charSelectLayoutField = AccessTools.Field(typeof(CharSelect_CampUI), "CharSelectLayout");
            var charSelectLayout = charSelectLayoutField.GetValue(__instance);
            
            if (charSelectLayout != null)
            {
                var targetsField = AccessTools.Field(charSelectLayout.GetType(), "Targets");
                var targets = targetsField.GetValue(charSelectLayout) as List<UnityEngine.RectTransform>;
                
                targets?.Clear();
                foreach (var charDoc in charList)
                {
                    var rectTransform = charDoc?.GetComponent<UnityEngine.RectTransform>();
                    if (rectTransform != null)
                        targets?.Add(rectTransform);
                }
                
                var columnNumField = AccessTools.Field(charSelectLayout.GetType(), "ColumnNum");
                columnNumField?.SetValue(charSelectLayout, targets?.Count ?? 0);
            }
        }
    }
    
    /// <summary>
    /// Patch for navigation - handles left/right arrow sliding
    /// </summary>
    public static class CharSelect_CampUI_Navigation_Patch
    {
        public static bool SetListNextTr_Prefix(CharSelect_CampUI __instance)
        {
            if (!FreeRecruitModPlugin.ModEnabled)
                return true;
            
            if (CharSelect_CampUI_Init_Patch.ShuffledCharacters.Count == 0)
                return true;
            
            CharSelect_CampUI_Init_Patch.WindowStartIndex = 
                (CharSelect_CampUI_Init_Patch.WindowStartIndex + 1) % 
                CharSelect_CampUI_Init_Patch.ShuffledCharacters.Count;
            
            RebuildUI(__instance);
            
            return false;
        }
        
        public static bool SetListPrevTr_Prefix(CharSelect_CampUI __instance)
        {
            if (!FreeRecruitModPlugin.ModEnabled)
                return true;
            
            if (CharSelect_CampUI_Init_Patch.ShuffledCharacters.Count == 0)
                return true;
            
            CharSelect_CampUI_Init_Patch.WindowStartIndex--;
            if (CharSelect_CampUI_Init_Patch.WindowStartIndex < 0)
                CharSelect_CampUI_Init_Patch.WindowStartIndex = 
                    CharSelect_CampUI_Init_Patch.ShuffledCharacters.Count - 1;
            
            RebuildUI(__instance);
            
            return false;
        }
        
        static void RebuildUI(CharSelect_CampUI __instance)
        {
            var windowCharacters = CharSelect_CampUI_Init_Patch.GetWindowCharacters();
            
            if (windowCharacters.Count == 0)
                return;
            
            var charListField = AccessTools.Field(typeof(CharSelect_CampUI), "CharList");
            var charDocumentPrefabField = AccessTools.Field(typeof(CharSelect_CampUI), "CharDocumentPrefab");
            var alignField = AccessTools.Field(typeof(CharSelect_CampUI), "Align");
            var faceListField = AccessTools.Field(typeof(CharSelect_CampUI), "FaceList");
            
            var charList = charListField.GetValue(__instance) as List<CharacterDocument>;
            var charDocumentPrefab = charDocumentPrefabField.GetValue(__instance) as GameObject;
            var align = alignField.GetValue(__instance) as Transform;
            var faceList = faceListField.GetValue(__instance) as List<CharSelect_Face>;
            
            if (charList == null || charDocumentPrefab == null || align == null || faceList == null)
                return;
            
            foreach (var doc in charList)
            {
                if (doc != null && doc.gameObject != null)
                    UnityEngine.Object.Destroy(doc.gameObject);
            }
            charList.Clear();
            
            foreach (var face in faceList)
            {
                if (face != null)
                    face.gameObject.SetActive(false);
            }
            
            for (int i = 0; i < windowCharacters.Count; i++)
            {
                var charData = windowCharacters[i];
                
                var docObj = UnityEngine.Object.Instantiate(charDocumentPrefab, align);
                var doc = docObj.GetComponent<CharacterDocument>();
                
                if (doc == null)
                {
                    UnityEngine.Object.Destroy(docObj);
                    continue;
                }
                
                var docMainField = AccessTools.Field(typeof(CharacterDocument), "Main");
                var docIndexField = AccessTools.Field(typeof(CharacterDocument), "Index");
                docMainField.SetValue(doc, __instance);
                docIndexField.SetValue(doc, i);
                doc.Init(charData);
                
                charList.Add(doc);
                
                if (i < faceList.Count)
                {
                    var faceAlignField = AccessTools.Field(typeof(CharSelect_Face), "FaceAlign");
                    var faceAlign = faceAlignField.GetValue(faceList[i]) as Transform;
                    
                    if (faceAlign != null)
                    {
                        foreach (Transform child in faceAlign)
                            UnityEngine.Object.Destroy(child.gameObject);
                    }
                    
                    string facePath = "";
                    if (CharacterSkinData.SkinCheck(charData.Key))
                    {
                        bool isFaceSmallEmpty = false;
                        facePath = CharacterSkinData.GetFaceChar(charData, out isFaceSmallEmpty);
                        if (isFaceSmallEmpty && faceAlign != null)
                            faceAlign.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                    }
                    else
                    {
                        facePath = charData.FaceSmallChar_Path.IsEmpty() ? charData.FaceOriginChar_Path : charData.FaceSmallChar_Path;
                        if (charData.FaceSmallChar_Path.IsEmpty() && faceAlign != null)
                            faceAlign.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                    }
                    
                    if (!string.IsNullOrEmpty(facePath) && faceAlign != null)
                    {
                        AddressableLoadManager.Instantiate(facePath, AddressableLoadManager.ManageType.Stage, faceAlign);
                    }
                    
                    var faceKeyField = AccessTools.Field(typeof(CharSelect_Face), "Key");
                    faceKeyField.SetValue(faceList[i], charData.Key);
                    faceList[i].gameObject.SetActive(true);
                    
                    var offMethod = AccessTools.Method(typeof(CharSelect_Face), "Off");
                    offMethod?.Invoke(faceList[i], null);
                }
            }
            
            var moveCharacterDocMethod = AccessTools.Method(typeof(CharSelect_CampUI), "MoveCharacterDoc");
            moveCharacterDocMethod?.Invoke(__instance, new object[] { charList.Count, true });
            
            var charSelectLayoutField = AccessTools.Field(typeof(CharSelect_CampUI), "CharSelectLayout");
            var charSelectLayout = charSelectLayoutField.GetValue(__instance);
            
            if (charSelectLayout != null)
            {
                var targetsField = AccessTools.Field(charSelectLayout.GetType(), "Targets");
                var targets = targetsField.GetValue(charSelectLayout) as List<UnityEngine.RectTransform>;
                
                targets?.Clear();
                foreach (var charDoc in charList)
                {
                    var rectTransform = charDoc?.GetComponent<UnityEngine.RectTransform>();
                    if (rectTransform != null)
                        targets?.Add(rectTransform);
                }
                
                var columnNumField = AccessTools.Field(charSelectLayout.GetType(), "ColumnNum");
                columnNumField?.SetValue(charSelectLayout, targets?.Count ?? 0);
            }
            
            // Show only the selected character (index 0), hide others
            ShowOnlySelectedCharacter(__instance);
            
            // Show only the selected face, hide others
            ShowOnlySelectedFace(__instance);
        }
        
        static void ShowOnlySelectedCharacter(CharSelect_CampUI __instance)
        {
            var charListField = AccessTools.Field(typeof(CharSelect_CampUI), "CharList");
            var charList = charListField.GetValue(__instance) as List<CharacterDocument>;
            
            if (charList == null) return;
            
            for (int i = 0; i < charList.Count; i++)
            {
                var doc = charList[i];
                if (doc != null)
                {
                    // Only show the selected character (Index == 0)
                    bool isSelected = (doc.Index == 0);
                    doc.gameObject.SetActive(isSelected);
                }
            }
        }
        
        static void ShowOnlySelectedFace(CharSelect_CampUI __instance)
        {
            var charListField = AccessTools.Field(typeof(CharSelect_CampUI), "CharList");
            var charList = charListField.GetValue(__instance) as List<CharacterDocument>;
            
            var faceListField = AccessTools.Field(typeof(CharSelect_CampUI), "FaceList");
            var faceList = faceListField.GetValue(__instance) as List<CharSelect_Face>;
            
            if (charList == null || faceList == null) return;
            
            for (int i = 0; i < faceList.Count && i < charList.Count; i++)
            {
                // Show only the face corresponding to selected character (Index == 0)
                bool isSelected = (charList[i].Index == 0);
                faceList[i].gameObject.SetActive(isSelected);
            }
        }
    }
}