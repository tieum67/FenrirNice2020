using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StylishProjectView
{
    [Serializable]
    public class StylishHighlight
    {
        public enum Mode { None, Color, Fade, Gradient }

        public Mode mode = Mode.None;
        public Color background = Color.white;
        public Color highlight = Color.white;
        public float fadeLength = 1.0f;
        public Gradient gradient = new Gradient();

        public bool overrideText = false;
        public Color textColor = Color.black;
        public FontStyle fontStyle = FontStyle.Normal;

        public bool clearOther = false;

        private bool m_UpdateTexture = true;
        private bool m_UpdatingTexture = false;
        private Texture2D m_Texture;

        public void CopyFrom(StylishHighlight other)
        {
            mode = other.mode;
            background = other.background;
            highlight = other.highlight;
            fadeLength = other.fadeLength;
            gradient.SetKeys(other.gradient.colorKeys, other.gradient.alphaKeys);

            overrideText = other.overrideText;
            textColor = other.textColor;
            fontStyle = other.fontStyle;

            clearOther = other.clearOther;
            m_UpdateTexture = true;
        }

        public float GetConfigHeight(IEnumerable<StylishHighlight> targets = null)
        {
            if (targets == null) targets = this.Yield();
            bool mix = targets.Select(t => t.mode).Distinct().Count() > 1;
            float line = 2;
            if (!mix)
            {
                if (mode == Mode.Color)
                    line = 4;
                if (mode == Mode.Fade)
                    line = 5;
                if (mode == Mode.Gradient)
                    line = 5;
            }
            if (overrideText)
                line += 2;
            float height = line * EditorGUIUtility.singleLineHeight + (line - 1) * 2;
            return height;
        }

        public void DrawConfig(IEnumerable<StylishHighlight> targets = null)
        {
            var rect = EditorGUILayout.GetControlRect(false, GetConfigHeight(targets));
            DrawConfig(rect, targets);
        }

        public void DrawConfig(Rect position, IEnumerable<StylishHighlight> targets = null)
        {
            if (targets == null) targets = this.Yield();

            EditorGUI.BeginChangeCheck();
            var rect = new Rect(position);
            rect.height = EditorGUIUtility.singleLineHeight;

            EditorGUI.showMixedValue = targets.Select(t => t.mode).Distinct().Count() > 1;
            EditorGUI.BeginChangeCheck();
            mode = (Mode)EditorGUI.EnumPopup(rect, "Highlight", mode);
            if (EditorGUI.EndChangeCheck())
                foreach (var target in targets)
                    target.mode = mode;
            var dropdownRect = new Rect(rect);
            dropdownRect.xMin += 52;
            dropdownRect.width = 24;
            dropdownRect.y -= 1;
            if (GUI.Button(dropdownRect, GUIContent.none, "StaticDropdown"))
                StylishHighlightSelector.Popup(dropdownRect, targets);
            rect.y += rect.height + 2;

            if (!EditorGUI.showMixedValue)
            {
                if (mode == Mode.Color || mode == Mode.Fade)
                {
                    if (mode == Mode.Fade)
                        rect.width = 160 + (rect.width - 160) / 2;
                    EditorGUI.showMixedValue = targets.Select(t => t.background).Distinct().Count() > 1;
                    EditorGUI.BeginChangeCheck();
                    background = EditorGUI.ColorField(rect, "Color", background);
                    if (EditorGUI.EndChangeCheck())
                        foreach (var target in targets)
                            target.background = background;
                    rect.x += rect.width + 4;
                    rect.xMax = position.xMax;

                    if (mode == Mode.Fade)
                    {
                        EditorGUI.showMixedValue = targets.Select(t => t.highlight).Distinct().Count() > 1;
                        EditorGUI.BeginChangeCheck();
                        highlight = EditorGUI.ColorField(rect, highlight);
                        if (EditorGUI.EndChangeCheck())
                            foreach (var target in targets)
                                target.highlight = highlight;
                    }
                    rect.xMin = position.xMin;
                    rect.y += rect.height + 2;
                }
                if (mode == Mode.Gradient)
                {
                    EditorGUI.showMixedValue = targets.Select(t => t.gradient).Distinct().Count() > 1;
                    EditorGUI.BeginChangeCheck();
                    gradient = InternalUtils.EditorGUIGradientField(rect, "Gradient", gradient);
                    if (EditorGUI.EndChangeCheck())
                        foreach (var target in targets.Except(this.Yield()))
                            target.gradient.SetKeys(gradient.colorKeys, gradient.alphaKeys);
                    rect.y += rect.height + 2;
                }
                if (mode == Mode.Fade || mode == Mode.Gradient)
                {
                    EditorGUI.showMixedValue = targets.Select(t => t.fadeLength).Distinct().Count() > 1;
                    EditorGUI.BeginChangeCheck();
                    fadeLength = EditorGUI.Slider(rect, "Length", fadeLength, 0, 1);
                    if (EditorGUI.EndChangeCheck())
                        foreach (var target in targets)
                            target.fadeLength = fadeLength;
                    rect.y += rect.height + 2;
                }

                if (mode != Mode.None)
                {
                    EditorGUI.showMixedValue = targets.Select(t => t.clearOther).Distinct().Count() > 1;
                    EditorGUI.BeginChangeCheck();
                    clearOther = EditorGUI.ToggleLeft(rect, "Clear Other Highlight.", clearOther);
                    if (EditorGUI.EndChangeCheck())
                        foreach (var target in targets)
                            target.clearOther = clearOther;
                    rect.y += rect.height + 2;
                }
            }

            EditorGUI.showMixedValue = targets.Select(t => t.overrideText).Distinct().Count() > 1;
            EditorGUI.BeginChangeCheck();
            overrideText = EditorGUI.ToggleLeft(rect, "Override Text Color and Font Style.", overrideText);
            if (EditorGUI.EndChangeCheck())
                foreach (var target in targets)
                    target.overrideText = overrideText;
            rect.y += rect.height + 2;
            if (!EditorGUI.showMixedValue && overrideText)
            {
                EditorGUI.showMixedValue = targets.Select(t => t.textColor).Distinct().Count() > 1;
                EditorGUI.BeginChangeCheck();
                textColor = EditorGUI.ColorField(rect, "Text Color", textColor);
                if (EditorGUI.EndChangeCheck())
                    foreach (var target in targets)
                        target.textColor = textColor;
                rect.y += rect.height + 2;

                EditorGUI.showMixedValue = targets.Select(t => t.fontStyle).Distinct().Count() > 1;
                EditorGUI.BeginChangeCheck();
                fontStyle = (FontStyle)EditorGUI.EnumPopup(rect, "Font Style", fontStyle);
                if (EditorGUI.EndChangeCheck())
                    foreach (var target in targets)
                        target.fontStyle = fontStyle;
                rect.y += rect.height + 2;
            }

            if (EditorGUI.EndChangeCheck())
            {
                foreach (var target in targets)
                    target.m_UpdateTexture = true;
                StylishSettings.Save();
            }
            EditorGUI.showMixedValue = false;
        }

        public void Draw(Rect position)
        {
            switch (mode)
            {
                case Mode.Color:
                    DrawColor(position);
                    break;
                case Mode.Fade:
                    DrawFade(position);
                    break;
                case Mode.Gradient:
                    DrawGradient(position);
                    break;
            }
        }

        private void DrawColor(Rect position)
        {
            EditorGUI.DrawRect(position, background);
        }

        private void DrawFade(Rect position)
        {
            if (m_Texture == null || m_UpdateTexture)
            {
                var fade = new Gradient();
                var color = new GradientColorKey[] { new GradientColorKey(background, 0), new GradientColorKey(highlight, 1) };
                var alpha = new GradientAlphaKey[] { new GradientAlphaKey(background.a, 0), new GradientAlphaKey(highlight.a, 1) };
                fade.SetKeys(color, alpha);
                UpdateTexture(fade);
            }
            var rect = new Rect(position);
            rect.width *= fadeLength;
            if (m_Texture != null)
                GUI.DrawTexture(rect, m_Texture);
            rect.xMin = rect.xMax;
            rect.xMax = position.xMax;
            EditorGUI.DrawRect(rect, highlight);
        }

        private void DrawGradient(Rect position)
        {
            if (m_Texture == null || m_UpdateTexture)
                UpdateTexture(gradient);
            var rect = new Rect(position);
            rect.width *= fadeLength;
            if (m_Texture != null)
                GUI.DrawTextureWithTexCoords(rect, m_Texture, new Rect(0, 0, 0.99f, 1));
            var color = gradient.colorKeys[gradient.colorKeys.Length - 1].color;
            color.a = gradient.alphaKeys[gradient.alphaKeys.Length - 1].alpha;
            rect.xMin = rect.xMax;
            rect.xMax = position.xMax;
            EditorGUI.DrawRect(rect, color);
        }

        private void UpdateTexture(Gradient gradient)
        {
            if (m_UpdatingTexture) return;
            InternalUtils.GradientClearCache();
            var preview = InternalUtils.GetGradientPreview(gradient);
            if (m_Texture == null)
            {
                m_Texture = new Texture2D(preview.width, preview.height, preview.format, preview.mipmapCount > 1);
                m_Texture.wrapMode = TextureWrapMode.Clamp;
            }
            m_Texture.LoadRawTextureData(preview.GetRawTextureData());
            m_Texture.Apply();
            m_UpdateTexture = m_UpdatingTexture = false;
        }
    }

    public class StylishHighlightSelector : PopupWindowContent
    {
        private IEnumerable<StylishHighlight> m_StylishHighlights;
        private GUIStyle m_Style;
        private Color m_TextColor;
        private FontStyle m_FontStyle;

        public static void Popup(Rect position, IEnumerable<StylishHighlight> highlights)
        {
            var popup = new StylishHighlightSelector();
            popup.m_StylishHighlights = highlights;
            PopupWindow.Show(position, popup);
        }

        public override Vector2 GetWindowSize()
        {
            var height = (StylishSettings.instance.highlightPresetList.Count + 1) * (EditorGUIUtility.singleLineHeight + 2) + 4;
            return new Vector2(200, height);
        }

        public override void OnGUI(Rect rect)
        {
            if (m_Style == null)
            {
                m_Style = new GUIStyle("PR Label");
                m_TextColor = m_Style.normal.textColor;
                m_FontStyle = m_Style.fontStyle;
            }

            GUILayout.Space(1);
            GUI.enabled = m_StylishHighlights.Count() == 1;
            if (GUILayout.Button("Add to Preset", "OL Plus"))
            {
                var highlight = new StylishHighlight();
                highlight.CopyFrom(m_StylishHighlights.Single());
                StylishSettings.instance.highlightPresetList.Add(highlight);
            }
            GUI.enabled = true;
            GUILayout.Space(1);

            int index = 1;
            StylishHighlight remove = null;
            foreach (var highlight in StylishSettings.instance.highlightPresetList)
            {
                var highlightRect = EditorGUILayout.GetControlRect(false);
                highlight.Draw(highlightRect);

                m_Style.normal.textColor = highlight.overrideText ? highlight.textColor : m_TextColor;
                m_Style.fontStyle = highlight.overrideText ? highlight.fontStyle : m_FontStyle;
                GUI.Label(highlightRect, "Highlight " + index, m_Style);
                index++;

                highlightRect.xMax -= 20;
                if (GUI.Button(highlightRect, GUIContent.none, GUIStyle.none))
                {
                    foreach (var target in m_StylishHighlights)
                        target.CopyFrom(highlight);
                    editorWindow.Close();
                    EditorApplication.RepaintProjectWindow();
                }

                highlightRect.xMin = highlightRect.xMax;
                highlightRect.xMax += 20;
                if (GUI.Button(highlightRect, GUIContent.none, "OL Minus"))
                    remove = highlight;
            }
            if (remove != null)
                StylishSettings.instance.highlightPresetList.Remove(remove);
        }
    }
}