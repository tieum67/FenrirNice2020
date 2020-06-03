using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace StylishProjectView
{
    [Serializable]
    public class GeneralSetting
    {
        public bool enable = true;
        public bool path = true;
        public bool keyword = true;
        public bool autoLevel = false;
        public bool autoLevelAsset = false;
        public bool autoSibling = false;
        public bool hideSelection = false;
    }

    [Serializable]
    public class PathData
    {
        public string path;
        public bool bypass = false;
        public bool enable = false;
        public bool autoLevel = false;
        public bool autoSibling = false;
        public StylishData stylishData = new StylishData();
    }

    [Serializable]
    public class StylishData
    {
        public string name;
        public StylishIconOverride icon = new StylishIconOverride();
        public StylishHighlight highlight = new StylishHighlight();
    }

    public class StylishSettings : ScriptableObject
    {
        private const string kSearchPath = "/StylishProjectView/Editor";
        private const string kSearchFile = "/StylishProjectView/Editor/StylishSettings.asset";

        public static StylishSettings instance { get { return GetInstance(); } }
        private static StylishSettings s_Instance = null;

        public GeneralSetting general = new GeneralSetting();

        public List<string> subfolderList = new List<string>();
        public List<bool> subfolderToggleList = new List<bool>();

        public List<StylishIcon> stylishIconList = new List<StylishIcon>();
        public List<StylishData> cyclicStylishList = new List<StylishData>();
        public List<StylishData> keywordStylishList = new List<StylishData>();
        public List<StylishHighlight> highlightPresetList = new List<StylishHighlight>();

        [SerializeField]
        private List<PathData> m_PathDataList = new List<PathData>();
        private Dictionary<string, PathData> m_PathDataTable = new Dictionary<string, PathData>();

        private Dictionary<string, StylishData> m_KeywordStylishTable = new Dictionary<string, StylishData>();

        [MenuItem("Tools/Stylish Project View")]
        private static void StylishProjectView()
        {
            Selection.objects = new UnityEngine.Object[] { instance };
        }

        private static StylishSettings GetInstance()
        {
            if (s_Instance != null) return s_Instance;

            var guids = AssetDatabase.FindAssets("t:StylishProjectView.StylishSettings");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(kSearchFile))
                    s_Instance = AssetDatabase.LoadAssetAtPath<StylishSettings>(path);
                if (s_Instance != null) break;
            }

            if (s_Instance == null && guids.Any())
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                s_Instance = AssetDatabase.LoadAssetAtPath<StylishSettings>(path);
            }

            if (s_Instance == null)
            {
                var savePath = "Assets/StylishSettings.asset";
                foreach (var path in AssetDatabase.GetAllAssetPaths())
                    if (path.EndsWith(kSearchPath))
                    {
                        savePath = path + "/StylishSettings.asset";
                        break;
                    }

                s_Instance = CreateInstance<StylishSettings>();
                if (!AssetDatabase.IsValidFolder("Assets/AssetStoreTools"))
                {
                    AssetDatabase.CreateAsset(s_Instance, savePath);
                    AssetDatabase.SaveAssets();

                    ExportSettings.Import(s_Instance, Application.dataPath + "/StylishProjectView/StylishSettings.json", false);
                    EditorUtility.SetDirty(s_Instance);
                    AssetDatabase.ImportAsset(savePath);
                }
            }
            RebuildTable();
            return s_Instance;
        }

        public static void RebuildTable()
        {
            foreach (var data in instance.m_PathDataList)
                instance.m_PathDataTable[data.path] = data;
            foreach (var data in instance.keywordStylishList)
                instance.m_KeywordStylishTable[data.name] = data;
        }

        public static void Save()
        {
            EditorUtility.SetDirty(instance);
            EditorApplication.RepaintProjectWindow();
        }

        public static void Clear()
        {
            instance.general = new GeneralSetting();
            foreach (var item in instance.stylishIconList)
                DestroyImmediate(item, true);
            instance.subfolderList.Clear();
            instance.subfolderToggleList.Clear();
            instance.stylishIconList.Clear();
            instance.cyclicStylishList.Clear();
            instance.keywordStylishList.Clear();
            instance.highlightPresetList.Clear();
            instance.m_PathDataList.Clear();
            instance.m_PathDataTable.Clear();
            instance.m_KeywordStylishTable.Clear();
        }

        public static void AddSubObject(UnityEngine.Object obj)
        {
            var path = AssetDatabase.GetAssetPath(instance);
            AssetDatabase.AddObjectToAsset(obj, path);
        }

        public static StylishData GetCyclicStylishData(int index)
        {
            if (index < 0 || instance.cyclicStylishList.Count == 0) return null;
            index %= instance.cyclicStylishList.Count;
            return instance.cyclicStylishList[index];
        }

        public static StylishData GetHierarchyLevelStylishData(int index)
        {
            if (!instance.general.autoLevel) return null;
            return GetCyclicStylishData(index);
        }

        public static StylishData GetSiblingIndexStylishData(int index)
        {
            if (!instance.general.autoSibling) return null;
            return GetCyclicStylishData(index);
        }

        public static StylishData GetKeywordStylishData(string keyword)
        {
            if (!instance.general.keyword) return null;
            StylishData data;
            if (instance.m_KeywordStylishTable.TryGetValue(keyword, out data))
                return data;
            return null;
        }

        public static List<string> GetPathStylisPaths()
        {
            return instance.m_PathDataList.Select(d => d.path).ToList();
        }

        public static bool GetPathStylishBypass(string path)
        {
            var data = GetPathData(path, false);
            if (data == null) return false;
            return data.bypass;
        }

        public static StylishData GetPathStylishData(string path)
        {
            if (!instance.general.path) return null;
            var data = GetPathData(path, false);
            if (data == null) return null;
            return data.stylishData;
        }

        public static PathData GetPathData(string path, bool create)
        {
            PathData data;
            if (instance.m_PathDataTable.TryGetValue(path, out data))
                return data;
            if (!create)
                return null;
            data = new PathData();
            data.path = path;
            return data;
        }

        public static bool AddPathData(PathData data)
        {
            if (instance.m_PathDataTable.ContainsKey(data.path))
                return false;
            instance.m_PathDataList.Add(data);
            instance.m_PathDataTable[data.path] = data;
            return true;
        }

        public static void RemovePathData(PathData data)
        {
            instance.m_PathDataList.Remove(data);
            instance.m_PathDataTable.Remove(data.path);
        }

        #region Subfolder List Config
        private static ReorderableList s_SubfolderReorderableList;
        private static bool s_SubfolderListReorder = false;
        private static bool s_SubfolderListFoldout = false;

        public static bool DrawSubfolderListConfig()
        {
            if (s_SubfolderReorderableList == null)
            {
                var subfolderList = instance.subfolderList;
                s_SubfolderReorderableList = new ReorderableList(subfolderList, typeof(string), true, true, true, true);
                s_SubfolderReorderableList.drawHeaderCallback += (Rect rect) =>
                {
                    rect.x -= 6;
                    rect.y++;
                    s_SubfolderListFoldout = EditorGUI.Foldout(rect, s_SubfolderListFoldout, "Subfolder Preset", true);
                };
                s_SubfolderReorderableList.drawElementCallback += (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    rect.height = 16;
                    subfolderList[index] = EditorGUI.TextField(rect, "Subfolder " + (index + 1), subfolderList[index]);
                };
                s_SubfolderReorderableList.onAddCallback += (list) => subfolderList.Add(string.Empty);
                s_SubfolderReorderableList.onReorderCallback += (list) => s_SubfolderListReorder = true;
                s_SubfolderReorderableList.elementHeight = 18;
            }

            EditorGUI.BeginChangeCheck();
            if (s_SubfolderListFoldout)
                s_SubfolderReorderableList.DoLayoutList();
            else
                s_SubfolderListFoldout = EditorGUILayout.Foldout(s_SubfolderListFoldout, "Subfolder Preset", true);
            if (EditorGUI.EndChangeCheck() || s_SubfolderListReorder)
            {
                s_SubfolderListReorder = false;
                return true;
            }

            return false;
        }
        #endregion

        #region Stylish Icon List Config
        private static ReorderableList s_StylishIconReorderableList;
        private static bool s_StylishIconListReorder = false;
        private static bool s_StylishIconListFoldout = false;
        private static StylishIcon s_Copy;
        private static StylishIcon s_Remove;

        public static bool DrawStylishIconListConfig()
        {
            if (s_StylishIconReorderableList == null)
            {
                var stylishIconList = instance.stylishIconList;
                s_StylishIconReorderableList = new ReorderableList(stylishIconList, typeof(StylishIcon), true, true, true, true);
                s_StylishIconReorderableList.drawHeaderCallback += (Rect rect) =>
                {
                    rect.x -= 6;
                    rect.y--;
                    s_StylishIconListFoldout = EditorGUI.Foldout(rect, s_StylishIconListFoldout, "Stylish Icon Preset", true);
                };
                s_StylishIconReorderableList.drawElementCallback += (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    var icon = stylishIconList[index];
                    var r = new Rect(rect);
                    r.yMin += 2;
                    r.yMax -= 2;
                    GUI.Box(r, GUIContent.none, "ShurikenModuleBg");
                    r.x += 2;
                    r.width = 100;
                    r.height = EditorGUIUtility.singleLineHeight;
                    GUI.Label(r, string.Empty, "ChannelStripAttenuationBar");
                    GUI.Label(r, "Preset " + (index + 1));
                    if (icon == null) return;

                    var offset = new RectOffset(64, 20, 0, 0);
                    if (GUI.Button(offset.Remove(r), GUIContent.none, "OL Plus"))
                        s_Copy = icon;
                    offset = new RectOffset(80, 4, 0, 0);
                    if (GUI.Button(offset.Remove(r), GUIContent.none, "OL Minus"))
                        s_Remove = icon;

                    r.y += r.height + 2;
                    if (GUI.Button(r, "Icon Creator"))
                        StylishIconCreator.Popup(r, icon);

                    r = new Rect(rect);
                    r.x += 120;
                    icon.DrawPreview(r);
                };
                s_StylishIconReorderableList.onAddCallback += (list) => stylishIconList.Add(StylishIcon.Create());
                s_StylishIconReorderableList.onRemoveCallback += (list) =>
                {
                    var icon = stylishIconList[s_StylishIconReorderableList.index];
                    if (icon == null) return;
                    stylishIconList.Remove(icon);
                    icon.Destory();
                };
                s_StylishIconReorderableList.onReorderCallback += (list) => s_StylishIconListReorder = true;
                s_StylishIconReorderableList.elementHeightCallback += (index) => 68;
            }

            EditorGUI.BeginChangeCheck();
            if (s_StylishIconListFoldout)
                s_StylishIconReorderableList.DoLayoutList();
            else
                s_StylishIconListFoldout = EditorGUILayout.Foldout(s_StylishIconListFoldout, "Stylish Icon Preset", true);
            if (s_Copy != null)
            {
                var icon = StylishIcon.Create();
                EditorUtility.CopySerialized(s_Copy, icon);
                int index = instance.stylishIconList.IndexOf(s_Copy);
                instance.stylishIconList.Insert(index, icon);
            }
            if (s_Remove != null)
            {
                instance.stylishIconList.Remove(s_Remove);
                s_Remove.Destory();
            }
            if (EditorGUI.EndChangeCheck() || s_StylishIconListReorder || s_Copy != null || s_Remove != null)
            {
                s_StylishIconListReorder = false;
                s_Copy = s_Remove = null;
                return true;
            }

            return false;
        }
        #endregion

        #region Cyclic Stylish List Config
        private static ReorderableList s_CyclicStylishReorderableList;
        private static bool s_CyclicStylishListReorder = false;
        private static bool s_CyclicStylishListFoldout = false;

        public static bool DrawCyclicStylishListConfig()
        {
            if (s_CyclicStylishReorderableList == null)
            {
                var cyclicStylishList = instance.cyclicStylishList;
                s_CyclicStylishReorderableList = new ReorderableList(cyclicStylishList, typeof(StylishData), true, true, true, true);
                s_CyclicStylishReorderableList.drawHeaderCallback += (Rect rect) =>
                {
                    rect.x -= 6;
                    rect.y--;
                    s_CyclicStylishListFoldout = EditorGUI.Foldout(rect, s_CyclicStylishListFoldout, "Cyclic Stylish", true);
                };
                s_CyclicStylishReorderableList.drawElementCallback += (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    var r = new Rect(rect);
                    r.yMin += 2;
                    r.yMax -= 2;
                    GUI.Box(r, GUIContent.none, "ShurikenModuleBg");
                    r.x += 2;
                    r.width = 80;
                    r.height = EditorGUIUtility.singleLineHeight;
                    GUI.Label(r, string.Empty, "ChannelStripAttenuationBar");
                    GUI.Label(r, "Stylish " + (index + 1));
                    r.y += r.height + 2;

                    r.width = rect.width - 4;
                    r.height = cyclicStylishList[index].icon.GetConfigHeight();
                    cyclicStylishList[index].icon.DrawConfig(r);
                    r.y += r.height + 2;

                    r.height = cyclicStylishList[index].highlight.GetConfigHeight();
                    cyclicStylishList[index].highlight.DrawConfig(r);
                };
                s_CyclicStylishReorderableList.onReorderCallback += (list) => s_CyclicStylishListReorder = true;
                s_CyclicStylishReorderableList.elementHeightCallback += (index) =>
                {
                    float height = EditorGUIUtility.singleLineHeight + 6;
                    height += cyclicStylishList[index].icon.GetConfigHeight() + 2;
                    height += cyclicStylishList[index].highlight.GetConfigHeight() + 2;
                    return height;
                };
            }

            EditorGUI.BeginChangeCheck();
            if (s_CyclicStylishListFoldout)
                s_CyclicStylishReorderableList.DoLayoutList();
            else
                s_CyclicStylishListFoldout = EditorGUILayout.Foldout(s_CyclicStylishListFoldout, "Cyclic Stylish", true);
            if (EditorGUI.EndChangeCheck() || s_CyclicStylishListReorder)
            {
                s_CyclicStylishListReorder = false;
                return true;
            }

            return false;
        }
        #endregion

        #region Keyword Stylish List Config
        private static ReorderableList s_KeywordStylishReorderableList;
        private static bool s_KeywordStylishListReorder = false;
        private static bool s_KeywordStylishListFoldout = false;

        public static bool DrawKeywordStylishListConfig()
        {
            if (s_KeywordStylishReorderableList == null)
            {
                var keywordStylishList = instance.keywordStylishList;
                s_KeywordStylishReorderableList = new ReorderableList(keywordStylishList, typeof(PathData), true, true, true, true);
                s_KeywordStylishReorderableList.drawHeaderCallback += (Rect rect) =>
                {
                    rect.x -= 6;
                    rect.y--;
                    s_KeywordStylishListFoldout = EditorGUI.Foldout(rect, s_KeywordStylishListFoldout, "Keyword Stylish", true);
                };
                s_KeywordStylishReorderableList.drawElementCallback += (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    var data = keywordStylishList[index];
                    var r = new Rect(rect);
                    r.yMin += 2;
                    r.yMax -= 2;
                    GUI.Box(r, GUIContent.none, "ShurikenModuleBg");
                    r.x += 2;
                    r.width = 80;
                    r.height = EditorGUIUtility.singleLineHeight;
                    GUI.Label(r, string.Empty, "ChannelStripAttenuationBar");
                    GUI.Label(r, "Keyword");
                    r.x += r.width + 8;
                    r.width = 160;
                    data.name = EditorGUI.DelayedTextField(r, data.name);
                    r.xMin = rect.xMin + 2;
                    r.y += r.height + 2;

                    r.width = rect.width - 4;
                    r.height = data.icon.GetConfigHeight();
                    data.icon.DrawConfig(r);
                    r.y += r.height + 2;

                    r.height = data.highlight.GetConfigHeight();
                    data.highlight.DrawConfig(r);
                };
                s_KeywordStylishReorderableList.onReorderCallback += (list) => s_KeywordStylishListReorder = true;
                s_KeywordStylishReorderableList.elementHeightCallback += (index) =>
                {
                    float height = EditorGUIUtility.singleLineHeight + 6;
                    height += keywordStylishList[index].icon.GetConfigHeight() + 2;
                    height += keywordStylishList[index].highlight.GetConfigHeight() + 2;
                    return height;
                };
            }

            EditorGUI.BeginChangeCheck();
            if (s_KeywordStylishListFoldout)
                s_KeywordStylishReorderableList.DoLayoutList();
            else
                s_KeywordStylishListFoldout = EditorGUILayout.Foldout(s_KeywordStylishListFoldout, "Keyword Stylish", true);
            if (EditorGUI.EndChangeCheck() || s_KeywordStylishListReorder)
            {
                s_KeywordStylishListReorder = false;
                instance.m_KeywordStylishTable = instance.keywordStylishList.Where(k => k.name != null).ToDictionary(k => k.name, v => v);
                return true;
            }

            return false;
        }
        #endregion
    }
}