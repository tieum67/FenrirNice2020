using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StylishProjectView
{
    public static class Utils
    {
        public static GUIContent CreateContent(string text = null, string image = null, string tooltip = null)
        {
            var content = new GUIContent();
            content.text = string.IsNullOrEmpty(text) ? string.Empty : text;
            content.image = string.IsNullOrEmpty(image) ? null : EditorGUIUtility.IconContent(image).image;
            content.tooltip = string.IsNullOrEmpty(tooltip) ? string.Empty : tooltip;
            return content;
        }

        private static Dictionary<string, Texture2D> s_IconCache = new Dictionary<string, Texture2D>();

        public static Texture2D GetResource(this string resource)
        {
            Texture2D icon = null;
            if (s_IconCache.TryGetValue(resource, out icon))
                return icon;
            icon = EditorGUIUtility.IconContent(resource).image as Texture2D;
            s_IconCache[resource] = icon;
            return icon;
        }

        public static IEnumerable<T> Yield<T>(this T item)
        {
            yield return item;
        }

        private static List<Action> s_DelayedAction = new List<Action>();
        private static List<double> s_DelayedTime = new List<double>();

        public static void DelayedCall(float seconds, Action action)
        {
            if (s_DelayedAction.Count == 0)
                EditorApplication.update += CheckDelayedCall;
            s_DelayedAction.Add(action);
            s_DelayedTime.Add(EditorApplication.timeSinceStartup + seconds);
        }

        private static void CheckDelayedCall()
        {
            for (int i = s_DelayedTime.Count - 1; i >= 0; i--)
            {
                if (EditorApplication.timeSinceStartup < s_DelayedTime[i]) continue;
                s_DelayedAction[i]();
                s_DelayedTime.RemoveAt(i);
                s_DelayedAction.RemoveAt(i);
            }
            if (s_DelayedAction.Count == 0)
                EditorApplication.update -= CheckDelayedCall;
        }
    }

    public static class ColorBlend
    {
        //public enum Mode { None, Override, Multiple, Screen, Overlay, GrayedMultiple, GrayedScreen, GrayedOverlay }
        public enum Mode { None, Override, Multiple }

        public static Color Blend(this Color a, Color b, Mode blend)
        {
            //Color temp;
            switch (blend)
            {
                case Mode.Override:
                    return b;
                case Mode.Multiple:
                    return a * b;
                //case Mode.Screen:
                //    temp = new Color(1 - a.r, 1 - a.g, 1 - a.b, a.a);
                //    temp *= new Color(1 - b.r, 1 - b.g, 1 - b.b, b.a);
                //    return new Color(1 - temp.r, 1 - temp.g, 1 - temp.b, temp.a);
                //case Mode.Overlay:
                //    if (a.grayscale < 0.5f)
                //        return 2 * (a * b);
                //    else
                //    {
                //        temp = new Color(1 - a.r, 1 - a.g, 1 - a.b, a.a);
                //        temp *= new Color(1 - b.r, 1 - b.g, 1 - b.b, b.a);
                //        temp *= 2;
                //        return new Color(1 - temp.r, 1 - temp.g, 1 - temp.b, temp.a);
                //    }
                //case Mode.GrayedMultiple:
                //    return new Color(a.grayscale, a.grayscale, a.grayscale, a.a).Blend(b, Mode.Multiple);
                //case Mode.GrayedScreen:
                //    return new Color(a.grayscale, a.grayscale, a.grayscale, a.a).Blend(b, Mode.Screen);
                //case Mode.GrayedOverlay:
                //    return new Color(a.grayscale, a.grayscale, a.grayscale, a.a).Blend(b, Mode.Overlay);
            }
            return a;
        }
    }

    public class GUIColor : GUI.Scope
    {
        private Color m_Color;

        public GUIColor(Color color)
        {
            m_Color = GUI.color;
            GUI.color = color;
        }

        public GUIColor(float alpha)
        {
            m_Color = GUI.color;
            var col = m_Color;
            col.a = alpha;
            GUI.color = col;
        }

        public GUIColor(Color col, float alpha)
        {
            m_Color = GUI.color;
            col.a = alpha;
            GUI.color = col;
        }

        protected override void CloseScope()
        {
            GUI.color = m_Color;
        }
    }
}