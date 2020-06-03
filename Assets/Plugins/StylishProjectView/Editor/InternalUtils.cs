using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace StylishProjectView
{
    public static class InternalUtils
    {
        private static Func<object, object> ProjectBrowser_LastInteractedProjectBrowser;
        private static Func<object, object> ProjectBrowser_AssetTree;
        private static Func<object, object> ProjectBrowser_FolderTree;
        private static Func<object, object> ProjectBrowser_ListArea;
        private static Func<object, object> ProjectBrowser_ViewMode;
        private static Func<object, object> ProjectBrowser_IsLocked_Getter;
        private static Action<object, object> ProjectBrowser_IsLocked_Setter;
        private static Func<object, bool> ProjectBrowser_IsLocked_Getter2;
        private static Action<object, bool> ProjectBrowser_IsLocked_Setter2;

        private static Func<object, object> ObjectListArea_LocalAssets;
        private static Func<object, object> ObjectListArea_DragSelection;
        private static Func<object, bool> ObjectListArea_AllowDragging;
        private static Func<object, object, bool> ObjectListArea_IsSelected;
        private static Func<object, bool> ObjectListArea_HasFocus;
        private static Func<object, bool> ObjectListArea_ListMode;
        private static Func<object, object, bool> ObjectListArea_IsExpanded;
        private static Func<object, object, bool> ObjectListArea_IsRenaming;
        private static Func<object, object> ObjectListArea_DropTargetControlID;
        private static Func<object, List<int>> ObjectListArea_GetInstanceIDs;
        private static Func<object, int, string, float, string> ObjectListArea_GetCroppedLabelText;

        private static Func<object, object> TreeViewController_DragSelection;
        private static Func<object, object, bool> TreeViewController_IsSelected;
        private static Func<object, bool> TreeViewController_HasFocus;
        private static Func<object, int[]> TreeViewController_GetSelection;
        private static Func<object, object> TreeViewController_GetDragging;
        private static Func<object, int[]> TreeViewController_GetRowIDs;
        private static Func<object, int> TreeViewDragging_GetDropTargetControlID;

        private static Action<string, Rect> VersionControl_OnProjectWindowItem;
        private static Func<object> Collaboration_GetCollabAccessInstance;
        private static Func<object, bool> Collaboration_IsServiceEnabled;
        private static Action<string, Rect> Collaboration_OnProjectWindowItemIconOverlay;
        private static Action<Rect, string, bool> Collaboration_OnProjectWindowIconOverlay;
        private static Action<Rect, string> Collaboration_OnProjectBrowserNavPanelIconOverlay;

        private static Func<string, Rect, Gradient, Gradient> EditorGUI_GradientField;
        private static Func<Rect, string, Gradient, Gradient> EditorGUI_GradientField2;
        private static Func<Gradient, Texture2D> GradientPreviewCache_GetGradientPreview;
        private static Action GradientPreviewCache_ClearCache;

        static InternalUtils()
        {
            var bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            var type = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
            var field = type.GetField("s_LastInteractedProjectBrowser", bf);
            ProjectBrowser_LastInteractedProjectBrowser = field.MakeGetter();
            field = type.GetField("m_AssetTree", bf);
            ProjectBrowser_AssetTree = field.MakeGetter();
            field = type.GetField("m_FolderTree", bf);
            ProjectBrowser_FolderTree = field.MakeGetter();
            field = type.GetField("m_ListArea", bf);
            ProjectBrowser_ListArea = field.MakeGetter();
            field = type.GetField("m_ViewMode", bf);
            ProjectBrowser_ViewMode = field.MakeGetter();
            field = type.GetField("m_IsLocked", bf);
            if (field != null)
            {
                ProjectBrowser_IsLocked_Getter = field.MakeGetter();
                ProjectBrowser_IsLocked_Setter = field.MakeSetter();
            }
            else
            {
                var prop = type.GetProperty("isLocked", bf).GetGetMethod(true);
                ProjectBrowser_IsLocked_Getter2 = prop.MakeFuncGenericThis<Func<object, bool>>();
                prop = type.GetProperty("isLocked", bf).GetSetMethod(true);
                ProjectBrowser_IsLocked_Setter2 = prop.MakeFuncGenericThisInput1<Action<object, bool>>();
            }

            type = typeof(Editor).Assembly.GetType("UnityEditor.ObjectListArea");
            field = type.GetField("m_LocalAssets", bf);
            ObjectListArea_LocalAssets = field.MakeGetter();
            var method = type.GetProperty("allowDragging", bf).GetGetMethod();
            ObjectListArea_AllowDragging = method.MakeFuncGenericThis<Func<object, bool>>();
            method = type.GetMethod("IsSelected", bf);
            ObjectListArea_IsSelected = method.MakeFuncGenericInput<Func<object, object, bool>>();
            method = type.GetMethod("HasFocus", bf);
            ObjectListArea_HasFocus = method.MakeFuncGenericThis<Func<object, bool>>();
            method = type.GetMethod("GetCroppedLabelText", bf);
            ObjectListArea_GetCroppedLabelText = method.MakeFuncGenericThisInput3<Func<object, int, string, float, string>>();

            type = typeof(Editor).Assembly.GetType("UnityEditor.ObjectListArea+LocalGroup");
            field = type.GetField("m_DragSelection", bf);
            ObjectListArea_DragSelection = field.MakeGetter();
            method = type.GetProperty("ListMode", bf).GetGetMethod();
            ObjectListArea_ListMode = method.MakeFuncGenericThis<Func<object, bool>>();
            method = type.GetMethod("IsExpanded", bf);
            ObjectListArea_IsExpanded = method.MakeFuncGenericInput<Func<object, object, bool>>();
            method = type.GetMethod("IsRenaming", bf);
            ObjectListArea_IsRenaming = method.MakeFuncGenericInput<Func<object, object, bool>>();
            field = type.GetField("m_DropTargetControlID", bf);
            ObjectListArea_DropTargetControlID = field.MakeGetter();
            method = type.GetMethod("GetInstanceIDs", bf);
            ObjectListArea_GetInstanceIDs = method.MakeFuncGenericThis<Func<object, List<int>>>();

            type = typeof(Editor).Assembly.GetType("UnityEditor.IMGUI.Controls.TreeViewController");
            field = type.GetField("m_DragSelection", bf);
            TreeViewController_DragSelection = field.MakeGetter();
            method = type.GetMethod("IsSelected", bf);
            TreeViewController_IsSelected = method.MakeFuncGenericInput<Func<object, object, bool>>();
            method = type.GetMethod("HasFocus", bf);
            TreeViewController_HasFocus = method.MakeFuncGenericThis<Func<object, bool>>();
            method = type.GetMethod("GetSelection", bf);
            TreeViewController_GetSelection = method.MakeFuncGenericThis<Func<object, int[]>>();
            method = type.GetProperty("dragging", bf).GetGetMethod();
            TreeViewController_GetDragging = method.MakeFuncGenericThis<Func<object, object>>();
            method = type.GetMethod("GetRowIDs", bf);
            TreeViewController_GetRowIDs = method.MakeFuncGenericThis<Func<object, int[]>>();

            type = typeof(Editor).Assembly.GetType("UnityEditor.IMGUI.Controls.ITreeViewDragging");
            method = type.GetMethod("GetDropTargetControlID", bf);
            TreeViewDragging_GetDropTargetControlID = method.MakeFuncGenericThis<Func<object, int>>();

            type = typeof(Editor).Assembly.GetType("UnityEditorInternal.VersionControl.ProjectHooks");
            method = type.GetMethod("OnProjectWindowItem", bf);
            VersionControl_OnProjectWindowItem = method.MakeStaticFunc<Action<string, Rect>>();

            type = typeof(Editor).Assembly.GetType("UnityEditor.Web.CollabAccess");
            method = type.GetProperty("Instance", bf).GetGetMethod();
            Collaboration_GetCollabAccessInstance = method.MakeStaticFunc<Func<object>>();
            method = type.GetMethod("IsServiceEnabled", bf);
            Collaboration_IsServiceEnabled = method.MakeFuncGenericThis<Func<object, bool>>();
            type = typeof(Editor).Assembly.GetType("UnityEditor.Collaboration.CollabProjectHook");
            method = type.GetMethod("OnProjectWindowItemIconOverlay", bf);
            if (method != null)
                Collaboration_OnProjectWindowItemIconOverlay = method.MakeStaticFunc<Action<string, Rect>>();
            method = type.GetMethod("OnProjectWindowIconOverlay", bf);
            if (method != null)
                Collaboration_OnProjectWindowIconOverlay = method.MakeStaticFunc<Action<Rect, string, bool>>();
            method = type.GetMethod("OnProjectBrowserNavPanelIconOverlay", bf);
            if (method != null)
                Collaboration_OnProjectBrowserNavPanelIconOverlay = method.MakeStaticFunc<Action<Rect, string>>();

            method = typeof(EditorGUI).GetMethod("GradientField", bf, null, new Type[] { typeof(string), typeof(Rect), typeof(Gradient) }, null);
            if (method != null)
                EditorGUI_GradientField = method.MakeStaticFunc<Func<string, Rect, Gradient, Gradient>>();
            else
            {
                EditorGUI_GradientField = null;
                method = typeof(EditorGUI).GetMethod("GradientField", bf, null, new Type[] { typeof(Rect), typeof(string), typeof(Gradient) }, null);
                EditorGUI_GradientField2 = method.MakeStaticFunc<Func<Rect, string, Gradient, Gradient>>();
            }

            type = typeof(Editor).Assembly.GetType("UnityEditorInternal.GradientPreviewCache");
            method = type.GetMethod("GetGradientPreview", bf);
            GradientPreviewCache_GetGradientPreview = method.MakeStaticFunc<Func<Gradient, Texture2D>>();
            method = type.GetMethod("ClearCache", bf);
            GradientPreviewCache_ClearCache = method.MakeStaticFunc<Action>();
        }

        public static EditorWindow GetLastInteractedProjectBrowser()
        {
            return (EditorWindow)ProjectBrowser_LastInteractedProjectBrowser(null);
        }

        public static object GetProjectBrowserAssetTree(object projectBrowser)
        {
            return ProjectBrowser_AssetTree(projectBrowser);
        }

        public static object GetProjectBrowserFolderTree(object projectBrowser)
        {
            return ProjectBrowser_FolderTree(projectBrowser);
        }

        public static object GetProjectBrowserListArea(object projectBrowser)
        {
            return ProjectBrowser_ListArea(projectBrowser);
        }

        public static int GetProjectBrowserViewMode(object projectBrowser)
        {
            return (int)ProjectBrowser_ViewMode(projectBrowser);
        }

        public static bool GetProjectBrowserLocked(object projectBrowser)
        {
            if (ProjectBrowser_IsLocked_Getter == null)
                return ProjectBrowser_IsLocked_Getter2(projectBrowser);
            return (bool)ProjectBrowser_IsLocked_Getter(projectBrowser);
        }

        public static void SetProjectBrowserLocked(object projectBrowser, bool locked)
        {
            if (ProjectBrowser_IsLocked_Setter == null)
                ProjectBrowser_IsLocked_Setter2(projectBrowser, locked);
            else
                ProjectBrowser_IsLocked_Setter(projectBrowser, locked);
        }

        public static bool TreeviewHasFocus(object treeview)
        {
            return TreeViewController_HasFocus(treeview);
        }

        public static int[] TreeviewGetSelection(object treeview)
        {
            return TreeViewController_GetSelection(treeview);
        }

        public static int[] TreeviewGetRowIDs(object treeview)
        {
            return TreeViewController_GetRowIDs(treeview);
        }

        public static bool IsTreeviewItemDragSelectedOrSelected(object treeview, int id)
        {
            var drags = (List<int>)TreeViewController_DragSelection(treeview);
            var selected = TreeViewController_IsSelected(treeview, id);
            return drags.Count <= 0 ? selected : drags.Contains(id);
        }

        public static bool IsTreeviewItemDropTarget(object treeview, int id)
        {
            var dragging = TreeViewController_GetDragging(treeview);
            var dropTarget = TreeViewDragging_GetDropTargetControlID(dragging);
            return dropTarget == id + 10000000;
        }

        public static bool ListAreaListMode(object listarea)
        {
            var group = ObjectListArea_LocalAssets(listarea);
            return ObjectListArea_ListMode(group);
        }

        public static bool ListAreaIsExpanded(object listarea, int id)
        {
            var group = ObjectListArea_LocalAssets(listarea);
            return ObjectListArea_IsExpanded(group, id);
        }

        public static bool ListAreaIsRenaming(object listarea, int id)
        {
            var group = ObjectListArea_LocalAssets(listarea);
            return ObjectListArea_IsRenaming(group, id);
        }

        public static bool ListAreaHasFocus(object listarea)
        {
            return ObjectListArea_HasFocus(listarea);
        }

        public static bool IsListAreaItemDragSelectedOrSelected(object listarea, int id)
        {
            var group = ObjectListArea_LocalAssets(listarea);
            var drags = (List<int>)ObjectListArea_DragSelection(group);
            var selected = ObjectListArea_IsSelected(listarea, id);
            var allow = ObjectListArea_AllowDragging(listarea);
            if (allow)
                return drags.Count <= 0 ? selected : drags.Contains(id);
            else
                return selected;
        }

        public static bool IsListAreaItemDropTarget(object listarea, int id)
        {
            var group = ObjectListArea_LocalAssets(listarea);
            var dropTarget = (int)ObjectListArea_DropTargetControlID(group);
            return dropTarget == id + 100000000;
        }

        public static string ListAreaGetCroppedLabelText(object listarea, int id, string text, float width)
        {
            return ObjectListArea_GetCroppedLabelText(listarea, id, text, width);
        }

        public static List<int> ListAreaGetInstanceIDs(object listarea)
        {
            var group = ObjectListArea_LocalAssets(listarea);
            return ObjectListArea_GetInstanceIDs(group);
        }

        public static void VersionControlIconOverlay(string guid, Rect position)
        {
            VersionControl_OnProjectWindowItem(guid, position);
        }

        public static bool CollaborationIsEnabled()
        {
            var instance = Collaboration_GetCollabAccessInstance();
            return Collaboration_IsServiceEnabled(instance);
        }

        public static void CollaborationIconOverlay(string guid, Rect position, bool small, bool tree)
        {
            if (Collaboration_OnProjectWindowItemIconOverlay != null)
                Collaboration_OnProjectWindowItemIconOverlay.Invoke(guid, position);
            else
            {
                if (tree && Collaboration_OnProjectBrowserNavPanelIconOverlay != null)
                    Collaboration_OnProjectBrowserNavPanelIconOverlay(position, guid);
                if (!tree && Collaboration_OnProjectWindowIconOverlay != null)
                    Collaboration_OnProjectWindowIconOverlay(position, guid, small);
            }
        }

        public static Gradient EditorGUIGradientField(Rect position, string label, Gradient gradient)
        {
            if (EditorGUI_GradientField != null)
                return EditorGUI_GradientField(label, position, gradient);
            else
                return EditorGUI_GradientField2(position, label, gradient);
        }

        public static Texture2D GetGradientPreview(Gradient gradient)
        {
            return GradientPreviewCache_GetGradientPreview(gradient);
        }

        public static void GradientClearCache()
        {
            GradientPreviewCache_ClearCache();
        }
    }
}