using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StylishProjectView
{
    [CustomEditor(typeof(StylishSettings))]
    public class StylishSettingsEditor : Editor
    {
        private static bool s_iconOnly = false;

        public override void OnInspectorGUI()
        {
            DrawSettingsGUI();
        }

        private static void DrawHeading(string title)
        {
            var rect = EditorGUILayout.GetControlRect();
            rect.width = 120;
            GUI.Label(rect, string.Empty, "ChannelStripAttenuationBar");
            GUI.Label(rect, title);
        }

        public static void DrawSettingsGUI()
        {
            EditorGUIUtility.hierarchyMode = false;

            EditorGUI.BeginChangeCheck();
            StylishSettings.instance.general.enable = EditorGUILayout.ToggleLeft("Enable Stylish Project View", StylishSettings.instance.general.enable);
            if (EditorGUI.EndChangeCheck())
                StylishSettings.Save();
            EditorGUILayout.Space();

            if (StylishSettings.DrawSubfolderListConfig())
            {
                StylishSettings.Save();
                FolderInspector.Update();
            }
            if (StylishSettings.DrawStylishIconListConfig())
                StylishSettings.Save();
            EditorGUILayout.Space();

            DrawHeading("Path Stylish");
            EditorGUI.BeginChangeCheck();
            StylishSettings.instance.general.path = EditorGUILayout.ToggleLeft("Enable Path Stylish", StylishSettings.instance.general.path);
            if (EditorGUI.EndChangeCheck())
                StylishSettings.Save();
            EditorGUILayout.Space();

            DrawHeading("Keyword Stylish");
            EditorGUI.BeginChangeCheck();
            StylishSettings.instance.general.keyword = EditorGUILayout.ToggleLeft("Enable Keyword Stylish", StylishSettings.instance.general.keyword);
            if (EditorGUI.EndChangeCheck())
                StylishSettings.Save();
            if (StylishSettings.DrawKeywordStylishListConfig())
                StylishSettings.Save();
            EditorGUILayout.Space();

            DrawHeading("Auto Stylish");
            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginChangeCheck();
            StylishSettings.instance.general.autoLevel = EditorGUILayout.ToggleLeft("Auto Stylish by Hierarchy Level", StylishSettings.instance.general.autoLevel);
            if (EditorGUI.EndChangeCheck() && StylishSettings.instance.general.autoLevel)
                StylishSettings.instance.general.autoSibling = false;
            EditorGUI.indentLevel++;
            StylishSettings.instance.general.autoLevelAsset = EditorGUILayout.ToggleLeft("Apply Highlight to Asset", StylishSettings.instance.general.autoLevelAsset);
            EditorGUI.indentLevel--;
            EditorGUI.BeginChangeCheck();
            StylishSettings.instance.general.autoSibling = EditorGUILayout.ToggleLeft("Auto Stylish by Sibling Index", StylishSettings.instance.general.autoSibling);
            if (EditorGUI.EndChangeCheck() && StylishSettings.instance.general.autoSibling)
                StylishSettings.instance.general.autoLevel = false;
            if (EditorGUI.EndChangeCheck())
                StylishSettings.Save();
            if (StylishSettings.DrawCyclicStylishListConfig())
                StylishSettings.Save();
            EditorGUILayout.Space();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("HelpBox");
            s_iconOnly = EditorGUILayout.ToggleLeft("Stylish Icon Only", s_iconOnly);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Import Settings", "ButtonLeft"))
                ExportSettings.Import(StylishSettings.instance, s_iconOnly);
            if (GUILayout.Button("Export Settings", "ButtonRight"))
                ExportSettings.Export(StylishSettings.instance, s_iconOnly);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
    }
}