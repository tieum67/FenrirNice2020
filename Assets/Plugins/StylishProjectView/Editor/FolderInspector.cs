using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace StylishProjectView
{
    [CanEditMultipleObjects, CustomEditor(typeof(DefaultAsset))]
    public class FolderInspector : Editor
    {
        private bool m_IsFolder;
        private List<string> m_PathList = new List<string>();
        private List<string> m_FolderList = new List<string>();
        private List<bool> m_FolderToggleList = new List<bool>();
        private List<string> m_SubfolderList = new List<string>();
        private Dictionary<string, bool> m_SubfolderToggleList = new Dictionary<string, bool>();
        private List<Rect> m_RectList;

        private Action[] m_ToolbarAction = new Action[3];

        private static FolderInspector s_Instance = null;
        private static GUIContent s_Content = new GUIContent();
        private static int s_ToolbarIndex = 0;
        private static string[] s_ToolbarTitle = new[] { "Folder", "Settings" };

        void OnEnable()
        {
            var path = AssetDatabase.GetAssetPath(serializedObject.targetObject);
            m_IsFolder = AssetDatabase.IsValidFolder(path);
            if (!m_IsFolder) return;

            m_PathList = new List<string>();
            m_FolderList = new List<string>();
            m_FolderToggleList = new List<bool>();
            m_SubfolderList = new List<string>();
            m_SubfolderToggleList = new Dictionary<string, bool>();

            foreach (var obj in serializedObject.targetObjects)
            {
                m_PathList.Add(AssetDatabase.GetAssetPath(obj));
                m_FolderList.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(obj)));
                m_FolderToggleList.Add(true);
            }
            if (m_PathList.Count > 1)
            {
                m_PathList.Add(string.Empty);
                m_FolderList.Add("All");
                m_FolderToggleList.Add(true);
            }

            RebuildCreateList(true);

            m_ToolbarAction = new Action[3];
            m_ToolbarAction[0] = FolderGUI;
            m_ToolbarAction[1] = SettingsGUI;
        }

        public override void OnInspectorGUI()
        {
            if (!m_IsFolder) return;
            GUI.enabled = true;
            s_Instance = this;

            //if (m_FolderList.Count > 1)
            //    DrawFolderSelection();

            s_ToolbarIndex = GUILayout.Toolbar(s_ToolbarIndex, s_ToolbarTitle);
            GUILayout.Space(2);
            m_ToolbarAction[s_ToolbarIndex].Invoke();
            s_Instance = null;
        }

        #region FolderSelection
        private void DrawFolderSelection()
        {
            var rect = EditorGUILayout.GetControlRect(false, 0);
            rect.width = Screen.width - 14;
            rect.height = Screen.height;

            if (m_FolderToggleList.Count > 1)
            {
                m_FolderToggleList[m_FolderToggleList.Count - 1] = true;
                for (int i = 0; i < m_FolderToggleList.Count - 1; i++)
                    if (!m_FolderToggleList[i])
                    {
                        m_FolderToggleList[m_FolderToggleList.Count - 1] = false;
                        break;
                    }
            }

            var style = (GUIStyle)"OL Toggle";
            m_RectList = EditorGUIUtility.GetFlowLayoutedRects(rect, style, 12, 4, m_FolderList);
            for (int i = 0; i < m_RectList.Count; i++)
            {
                if (i == 0) rect = m_RectList[0];

                var buttonRect = m_RectList[i];
                buttonRect.xMax += 8;
                GUI.Box(buttonRect, GUIContent.none, "ShurikenModuleTitle");

                s_Content.text = m_FolderList[i];
                s_Content.tooltip = m_PathList[i];
                buttonRect.xMin += 16;
                GUI.Label(buttonRect, s_Content);

                buttonRect = m_RectList[i];
                buttonRect.xMax += 8;
                var toggle = GUI.Toggle(buttonRect, m_FolderToggleList[i], string.Empty, style);
                if (toggle != m_FolderToggleList[i])
                {
                    m_FolderToggleList[i] = toggle;
                    if (m_FolderToggleList.Count > 1 && i == m_FolderToggleList.Count - 1)
                        for (int j = 0; j < m_FolderToggleList.Count - 1; j++)
                            m_FolderToggleList[j] = toggle;
                }
                rect.yMax = Mathf.Max(rect.yMax, m_RectList[i].yMax);
            }
            if (m_RectList.Any())
                EditorGUILayout.GetControlRect(false, rect.height);
            GUILayout.Space(2);
        }
        #endregion

        public static void Update()
        {
            if (s_Instance == null) return;
            s_Instance.RebuildCreateList(false);
            s_Instance.SaveCreateToggleList();
        }

        private void RebuildCreateList(bool load)
        {
            if (load)
            {
                m_SubfolderToggleList.Clear();
                for (int i = 0; i < StylishSettings.instance.subfolderList.Count; i++)
                {
                    var toogle = false;
                    if (i < StylishSettings.instance.subfolderToggleList.Count)
                        toogle = StylishSettings.instance.subfolderToggleList[i];
                    m_SubfolderToggleList[StylishSettings.instance.subfolderList[i]] = toogle;
                }
                m_SubfolderList = StylishSettings.instance.subfolderList.Distinct().Where(s => s != string.Empty).ToList();
            }
            else
            {
                m_SubfolderList = StylishSettings.instance.subfolderList.Distinct().Where(s => s != string.Empty).ToList();
                var table = new Dictionary<string, bool>();
                foreach (var folder in m_SubfolderList)
                {
                    table[folder] = false;
                    if (m_SubfolderToggleList.ContainsKey(folder))
                        table[folder] = m_SubfolderToggleList[folder];
                }
                m_SubfolderToggleList = table;
            }

            if (m_SubfolderList.Any())
                m_SubfolderList.Add("Create Selected");
            else
                m_SubfolderList.Add("No Preset");
        }

        private void SaveCreateToggleList()
        {
            var toogleList = new List<bool>();
            foreach (var folder in StylishSettings.instance.subfolderList)
            {
                var toogle = false;
                if (m_SubfolderToggleList.ContainsKey(folder))
                    toogle = m_SubfolderToggleList[folder];
                toogleList.Add(toogle);
            }
            StylishSettings.instance.subfolderToggleList = toogleList;
            StylishSettings.Save();
        }

        private static void DrawHeading(string title)
        {
            var rect = EditorGUILayout.GetControlRect();
            rect.width = 120;
            GUI.Label(rect, string.Empty, "ChannelStripAttenuationBar");
            GUI.Label(rect, title);
        }

        #region FolderGUI
        private void FolderGUI()
        {
            DrawSubfolderCreation();
            DrawFolderStylish();
        }

        #region SubfolderCreation
        private void DrawSubfolderCreation()
        {
            DrawHeading("Create Subfolder");

            var rect = EditorGUILayout.GetControlRect(false, 0);
            rect.width = Screen.width - 14;
            rect.height = Screen.height;

            var style = (GUIStyle)"OL Toggle";
            m_RectList = EditorGUIUtility.GetFlowLayoutedRects(rect, style, 12, 4, m_SubfolderList);
            for (int i = 0; i < m_RectList.Count; i++)
            {
                if (i == 0) rect = m_RectList[0];

                var buttonRect = m_RectList[i];
                buttonRect.xMax += 8;
                GUI.Box(buttonRect, GUIContent.none, "ShurikenModuleTitle");

                s_Content.text = m_SubfolderList[i];
                s_Content.tooltip = string.Empty;
                buttonRect.xMin += 16;
                buttonRect.yMax -= 1;
                if (GUI.Button(buttonRect, s_Content, EditorStyles.miniButton))
                    if (i != m_RectList.Count - 1)
                        CreateSubfolder(m_SubfolderList[i]);
                    else
                        CreateSubfolder(null);

                buttonRect = m_RectList[i];
                buttonRect.width = 16;
                EditorGUI.BeginChangeCheck();
                if (!(m_RectList.Count == 1 && i == 0) && i != m_RectList.Count - 1)
                    m_SubfolderToggleList[m_SubfolderList[i]] = GUI.Toggle(buttonRect, m_SubfolderToggleList[m_SubfolderList[i]], string.Empty, style);
                if (EditorGUI.EndChangeCheck())
                    SaveCreateToggleList();

                rect.yMax = Mathf.Max(rect.yMax, m_RectList[i].yMax);
            }
            if (m_RectList.Any())
                EditorGUILayout.GetControlRect(false, rect.height);
            GUILayout.Space(2);
        }

        private void CreateSubfolder(string selected)
        {
            AssetDatabase.StartAssetEditing();
            var folders = selected.Yield();
            if (selected == null)
                folders = m_SubfolderToggleList.Where(kv => kv.Value).Select(kv => kv.Key);
            foreach (var path in m_PathList)
            {
                if (path == string.Empty) continue;
                var subFolders = AssetDatabase.GetSubFolders(path);
                foreach (var subfolder in folders)
                    if (!subFolders.Contains(path + "/" + subfolder))
                        AssetDatabase.CreateFolder(path, subfolder);
            }
            AssetDatabase.StopAssetEditing();
        }
        #endregion

        #region FolderStylish
        private void DrawFolderStylish()
        {
            DrawHeading("Stylish Folder");

            var datas = m_PathList.Where(p => p != string.Empty).Select(p => StylishSettings.GetPathData(p, true));

            EditorGUI.showMixedValue = datas.Select(d => d.enable).Distinct().Count() > 1;
            EditorGUI.BeginChangeCheck();
            var enable = EditorGUILayout.ToggleLeft("Enable stylish override for selected folders.", datas.First().enable);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var data in datas)
                {
                    if (enable && !data.enable && !data.bypass)
                        StylishSettings.AddPathData(data);
                    if (!enable && data.enable && !data.bypass)
                        StylishSettings.RemovePathData(data);
                    data.enable = enable;
                }
                StylishSettings.Save();
            }
            EditorGUI.showMixedValue = false;

            EditorGUI.showMixedValue = datas.Select(d => d.bypass).Distinct().Count() > 1;
            EditorGUI.BeginChangeCheck();
            var bypass = EditorGUILayout.ToggleLeft("Bypass all stylish.", datas.First().bypass);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var data in datas)
                {
                    if (bypass && !data.bypass && !data.enable)
                        StylishSettings.AddPathData(data);
                    if (!bypass && data.bypass && !data.enable)
                        StylishSettings.RemovePathData(data);
                    data.bypass = bypass;
                }
                StylishSettings.Save();
            }

            if (!enable || bypass) return;

            datas.First().stylishData.icon.DrawConfig(datas.Select(d => d.stylishData.icon));
            GUILayout.Space(2);
            datas.First().stylishData.highlight.DrawConfig(datas.Select(d => d.stylishData.highlight));

            var value = EditorGUILayout.ToggleLeft("Hide Selection Overlay.", StylishSettings.instance.general.hideSelection);
            if (value != StylishSettings.instance.general.hideSelection)
            {
                StylishSettings.instance.general.hideSelection = value;
                StylishSettings.Save();
            }
            GUILayout.Space(6);
        }
        #endregion
        #endregion

        #region AssetsGUI
        private void AssetsGUI()
        {
        }
        #endregion

        #region SettingsGUI
        private void SettingsGUI()
        {
            StylishSettingsEditor.DrawSettingsGUI();
        }
        #endregion
    }
}