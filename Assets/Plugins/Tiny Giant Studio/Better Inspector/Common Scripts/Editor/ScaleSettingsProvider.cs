using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TinyGiantStudio.BetterInspector
{
    /// <summary>
    /// This creates the scale settings in the project settings window.
    /// </summary>
    public static class ScaleSettingsProvider
    {
        [SettingsProvider]
        private static SettingsProvider CreateScaleSettingsProvider()
        {
            ScalesManager scales = ScalesFinder.MyScales();

            if (scales.units.Count == 0)
            {
                scales.Reset();
            }

            SettingsProvider provider = new SettingsProvider("Project/Tiny Giant Studio/Scale Settings", SettingsScope.Project)
            {
                label = "Scale Settings",
                guiHandler = (searchContext) =>
                {
                    EditorGUI.BeginChangeCheck();

                    GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MaxWidth(500)); //full custom unit settings

                    GUILayout.BeginHorizontal(EditorStyles.toolbar);
                    EditorGUILayout.LabelField("Name", EditorStyles.miniLabel, GUILayout.MaxWidth(300));
                    EditorGUILayout.LabelField("Value", EditorStyles.miniLabel, GUILayout.MaxWidth(150));
                    GUILayout.EndHorizontal();

                    for (int i = 0; i < scales.units.Count; i++)
                    {
                        GUILayout.BeginHorizontal();

                        string newName = EditorGUILayout.TextField(scales.units[i].name, GUILayout.MaxWidth(300));
                        if (newName != scales.units[i].name)
                        {
                            scales.units[i].name = newName;
                            EditorUtility.SetDirty(scales);
                        }

                        float newValue = EditorGUILayout.FloatField(scales.units[i].value, GUILayout.MaxWidth(150));
                        if (newValue != scales.units[i].value)
                        {
                            scales.units[i].value = newValue;
                            EditorUtility.SetDirty(scales);
                        }

                        if (GUILayout.Button("Remove", GUILayout.MaxWidth(70)))
                        {
                            scales.units.RemoveAt(i);
                            EditorUtility.SetDirty(scales);
                        }

                        GUILayout.EndHorizontal();
                    }
                    if (GUILayout.Button("Add new Unit", GUILayout.MaxWidth(500), GUILayout.Height(30)))
                    {
                        scales.units.Add(new Unit("New unit", 1));
                        EditorUtility.SetDirty(scales);
                    }
                    GUILayout.Space(30);

                    GUILayout.Space(10);
                    if (GUILayout.Button("Reset to default"))
                    {
                        if (EditorUtility.DisplayDialog("Restore default unit values?", "Are you sure you want to restore default values? This will overwrite all changes to the units.", "Yes", "No"))
                        {
                            scales.Reset();
                            EditorUtility.SetDirty(scales);
                        }
                    }
                    GUILayout.EndVertical();

                    if (EditorGUI.EndChangeCheck())
                    {
                    }
                },

                keywords = new HashSet<string>(new[] { "Scale", "Settings" })
            };

            return provider;
        }
    }
}