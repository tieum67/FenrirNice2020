using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;

namespace StylishProjectView
{
    [InitializeOnLoad]
    public static class ProjectViewGUI
    {
        private class Styles
        {
            public GUIStyle label = new GUIStyle("PR Label");
            public GUIStyle gridLabel = new GUIStyle("ProjectBrowserGridLabel");
            public GUIStyle background = new GUIStyle("ProjectBrowserIconAreaBg");
            public Color textColor;
            public FontStyle fontStyle;
            public Styles()
            {
                textColor = label.normal.textColor;
                fontStyle = label.fontStyle;
            }
        }
        private static Styles s_Styles;

        public const string kHandleCommandName = "HandleCommand";
        public static Action handleCommandEvent;

        private static Dictionary<string, int> s_InstanceIDTable = new Dictionary<string, int>();
        private static Dictionary<string, string[]> s_SubfolderTable = new Dictionary<string, string[]>();
        private static Dictionary<string, int> s_HierarchyLevelTable = new Dictionary<string, int>();
        private static Dictionary<string, int> s_SiblingIndexTable = new Dictionary<string, int>();

        private static StylishData s_StylishData = new StylishData();
        private static StylishHighlight s_StylishHighlight = new StylishHighlight();
        private static List<StylishData> s_StylishDataList = new List<StylishData>();

        private static Func<string, int> AssetDatabase_GetMainAssetInstanceID;

        static ProjectViewGUI()
        {
            var bindingFlag = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var method = typeof(AssetDatabase).GetMethod("GetMainAssetInstanceID", bindingFlag);
            AssetDatabase_GetMainAssetInstanceID = method.MakeStaticFunc<Func<string, int>>();

            EditorApplication.projectWindowItemOnGUI += ProjectWindowItemOnGUI;
#if UNITY_2018_1_OR_NEWER
            EditorApplication.projectChanged += ProjectWindowChanged;
#else
            EditorApplication.projectWindowChanged += ProjectWindowChanged;
#endif
        }

        private static void ProjectWindowChanged()
        {
            s_InstanceIDTable.Clear();
            s_SubfolderTable.Clear();
            s_HierarchyLevelTable.Clear();
            s_SiblingIndexTable.Clear();
        }

        private static void ProjectWindowItemOnGUI(string guid, Rect selectionRect)
        {
            if (s_Styles == null) s_Styles = new Styles();

            if (Event.current.type == EventType.ExecuteCommand && Event.current.commandName.StartsWith(kHandleCommandName) && handleCommandEvent != null)
            {
                handleCommandEvent.Invoke();
                handleCommandEvent = null;
                return;
            }
            if (!StylishSettings.instance.general.enable) return;

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == "Assets") return;

            if (AssetDatabase.IsValidFolder(path) && path.StartsWith("Packages/") && path.Split('/').Length == 2)
                return;
            var name = Path.GetFileNameWithoutExtension(path);
            var icon = AssetDatabase.GetCachedIcon(path) as Texture2D;

            bool folder = AssetDatabase.IsValidFolder(path);
            var instanceID = s_InstanceIDTable.Get(path, GetInstanceID);
            var hierarchyLevel = s_HierarchyLevelTable.Get(path, GetHierarchyLevel);
            var siblingIndex = s_SiblingIndexTable.Get(path, GetSiblingIndex);

            if (StylishSettings.GetPathStylishBypass(path)) return;
            var projectview = InternalUtils.GetLastInteractedProjectBrowser();
            if (projectview == null) return;
            var viewmode = InternalUtils.GetProjectBrowserViewMode(projectview);

            StylishData data;
            s_StylishDataList.Clear();
            data = StylishSettings.GetHierarchyLevelStylishData(hierarchyLevel);
            if (data != null) s_StylishDataList.Add(GetAutoStylishData(data, folder));
            data = StylishSettings.GetSiblingIndexStylishData(siblingIndex);
            if (data != null) s_StylishDataList.Add(GetAutoStylishData(data, folder));
            data = StylishSettings.GetKeywordStylishData(name);
            if (data != null && folder) s_StylishDataList.Add(data);
            data = StylishSettings.GetPathStylishData(path);
            if (data != null) s_StylishDataList.Add(data);

            if (s_StylishDataList.Count == 0)
            {
                if (viewmode != 0 && IsTreeView(selectionRect))
                {
                    var foldertree = InternalUtils.GetProjectBrowserFolderTree(projectview);
                    SelectInFolderTree(selectionRect, projectview, foldertree);
                }
                return;
            }

            bool selected, focus, drop;
            if (viewmode == 0)
            {
                var assettree = InternalUtils.GetProjectBrowserAssetTree(projectview);
                if (assettree == null) return;
                selected = InternalUtils.IsTreeviewItemDragSelectedOrSelected(assettree, instanceID);
                focus = InternalUtils.TreeviewHasFocus(assettree);
                drop = InternalUtils.IsTreeviewItemDropTarget(assettree, instanceID);
                TreeviewDrawIconAndLabel(selectionRect, name, s_StylishDataList, icon, selected, focus, drop, folder);
                DrawIconOverlay(selectionRect, guid, true, true);
            }
            else if (IsTreeView(selectionRect))
            {
                var foldertree = InternalUtils.GetProjectBrowserFolderTree(projectview);
                if (foldertree == null) return;
                selected = InternalUtils.IsTreeviewItemDragSelectedOrSelected(foldertree, instanceID);
                focus = InternalUtils.TreeviewHasFocus(foldertree);
                drop = InternalUtils.IsTreeviewItemDropTarget(foldertree, instanceID);
                TreeviewDrawIconAndLabel(selectionRect, name, s_StylishDataList, icon, selected, focus, drop, folder);
                DrawIconOverlay(selectionRect, guid, true, true);
                SelectInFolderTree(selectionRect, projectview, foldertree);
            }
            else
            {
                var listarea = InternalUtils.GetProjectBrowserListArea(projectview);
                if (listarea == null) return;
                var cropped = InternalUtils.ListAreaGetCroppedLabelText(listarea, instanceID, name, selectionRect.width);
                selected = InternalUtils.IsListAreaItemDragSelectedOrSelected(listarea, instanceID);
                focus = InternalUtils.ListAreaHasFocus(listarea);
                drop = InternalUtils.IsListAreaItemDropTarget(listarea, instanceID);
                var rename = InternalUtils.ListAreaIsRenaming(listarea, instanceID);
                //if (InternalUtils.ListAreaListMode(listarea))
                if (selectionRect.height == 16)
                {
                    ListAreaDrawIconAndLabel(selectionRect, name, s_StylishDataList, icon, selected, focus, drop, rename, folder);
                    DrawIconOverlay(selectionRect, guid, true, false);
                }
                else
                {
                    if (ListAreaDrawIcon(selectionRect, cropped, s_StylishDataList, selected, focus, drop, rename, folder))
                        DrawIconOverlay(selectionRect, guid, false, false);
                }
            }
        }

        private static StylishData GetAutoStylishData(StylishData data, bool folder)
        {
            if (!folder)
            {
                s_StylishData.highlight = s_StylishHighlight;
                if (StylishSettings.instance.general.autoLevelAsset)
                    s_StylishData.highlight = data.highlight;
                data = s_StylishData;
            }
            return data;
        }

        private static bool IsTreeView(Rect rect)
        {
            return rect.height == 16 && (rect.x - 16) % 14 == 0;
        }

        private static int s_SelectInFolderTreeHash = "SelectInFolderTreeHash".GetHashCode();
        private static int s_SelectInFolderTreeLastId;

        private static void SelectInFolderTree(Rect rect, EditorWindow projectview, object treeview)
        {
            if (treeview == null) return;

            rect.xMin = 0;
            rect.width = 16;
            var cid = GUIUtility.GetControlID(s_SelectInFolderTreeHash, FocusType.Passive, rect);
            if (!rect.Contains(Event.current.mousePosition)) return;
            switch (Event.current.type)
            {
                case EventType.MouseDown:
                    s_SelectInFolderTreeLastId = cid;
                    return;
                case EventType.MouseUp:
                    if (s_SelectInFolderTreeLastId != cid) return;
                    s_SelectInFolderTreeLastId = 0;
                    break;
                default:
                    return;
            }

            bool locked = InternalUtils.GetProjectBrowserLocked(projectview);
            InternalUtils.SetProjectBrowserLocked(projectview, true);
            Utils.DelayedCall(0.05f, () => SetSelectionInFolderTree(treeview));
            Utils.DelayedCall(0.1f, () => InternalUtils.SetProjectBrowserLocked(projectview, locked));
        }

        private static void SetSelectionInFolderTree(object treeview)
        {
            var ids = InternalUtils.TreeviewGetSelection(treeview);
            Selection.objects = ids.Select(id => EditorUtility.InstanceIDToObject(id)).ToArray();
        }

        private static void DrawIcon(Rect rect, Texture2D icon, bool selected)
        {
            var color = GUI.color;
            if (selected)
                GUI.color *= new Color(0.85f, 0.9f, 1f);
            if (icon != null)
                GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);
            GUI.color = color;
        }

        private static void DrawIconOverlay(Rect rect, string guid, bool small, bool tree)
        {
            if (Event.current.rawType != EventType.Repaint) return;

            if (small)
            {
                float num = !Provider.isActive ? 0f : 14f;
                if (!tree)
                    rect.xMin += s_Styles.label.margin.left;
                rect.width = 16f;
                rect.x += num * 0.5f;
            }
            else
            {
                rect.height -= s_Styles.gridLabel.fixedHeight;
            }

            if (InternalUtils.CollaborationIsEnabled())
                InternalUtils.CollaborationIconOverlay(guid, rect, small, tree);
            else if (Provider.isActive)
                InternalUtils.VersionControlIconOverlay(guid, rect);
        }

        private static bool ListAreaDrawIcon(Rect rect, string label, List<StylishData> datas, bool selected, bool focus, bool drop, bool rename, bool folder)
        {
            if (Event.current.rawType != EventType.Repaint) return false;
            bool iconSelected = selected;
            if (datas.Any(d => d.highlight.mode != StylishHighlight.Mode.None) && StylishSettings.instance.general.hideSelection) selected = false;

            var clearRect = new Rect(rect);
            clearRect.height -= s_Styles.gridLabel.fixedHeight;
            bool clear = false;
            if (folder && datas.Any(d => d.icon.mode == StylishIconOverride.Mode.Replace))
                clear = true;
            if (clear)
                s_Styles.background.Draw(clearRect, string.Empty, false, false, false, false);

            var iconRect = new Rect(rect);
            iconRect.height = iconRect.width;
            var center = iconRect.center;
            if (iconRect.width > 64) iconRect.width = 64;
            iconRect.height = iconRect.width;
            iconRect.center = center;

            int index = 0;
            if (folder)
                for (int i = 0; i < datas.Count; i++)
                    if (datas[i].icon.mode == StylishIconOverride.Mode.Replace) index = i;
            for (int i = index; i < datas.Count; i++)
                if (datas[i].icon.mode != StylishIconOverride.Mode.None)
                    datas[i].icon.Draw(iconRect, false, iconSelected);

            var labelRect = new Rect(rect.x, rect.yMax + 1f - s_Styles.gridLabel.fixedHeight, rect.width - 1f, s_Styles.gridLabel.fixedHeight);
            var dropRect = new Rect(labelRect.x - 10f, labelRect.y, labelRect.width + 20f, labelRect.height);
            var highlightRect = new Rect(dropRect.x + 3f, dropRect.y - 1, dropRect.width - 6f, dropRect.height + 3);

            if (!rename)
                s_Styles.background.Draw(highlightRect, string.Empty, false, false, false, false);

            index = 0;
            for (int i = 0; i < datas.Count; i++)
                if (datas[i].highlight.clearOther) index = i;
            if (!rename)
                for (int i = index; i < datas.Count; i++)
                    datas[i].highlight.Draw(highlightRect);

            if (drop)
                s_Styles.label.Draw(dropRect, GUIContent.none, true, true, false, false);

            var textColor = s_Styles.textColor;
            var fontStyle = s_Styles.fontStyle;
            for (int i = datas.Count - 1; i >= 0; i--)
                if (datas[i].highlight.overrideText)
                {
                    textColor = datas[i].highlight.textColor;
                    fontStyle = datas[i].highlight.fontStyle;
                    break;
                }
            s_Styles.gridLabel.normal.textColor = textColor;
            s_Styles.gridLabel.focused.textColor = textColor;
            s_Styles.gridLabel.fontStyle = fontStyle;
            if (!rename)
                s_Styles.gridLabel.Draw(labelRect, label, false, false, selected, focus);

            return clear;
        }

        private static void ListAreaDrawIconAndLabel(Rect rect, string label, List<StylishData> datas, Texture2D icon, bool selected, bool focus, bool drop, bool rename, bool folder)
        {
            if (Event.current.rawType != EventType.Repaint) return;
            if (datas.Any(d => d.highlight.mode != StylishHighlight.Mode.None) && StylishSettings.instance.general.hideSelection) selected = false;

            float num = !Provider.isActive ? 0f : 14f;
            rect.xMin += s_Styles.label.margin.left;
            var clearRect = new Rect(rect);
            if (rename)
                clearRect.width = num + 16f;
            s_Styles.background.Draw(clearRect, string.Empty, false, false, false, false);

            int index = 0;
            for (int i = 0; i < datas.Count; i++)
                if (datas[i].highlight.clearOther) index = i;
            if (!rename)
                for (int i = index; i < datas.Count; i++)
                    datas[i].highlight.Draw(rect);

            if (drop)
            {
                GUI.BeginClip(rect);
                var dropRect = new Rect(Vector2.zero, rect.size);
                dropRect.xMin = -rect.xMin;
                s_Styles.label.Draw(dropRect, GUIContent.none, true, true, false, false);
                GUI.EndClip();
            }

            var textColor = s_Styles.textColor;
            var fontStyle = s_Styles.fontStyle;
            for (int i = datas.Count - 1; i >= 0; i--)
                if (datas[i].highlight.overrideText)
                {
                    textColor = datas[i].highlight.textColor;
                    fontStyle = datas[i].highlight.fontStyle;
                    break;
                }
            s_Styles.label.padding.left = (int)(num + 16f + 2f);
            s_Styles.label.normal.textColor = textColor;
            s_Styles.label.focused.textColor = textColor;
            s_Styles.label.fontStyle = fontStyle;
            if (!rename)
                s_Styles.label.Draw(rect, label, false, false, selected, focus);

            rect.width = 16f;
            rect.x += num * 0.5f;
            if (!folder || !datas.Any(d => d.icon.mode == StylishIconOverride.Mode.Replace))
                DrawIcon(rect, icon, selected);
            index = 0;
            if (folder)
                for (int i = 0; i < datas.Count; i++)
                    if (datas[i].icon.mode == StylishIconOverride.Mode.Replace) index = i;
            for (int i = index; i < datas.Count; i++)
                if (datas[i].icon.mode != StylishIconOverride.Mode.None)
                    datas[i].icon.Draw(rect, true, selected);
        }

        private static void TreeviewDrawIconAndLabel(Rect rect, string label, List<StylishData> datas, Texture2D icon, bool selected, bool focus, bool drop, bool folder)
        {
            if (Event.current.rawType != EventType.Repaint) return;
            if (datas.Any(d => d.highlight.mode != StylishHighlight.Mode.None) && StylishSettings.instance.general.hideSelection) selected = false;

            s_Styles.background.Draw(rect, string.Empty, false, false, false, false);

            int index = 0;
            for (int i = 0; i < datas.Count; i++)
                if (datas[i].highlight.clearOther) index = i;
            for (int i = index; i < datas.Count; i++)
                datas[i].highlight.Draw(rect);

            if (drop)
            {
                GUI.BeginClip(rect);
                var dropRect = new Rect(Vector2.zero, rect.size);
                dropRect.xMin = -rect.xMin;
                s_Styles.label.Draw(dropRect, GUIContent.none, true, true, false, false);
                GUI.EndClip();
            }

            var textColor = s_Styles.textColor;
            var fontStyle = s_Styles.fontStyle;
            for (int i = datas.Count - 1; i >= 0; i--)
                if (datas[i].highlight.overrideText)
                {
                    textColor = datas[i].highlight.textColor;
                    fontStyle = datas[i].highlight.fontStyle;
                    break;
                }
            float num = !Provider.isActive ? 0f : 14f;
            s_Styles.label.padding.left = (int)(num + 16f + 2f);
            s_Styles.label.normal.textColor = textColor;
            s_Styles.label.focused.textColor = textColor;
            s_Styles.label.fontStyle = fontStyle;
            s_Styles.label.Draw(rect, label, false, false, selected, focus);

            rect.width = 16f;
            rect.x += num * 0.5f;
            if (!folder || !datas.Any(d => d.icon.mode == StylishIconOverride.Mode.Replace))
                DrawIcon(rect, icon, selected);
            index = 0;
            if (folder)
                for (int i = 0; i < datas.Count; i++)
                    if (datas[i].icon.mode == StylishIconOverride.Mode.Replace) index = i;
            for (int i = index; i < datas.Count; i++)
                if (datas[i].icon.mode != StylishIconOverride.Mode.None)
                    datas[i].icon.Draw(rect, true, selected);
        }

        private static int GetInstanceID(string path)
        {
            //var obj = AssetDatabase.LoadMainAssetAtPath(path);
            //if (obj == null) return 0;
            //return obj.GetInstanceID();
            return AssetDatabase_GetMainAssetInstanceID(path);
        }

        private static string[] GetSubfolders(string path)
        {
            var subfolders = AssetDatabase.GetSubFolders(path);
            subfolders = subfolders.Select(f => Path.GetFileNameWithoutExtension(f)).OrderBy(f => f).ToArray();
            return subfolders;
        }

        private static int GetHierarchyLevel(string path)
        {
            return path.Count(c => c == '/') - 1;
        }

        private static int GetSiblingIndex(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            int index = path.LastIndexOf("/");
            if (index < 0) return -1;
            path = path.Substring(0, index);
            var subfolders = s_SubfolderTable.Get(path, GetSubfolders);
            return Array.IndexOf(subfolders, name);
        }

        private static TValue Get<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, Func<TKey, TValue> func)
        {
            TValue result;
            if (dict.TryGetValue(key, out result))
                return result;
            result = func(key);
            dict[key] = result;
            return result;
        }
    }
}