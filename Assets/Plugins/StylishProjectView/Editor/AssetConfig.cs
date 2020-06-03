using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StylishProjectView
{
    public static class AssetConfig
    {
        [MenuItem("Assets/Create Stylish", true)]
        private static bool CreateStylishValidation()
        {
            return GetSelectedPath().Any();
        }

        [MenuItem("Assets/Create Stylish")]
        private static void CreateStylish()
        {
            var projectview = InternalUtils.GetLastInteractedProjectBrowser();
            ProjectViewGUI.handleCommandEvent = () =>
            {
                if (Event.current.commandName == ProjectViewGUI.kHandleCommandName + "AssetConfigPopup")
                {
                    Event.current.Use();
                    AssetConfigPopup.Popup(GUIUtility.ScreenToGUIRect(projectview.position), GetSelectedPath());
                }
            };
            projectview.SendEvent(EditorGUIUtility.CommandEvent(ProjectViewGUI.kHandleCommandName + "AssetConfigPopup"));
        }

        private static IEnumerable<string> GetSelectedPath()
        {
            var objs = Selection.objects.Where(o => AssetDatabase.Contains(o) && AssetDatabase.IsMainAsset(o));
            return objs.Select(o => AssetDatabase.GetAssetPath(o)).Where(p => p != "Assets");
        }

        public class AssetConfigPopup : PopupWindowContent
        {
            private const float kMinHeight = 200;

            private List<string> m_PathList;
            private float m_Height = kMinHeight;

            public static void Popup(Rect position, IEnumerable<string> paths)
            {
                var popup = new AssetConfigPopup();
                popup.m_PathList = paths.ToList();
                PopupWindow.Show(position, popup);
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(400, m_Height);
            }

            public override void OnGUI(Rect rect)
            {
                var wholeRect = EditorGUILayout.BeginVertical();

                var datas = m_PathList.Where(p => p != string.Empty).Select(p => StylishSettings.GetPathData(p, true));
                EditorGUI.showMixedValue = datas.Select(d => d.enable).Distinct().Count() > 1;
                EditorGUI.BeginChangeCheck();
                var enable = EditorGUILayout.ToggleLeft("Enable stylish override for selected folders and assets.", datas.First().enable);
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

                if (datas.Any(d => !AssetDatabase.IsValidFolder(d.path)))
                    EditorGUILayout.HelpBox("Replace icon only support for folder, overlay will be used for asset.", MessageType.Info, true);
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

                EditorGUILayout.EndVertical();
                GUILayout.FlexibleSpace();

                var height = m_Height;
                if (wholeRect.height != height)
                    height = Mathf.Max(kMinHeight, wholeRect.height);
                if (height != m_Height && Event.current.rawType == EventType.Repaint)
                {
                    m_Height = height;
                    editorWindow.Repaint();
                }
            }
        }
    }
}