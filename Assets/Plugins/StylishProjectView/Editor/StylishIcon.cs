using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace StylishProjectView
{
    [Serializable]
    public class TextureLayer
    {
        public bool useResource = false;
        public string resource = string.Empty;
        public Texture2D textures;
        public Color tints = Color.white;
        public Vector2 offset = Vector2.zero;
        public Vector2 size = Vector2.one;
    }

    [Serializable]
    public class StylishIcon : ScriptableObject
    {
        public List<TextureLayer> normal = new List<TextureLayer>();
        public List<TextureLayer> small = new List<TextureLayer>();

        private static Color s_SelectedOverlay = new Color(0.85f, 0.9f, 1f);

        public static StylishIcon Create()
        {
            var icon = CreateInstance<StylishIcon>();
            icon.name = "StylishIcon";
            icon.normal = new List<TextureLayer>();
            icon.small = new List<TextureLayer>();
            StylishSettings.AddSubObject(icon);
            return icon;
        }

        public void Destory()
        {
            DestroyImmediate(this, true);
        }

        public void Draw(Rect position, bool small, bool selected)
        {
            var color = GUI.color;
            var layers = small ? this.small : this.normal;
            if (layers.Count == 0)
                layers = this.normal;
            Rect dest = new Rect();
            GUI.BeginClip(position);
            foreach (var layer in layers)
            {
                var icon = layer.useResource && layer.resource != string.Empty ? layer.resource.GetResource() : layer.textures;
                if (icon == null) continue;

                GUI.color = layer.tints;
                if (selected) GUI.color *= s_SelectedOverlay;

                dest.x = layer.offset.x * position.width;
                dest.y = layer.offset.y * position.height;
                dest.width = layer.size.x * position.width;
                dest.height = layer.size.y * position.height;
                GUI.DrawTexture(dest, icon);
            }
            GUI.EndClip();
            GUI.color = color;
        }

        public void DrawPreview(Rect position)
        {
            var rect = new Rect(position);
            rect.yMin += 2;
            rect.yMax -= 2;
            rect.x += 4;
            rect.width = 64;
            Draw(rect, false, false);
            rect.x += rect.width + 8;
            rect.y += 16;
            rect.width = rect.height = 16;
            Draw(rect, true, false);
        }
    }

    [Serializable]
    public class StylishIconOverride
    {
        public enum Mode { None, Replace, Overlay }

        public Mode mode = Mode.None;
        public ColorBlend.Mode blend = ColorBlend.Mode.None;
        public StylishIcon icon;
        public Color[] normalTints;
        public Color[] smallTints;

        public static GUIStyle s_Dropdown;
        private static Color s_SelectedOverlay = new Color(0.85f, 0.9f, 1f);

        public float GetConfigHeight(IEnumerable<StylishIconOverride> targets = null)
        {
            if (targets == null) targets = this.Yield();
            bool mix = targets.Select(i => i.mode).Distinct().Count() > 1;
            float height = !mix && mode != Mode.None ? 68 : EditorGUIUtility.singleLineHeight;
            return height;
        }

        public void DrawConfig(IEnumerable<StylishIconOverride> targets = null)
        {
            var rect = EditorGUILayout.GetControlRect(false, GetConfigHeight(targets));
            DrawConfig(rect, targets);
        }

        public void DrawConfig(Rect position, IEnumerable<StylishIconOverride> targets = null)
        {
            if (targets == null) targets = this.Yield();
            if (s_Dropdown == null)
            {
                var ver = Application.unityVersion.Split('.');
                int major = 0, minor = 0;
                int.TryParse(ver[0], out major);
                int.TryParse(ver[1], out minor);
                if (major > 2018 || (major == 2018 && minor >= 3))
                {
                    s_Dropdown = new GUIStyle("LargeButton");
                }
                else
                {
                    s_Dropdown = new GUIStyle("LargeDropDown");
                    s_Dropdown.border = new RectOffset(6, 15, 15, 3);
                    s_Dropdown.fixedHeight = 0;
                }
            }

            EditorGUI.BeginChangeCheck();
            var rect = new Rect(position);
            rect.height = EditorGUIUtility.singleLineHeight;

            rect.width = 30;
            GUI.Label(rect, "Icon");
            rect.x += rect.width + 8;
            rect.width = 60;
            bool mix = targets.Select(t => t.mode).Distinct().Count() > 1;
            EditorGUI.showMixedValue = mix;
            EditorGUI.BeginChangeCheck();
            mode = (Mode)EditorGUI.EnumPopup(rect, mode);
            if (EditorGUI.EndChangeCheck())
                foreach (var target in targets)
                    target.mode = mode;

            if (!mix && mode != Mode.None)
            {
                if (icon != null)
                {
                    rect.x += rect.width + 8;
                    rect.width = 30;
                    GUI.Label(rect, "Tints");
                    rect.x += rect.width + 8;
                    rect.width = 100;
                    EditorGUI.showMixedValue = targets.Select(t => t.blend).Distinct().Count() > 1;
                    EditorGUI.BeginChangeCheck();
                    blend = (ColorBlend.Mode)EditorGUI.EnumPopup(rect, blend);
                    if (EditorGUI.EndChangeCheck())
                        foreach (var target in targets)
                            target.blend = blend;
                    rect.y += rect.height + 2;

                    rect.xMin = position.xMin;
                    if (!EditorGUI.showMixedValue && blend != ColorBlend.Mode.None)
                    {
                        var width = rect.width;

                        var count = targets.Select(t => t.normalTints.Length).Min();
                        rect.width = (width - (count - 1) * 2) / count;
                        rect.width = Mathf.Min(rect.width, 40);
                        for (int i = 0; i < count; i++)
                        {
                            EditorGUI.showMixedValue = targets.Select(t => t.normalTints[i]).Distinct().Count() > 1;
                            EditorGUI.BeginChangeCheck();
#if UNITY_2018_1_OR_NEWER
                            normalTints[i] = EditorGUI.ColorField(rect, GUIContent.none, normalTints[i], false, true, false);
#else
                            normalTints[i] = EditorGUI.ColorField(rect, GUIContent.none, normalTints[i], false, true, false, null);
#endif
                            if (EditorGUI.EndChangeCheck())
                                foreach (var target in targets)
                                    target.normalTints[i] = normalTints[i];
                            rect.x += rect.width + 2;
                        }
                        rect.y += rect.height + 2;
                        rect.xMin = position.xMin;

                        count = targets.Select(t => t.smallTints.Length).Min();
                        rect.width = (width - (count - 1) * 2) / count;
                        rect.width = Mathf.Min(rect.width, 40);
                        for (int i = 0; i < count; i++)
                        {
                            EditorGUI.showMixedValue = targets.Select(t => t.smallTints[i]).Distinct().Count() > 1;
                            EditorGUI.BeginChangeCheck();
#if UNITY_2018_1_OR_NEWER
                            smallTints[i] = EditorGUI.ColorField(rect, GUIContent.none, smallTints[i], false, true, false);
#else
                            smallTints[i] = EditorGUI.ColorField(rect, GUIContent.none, smallTints[i], false, true, false, null);
#endif
                            if (EditorGUI.EndChangeCheck())
                                foreach (var target in targets)
                                    target.smallTints[i] = smallTints[i];
                            rect.x += rect.width + 2;
                        }
                    }
                }
            }
            if (EditorGUI.EndChangeCheck())
                StylishSettings.Save();

            if (!mix && mode != Mode.None)
            {
                int id = GUIUtility.GetControlID(FocusType.Passive, rect);
                rect = new Rect(position);
                rect.xMin = position.xMax - 100;
                EditorGUI.showMixedValue = targets.Select(t => t.icon).Distinct().Count() > 1;
                if (EditorGUI.DropdownButton(rect, GUIContent.none, FocusType.Passive, s_Dropdown))
                    if (StylishSettings.instance.stylishIconList.Count > 0)
                        StylishIconSelector.Popup(rect, id);
                if (StylishIconSelector.IsSelected(id))
                {
                    foreach (var target in targets)
                    {
                        target.icon = StylishIconSelector.GetSelected();
                        target.normalTints = Enumerable.Repeat(Color.white, target.icon.normal.Count).ToArray();
                        target.smallTints = Enumerable.Repeat(Color.white, target.icon.small.Count).ToArray();
                    }
                    StylishSettings.Save();
                }
                if (!EditorGUI.showMixedValue)
                    if (icon != null) DrawPreview(rect);
            }
            EditorGUI.showMixedValue = false;
        }

        public void Draw(Rect position, bool small, bool selected)
        {
            if (icon == null) return;

            var color = GUI.color;
            var layers = small ? icon.small : icon.normal;
            var tints = small ? smallTints : normalTints;
            if (layers.Count == 0) tints = normalTints;
            if (layers.Count == 0) layers = icon.normal;

            int index = 0;
            Rect dest = new Rect();
            GUI.BeginClip(position);
            foreach (var layer in layers)
            {
                var tex = layer.useResource && layer.resource != string.Empty ? layer.resource.GetResource() : layer.textures;
                if (tex == null) continue;

                if (index >= tints.Length)
                {
                    normalTints = Enumerable.Repeat(Color.white, icon.normal.Count).ToArray();
                    smallTints = Enumerable.Repeat(Color.white, icon.small.Count).ToArray();
                    tints = small ? smallTints : normalTints;
                }
                GUI.color = layer.tints.Blend(tints[index], blend);
                if (selected) GUI.color *= s_SelectedOverlay;

                dest.x = layer.offset.x * position.width;
                dest.y = layer.offset.y * position.height;
                dest.width = layer.size.x * position.width;
                dest.height = layer.size.y * position.height;
                GUI.DrawTexture(dest, tex);
                index++;
            }
            GUI.EndClip();
            GUI.color = color;
        }

        public void DrawPreview(Rect position)
        {
            if (icon == null) return;

            var rect = new Rect(position);
            rect.yMin += 2;
            rect.yMax -= 2;
            rect.x += 4;
            rect.width = 64;
            Draw(rect, false, false);
            rect.x += rect.width + 8;
            rect.y += 16;
            rect.width = rect.height = 16;
            Draw(rect, true, false);
        }
    }

    public class StylishIconCreator : PopupWindowContent
    {
        private const float kMinHeight = 200;

        private StylishIcon m_StylishIcon;
        private float m_Height = kMinHeight;

        private static int s_ToolbarIndex = 0;
        private static string[] s_ToolbarTitle = new[] { "Normal", "Small" };
        private static GUIContent[] s_TextureLayerButtons;

        public static void Popup(Rect position, StylishIcon icon)
        {
            if (s_TextureLayerButtons == null)
            {
                s_TextureLayerButtons = new GUIContent[2];
                s_TextureLayerButtons[0] = Utils.CreateContent(image: "ol plus");
                s_TextureLayerButtons[1] = Utils.CreateContent(image: "ol minus");
            }

            var popup = new StylishIconCreator();
            popup.m_StylishIcon = icon;
            PopupWindow.Show(position, popup);
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(300, m_Height);
        }

        public override void OnGUI(Rect rect)
        {
            if (m_StylishIcon.normal == null) m_StylishIcon.normal = new List<TextureLayer>();
            if (m_StylishIcon.small == null) m_StylishIcon.small = new List<TextureLayer>();

            var wholeRect = EditorGUILayout.BeginVertical();

            s_ToolbarIndex = GUILayout.Toolbar(s_ToolbarIndex, s_ToolbarTitle);
            if (s_ToolbarIndex == 0)
                EditorGUILayout.HelpBox("Normal icon is used in thumbnail view.", MessageType.None);
            else
                EditorGUILayout.HelpBox("Small icon is used in treeview and listview.", MessageType.None);

            var layers = s_ToolbarIndex == 0 ? m_StylishIcon.normal : m_StylishIcon.small;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Texture Layers", GUILayout.Width(100));
            var button = GUILayout.Toolbar(-1, s_TextureLayerButtons, EditorStyles.miniButton, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            if (button == 0) layers.Add(new TextureLayer());
            if (button == 1) layers.RemoveAt(layers.Count - 1);

            int index = 0, add = -1, remove = -1;
            foreach (var layer in layers)
            {
                using (new GUIColor(0.3f))
                    EditorGUILayout.BeginVertical("ShurikenEffectBg");

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Layer " + index);
                GUILayout.FlexibleSpace();
                layer.useResource = GUILayout.Toggle(layer.useResource, "Use Internal Resource");
                GUILayout.FlexibleSpace();
                button = GUILayout.Toolbar(-1, s_TextureLayerButtons, EditorStyles.miniButton, GUILayout.Width(50));
                if (button == 0) add = index;
                if (button == 1) remove = index;
                EditorGUILayout.EndHorizontal();
                index++;

                EditorGUIUtility.labelWidth = 63;
                EditorGUILayout.BeginHorizontal();
                if (layer.useResource)
                    layer.resource = EditorGUILayout.DelayedTextField("Reource", layer.resource);
                else
                    layer.textures = (Texture2D)EditorGUILayout.ObjectField(layer.textures, typeof(Texture2D), false);
#if UNITY_2018_1_OR_NEWER
                layer.tints = EditorGUILayout.ColorField(GUIContent.none, layer.tints, false, true, false, GUILayout.Width(27));
#else
                layer.tints = EditorGUILayout.ColorField(GUIContent.none, layer.tints, false, true, false, null, GUILayout.Width(27));
#endif
                EditorGUILayout.EndHorizontal();

                EditorGUIUtility.labelWidth = 12;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Offset:");
                layer.offset.x = EditorGUILayout.FloatField("X", layer.offset.x, GUILayout.Width(45));
                layer.offset.y = EditorGUILayout.FloatField("Y", layer.offset.y, GUILayout.Width(45));
                GUILayout.Label("Size:");
                layer.size.x = EditorGUILayout.FloatField("X", layer.size.x, GUILayout.Width(45));
                layer.size.y = EditorGUILayout.FloatField("Y", layer.size.y, GUILayout.Width(45));
                EditorGUILayout.EndHorizontal();
                EditorGUIUtility.labelWidth = 0;

                EditorGUILayout.EndVertical();
            }
            GUILayout.Space(8);

            if (add != -1) layers.Insert(add, new TextureLayer());
            if (remove != -1) layers.RemoveAt(remove);
            if (EditorGUI.EndChangeCheck() || add != -1 || remove != -1)
                StylishSettings.Save();

            var size = s_ToolbarIndex == 0 ? 64 : 16;
            var iconRect = EditorGUILayout.GetControlRect(false, size);
            iconRect.width = size;
            m_StylishIcon.Draw(iconRect, s_ToolbarIndex == 1, false);
            GUILayout.Space(8);

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

    public class StylishIconSelector : PopupWindowContent
    {
        private const string kCommandName = "StylishIconSelector";

        private int m_ScreenHeight = 0;
        private int m_Row = 0;

        private int m_Id;
        private EditorWindow m_Window;
        private StylishIcon m_Selected;

        private static StylishIconSelector s_Selector;

        public static void Popup(Rect position, int id)
        {
            var type = typeof(Editor).Assembly.GetType("UnityEditor.GUIView");
            var current = type.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            type = typeof(Editor).Assembly.GetType("UnityEditor.HostView");
            var actualView = type.GetProperty("actualView", BindingFlags.NonPublic | BindingFlags.Instance);
            var window = (EditorWindow)actualView.GetValue(current.GetValue(null, null), null);

            var popup = new StylishIconSelector();
            popup.m_Id = id;
            popup.m_Window = window;
            s_Selector = popup;
            PopupWindow.Show(position, popup);
        }

        public override Vector2 GetWindowSize()
        {
            if (m_ScreenHeight == 0) m_ScreenHeight = Screen.height;
            int count = StylishSettings.instance.stylishIconList.Count;
            m_Row = count;
            float height = count * 70;
            int maxRow = Mathf.CeilToInt(Screen.currentResolution.height * 0.8f / 70);
            int column = 1;
            while (count > maxRow)
            {
                m_Row = maxRow;
                height = maxRow * 70;
                column++;
                count -= maxRow;
            }
            return new Vector2(column * 102, height);
        }

        public override void OnGUI(Rect rect)
        {
            int row = 0;
            var iconRect = new Rect(rect);
            iconRect.width = 100;
            iconRect.height = 68;
            foreach (var icon in StylishSettings.instance.stylishIconList)
            {
                icon.DrawPreview(iconRect);
                if (GUI.Button(iconRect, GUIContent.none, GUIStyle.none))
                {
                    m_Selected = icon;
                    editorWindow.Close();
                    m_Window.SendEvent(EditorGUIUtility.CommandEvent(kCommandName));
                }

                iconRect.y += iconRect.height + 2;
                row++;
                if (row >= m_Row)
                {
                    row = 0;
                    iconRect.x += iconRect.width + 2;
                    iconRect.y = rect.y;
                }
            }
        }

        public static bool IsSelected(int id)
        {
            if (s_Selector == null || s_Selector.m_Id != id) return false;
            if (Event.current.type != EventType.ExecuteCommand || Event.current.commandName != kCommandName)
                return false;
            Event.current.Use();
            return true;
        }

        public static StylishIcon GetSelected()
        {
            return s_Selector.m_Selected;
        }
    }
}