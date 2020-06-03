using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StylishProjectView
{
    [Serializable]
    public class ExportSettings
    {
        [Serializable]
        public class ExportStylishData
        {
            public string name;
            public ExportStylishIconOverride icon;
            public StylishHighlight highlight;
        }

        [Serializable]
        public class ExportStylishIcon
        {
            public List<TextureLayer> normal;
            public List<TextureLayer> small;
        }

        [Serializable]
        public class ExportStylishIconOverride
        {
            public enum Mode { None, Replace, Overlay }

            public Mode mode;
            public ColorBlend.Mode blend;
            public int icon;
            public Color[] normalTints;
            public Color[] smallTints;
        }

        [Serializable]
        public class ExportPathData
        {
            public string path;
            public bool bypass = false;
            public bool enable = false;
            public bool autoLevel = false;
            public bool autoSibling = false;
            public ExportStylishData stylishData = new ExportStylishData();
        }

        public bool iconOnly = false;
        public GeneralSetting general;
        public List<string> subfolderList;
        public List<bool> subfolderToggleList;

        public List<ExportStylishIcon> stylishIconList = new List<ExportStylishIcon>();
        public List<ExportStylishData> cyclicStylishList = new List<ExportStylishData>();
        public List<ExportStylishData> keywordStylishList = new List<ExportStylishData>();
        public List<ExportPathData> pathDataList = new List<ExportPathData>();
        public List<StylishHighlight> highlightPresetList = new List<StylishHighlight>();

        public static void Import(StylishSettings instance, bool iconOnly)
        {
            var file = EditorUtility.OpenFilePanel("Import Settings", "", "json");
            if (string.IsNullOrEmpty(file)) return;
            Import(instance, file, iconOnly);
        }

        public static void Import(StylishSettings instance, string file, bool iconOnly)
        {
            var export = new ExportSettings();
            var json = File.ReadAllText(file, System.Text.Encoding.UTF8);
            EditorJsonUtility.FromJsonOverwrite(json, export);
            ImportSettings(instance, export, iconOnly);
        }

        public static void Export(StylishSettings instance, bool iconOnly)
        {
            var json = EditorJsonUtility.ToJson(CreateExportSettings(instance, iconOnly), true);
            var file = EditorUtility.SaveFilePanel("Export Settings", "", "StylishSettings", "json");
            if (string.IsNullOrEmpty(file)) return;
            File.WriteAllText(file, json, System.Text.Encoding.UTF8);
        }

        private static void ImportSettings(StylishSettings instance, ExportSettings export, bool iconOnly)
        {
            if (export.iconOnly) iconOnly = true;
            if (!iconOnly)
            {
                StylishSettings.Clear();
                instance.general = export.general;
                instance.subfolderList.AddRange(export.subfolderList);
                instance.subfolderToggleList.AddRange(export.subfolderToggleList);
                instance.highlightPresetList.AddRange(export.highlightPresetList);
            }
            foreach (var item in export.stylishIconList)
            {
                var icon = StylishIcon.Create();
                icon.small = item.small;
                icon.normal = item.normal;
                instance.stylishIconList.Add(icon);
            }
            if (!iconOnly)
            {
                foreach (var item in export.cyclicStylishList)
                    instance.cyclicStylishList.Add(CreateStylishData(item));
                foreach (var item in export.keywordStylishList)
                    instance.keywordStylishList.Add(CreateStylishData(item));
                foreach (var item in export.pathDataList)
                    StylishSettings.AddPathData(CreatePathData(item));
                StylishSettings.RebuildTable();
            }
            StylishSettings.Save();
        }

        private static ExportSettings CreateExportSettings(StylishSettings instance, bool iconOnly)
        {
            var export = new ExportSettings();
            export.iconOnly = iconOnly;
            if (!iconOnly)
            {
                export.general = instance.general;
                export.subfolderList = instance.subfolderList;
                export.subfolderToggleList = instance.subfolderToggleList;
                export.highlightPresetList = instance.highlightPresetList;
            }
            foreach (var item in instance.stylishIconList)
            {
                var ex = new ExportStylishIcon();
                ex.small = item.small;
                ex.normal = item.normal;
                export.stylishIconList.Add(ex);
            }
            if (!iconOnly)
            {
                foreach (var item in instance.cyclicStylishList)
                    export.cyclicStylishList.Add(CreateExportStylishData(item));
                foreach (var item in instance.keywordStylishList)
                    export.keywordStylishList.Add(CreateExportStylishData(item));
                foreach (var item in StylishSettings.GetPathStylisPaths())
                    export.pathDataList.Add(CreateExportPathData(StylishSettings.GetPathData(item, false)));
            }
            return export;
        }

        private static StylishData CreateStylishData(ExportStylishData data)
        {
            var result = new StylishData();
            result.name = data.name;
            result.highlight = data.highlight;
            result.icon = new StylishIconOverride();
            result.icon.mode = (StylishIconOverride.Mode)data.icon.mode;
            result.icon.blend = data.icon.blend;
            result.icon.normalTints = data.icon.normalTints;
            result.icon.smallTints = data.icon.smallTints;
            result.icon.icon = StylishSettings.instance.stylishIconList[data.icon.icon];
            return result;
        }

        private static PathData CreatePathData(ExportPathData data)
        {
            var result = new PathData();
            result.path = data.path;
            result.bypass = data.bypass;
            result.enable = data.enable;
            result.autoLevel = data.autoLevel;
            result.autoSibling = data.autoSibling;
            result.stylishData = CreateStylishData(data.stylishData);
            return result;
        }

        private static ExportStylishData CreateExportStylishData(StylishData data)
        {
            var result = new ExportStylishData();
            result.name = data.name;
            result.highlight = data.highlight;
            result.icon = new ExportStylishIconOverride();
            result.icon.mode = (ExportStylishIconOverride.Mode)data.icon.mode;
            result.icon.blend = data.icon.blend;
            result.icon.normalTints = data.icon.normalTints;
            result.icon.smallTints = data.icon.smallTints;
            result.icon.icon = StylishSettings.instance.stylishIconList.IndexOf(data.icon.icon);
            return result;
        }

        private static ExportPathData CreateExportPathData(PathData data)
        {
            var result = new ExportPathData();
            result.path = data.path;
            result.bypass = data.bypass;
            result.enable = data.enable;
            result.autoLevel = data.autoLevel;
            result.autoSibling = data.autoSibling;
            result.stylishData = CreateExportStylishData(data.stylishData);
            return result;
        }
    }
}