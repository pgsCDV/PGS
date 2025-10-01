using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TinyGiantStudio.BetterInspector
{
    /// <summary>
    /// This class handles everything that has to do with the copy, paste and rotation buttons.
    /// </summary>
    public class QuickActions
    {
        private string hierarchyCopyIdentifier = "Hierarchy Copy";

        /// <summary>
        /// This sets up copy,paste and reset button for one target transform in local space.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="container"></param>
        public void HookLocalTransform(Object[] targetsObj, VisualElement container)
        {
            //Converting back to array because list is not supported by Undo.RecordObjects
            var targets = targetsObj.Cast<Transform>().ToList().ToArray();

            var positionToolbar = container.Q<GroupBox>("PositionToolbar");
            var pastePositionButton = positionToolbar.Q<Button>("Paste");

            var rotationToolbar = container.Q<GroupBox>("RotationToolbar");
            var pasteRotationButton = rotationToolbar.Q<Button>("Paste");

            var scaleToolbar = container.Q<GroupBox>("ScaleToolbar");
            var pasteScaleButton = scaleToolbar.Q<Button>("Paste");

            positionToolbar.Q<Button>("Copy").clicked += () =>
            {
                if (targets.Length == 1)
                    EditorGUIUtility.systemCopyBuffer = "Vector3" + targets[0].localPosition.ToString("F20");
                else
                    CopyMultipleSelectToBuffer_position(targets, false);

                UpdatePasteButtons();
            };

            pastePositionButton.clicked += () =>
            {
                if (targets.Length == 1)
                {
                    GetVector3FromCopyBuffer(out bool exists, out float x, out float y, out float z);
                    if (!exists)
                        return;

                    Undo.RecordObject(targets[0], "Position Paste on " + targets[0].gameObject.name);

                    targets[0].localPosition = new Vector3(x, y, z);

                    EditorUtility.SetDirty(targets[0]);
                }
                else
                {
                    GetVector3ListFromCopyBuffer(out bool exists, out List<string> values);

                    if (!exists) return;

                    Undo.RecordObjects(targets, "Position Paste.");
                    for (int i = 0; i < targets.Length; i++)
                    {
                        if (values.Count <= i)
                            break;

                        var value = GetVector3FromString(values[i], out bool exists2);

                        if (!exists2) continue;

                        targets[i].localPosition = value;

                        EditorUtility.SetDirty(targets[i]);
                    }
                }
            };

            positionToolbar.Q<Button>("Reset").clicked += () =>
            {
                Undo.RecordObjects(targets, "Reset positions.");
                foreach (var target in targets)
                {
                    target.localPosition = Vector3.zero;
                    EditorUtility.SetDirty(target);
                }
            };

            rotationToolbar.Q<Button>("Copy").clicked += () =>
            {
                if (targets.Length == 1)
                    EditorGUIUtility.systemCopyBuffer = "Vector3" + targets[0].localEulerAngles.ToString("F20"); //This used to be :myTarget.localRotation.eulerAngles.ToString(). Is there any difference?
                else
                    CopyMultipleSelectToBuffer_rotation(targets, false);

                UpdatePasteButtons();
            };
            pasteRotationButton.clicked += () =>
            {
                if (targets.Length == 1)
                {
                    GetVector3FromCopyBuffer(out bool exists, out float x, out float y, out float z);
                    if (exists)
                    {
                        Undo.RecordObjects(targets, "Rotation Paste.");

                        //These three shouldn't be needed but were added because for some reason,
                        //the world rotation field were not being updated without them because isRotationUpdatedByWorldField was true when it shouldn't be
                        //temporarilyRotatedToCheckSize = false;
                        //isRotationUpdatedByWorldField = false;
                        //isRotateUpdatedByLocalField = false;

                        targets[0].localRotation = Quaternion.Euler(x, y, z);

                        //soTarget.Update();
                        EditorUtility.SetDirty(targets[0]);
                    }
                }
                else
                {
                    GetVector3ListFromCopyBuffer(out bool exists, out List<string> values);
                    //values.Reverse();

                    if (!exists) return;

                    Undo.RecordObjects(targets, "Rotation Paste.");
                    //for (int i = transforms.Count() - 1; i >= 0; i--)
                    for (int i = 0; i < targets.Length; i++)
                    {
                        if (values.Count <= i)
                            break;

                        var value = GetVector3FromString(values[i], out bool exists2);

                        if (!exists2) continue;

                        ////These three shouldn't be needed but were added because for some reason,
                        ////the world rotation field were not being updated without them because isRotationUpdatedByWorldField was true when it shouldn't be
                        //temporarilyRotatedToCheckSize = false;
                        //isRotationUpdatedByWorldField = false;
                        //isRotateUpdatedByLocalField = false;

                        targets[i].localRotation = Quaternion.Euler(value.x, value.y, value.z);

                        //soTarget.Update();
                        EditorUtility.SetDirty(targets[i]);
                    }
                }
            };
            rotationToolbar.Q<Button>("Reset").clicked += () =>
            {
                Undo.RecordObjects(targets, "Reset rotation.");
                foreach (var target in targets)
                {
                    target.localRotation = Quaternion.Euler(Vector3.zero);
                    EditorUtility.SetDirty(target);
                }
            };

            scaleToolbar.Q<Button>("Copy").clicked += () =>
            {
                if (targets.Length == 1)
                    EditorGUIUtility.systemCopyBuffer = "Vector3" + targets[0].localScale.ToString("F10");
                else
                    CopyMultipleSelectToBuffer_scale(targets, false);

                UpdatePasteButtons();
            };

            pasteScaleButton.clicked += () =>
            {
                if (targets.Length == 1)
                {
                    GetVector3FromCopyBuffer(out bool exists, out float x, out float y, out float z);

                    if (!exists)
                        return;

                    Undo.RecordObject(targets[0], "Scale Paste on " + targets[0].gameObject.name);

                    targets[0].localScale = new Vector3(x, y, z);

                    EditorUtility.SetDirty(targets[0]);
                }
                else
                {
                    GetVector3ListFromCopyBuffer(out bool exists, out List<string> values);

                    if (!exists) return;

                    Undo.RecordObjects(targets, "Scale Paste.");
                    for (int i = 0; i < targets.Length; i++)
                    {
                        if (values.Count <= i)
                            break;

                        var value = GetVector3FromString(values[i], out bool exists2);

                        if (!exists2) continue;

                        targets[i].localScale = value;
                        EditorUtility.SetDirty(targets[i]);
                    }
                }
            };
            scaleToolbar.Q<Button>("Reset").clicked += () =>
            {
                Undo.RecordObjects(targets, "Reset position.");
                foreach (var target in targets)
                {
                    target.localScale = Vector3.one;
                    EditorUtility.SetDirty(target);
                }
            };

            container.RegisterCallback<MouseOverEvent>(e => { UpdatePasteButtons(); });

            UpdatePasteButtons();
            void UpdatePasteButtons()
            {
                bool exists;

                if (targets.Length == 1)
                {
                    GetVector3FromCopyBuffer(out exists, out float x, out float y, out float z);

                    if (exists)
                    {
                        string space = "Local";
                        UpdateTooltip_CanPaste(pastePositionButton, pasteRotationButton, pasteScaleButton, x, y, z, space);
                    }
                }
                else
                {
                    GetVector3ListFromCopyBuffer(out exists, out List<string> values);
                    if (exists)
                    {
                        pastePositionButton.SetEnabled(true);
                        pasteRotationButton.SetEnabled(true);
                        pasteScaleButton.SetEnabled(true);

                        var transforms = targets.Cast<Transform>().ToList();
                        string valueString = "\n";

                        for (int i = 0; i < transforms.Count; i++)
                        {
                            if (values.Count <= i)
                                break;

                            valueString += "\n" + transforms[i] + " " + values[i] + "\n";
                        }

                        valueString += "\n";

                        pastePositionButton.tooltip = "Paste " + valueString + "to local position.";
                        pasteRotationButton.tooltip = "Paste " + "to local rotation.";
                        pasteScaleButton.tooltip = "Paste " + valueString + "to local scale.";
                    }
                }

                if (!exists)
                    UpdateTooltip_unableToPaste(pastePositionButton, pasteRotationButton, pasteScaleButton);
            }
        }

        private void UpdateTooltip_CanPaste(Button pastePositionButton, Button pasteRotationButton, Button pasteScaleButton, float x, float y, float z, string space)
        {
            pastePositionButton.SetEnabled(true);
            pasteRotationButton.SetEnabled(true);
            pasteScaleButton.SetEnabled(true);

            pastePositionButton.tooltip = "Paste " + x + "," + y + "," + z + " to " + space + " position.";
            pasteRotationButton.tooltip = "Paste " + x + "," + y + "," + " to " + space + " rotation.";
            pasteScaleButton.tooltip = "Paste " + x + "," + y + "," + z + " to " + space + " scale.";
        }

        private void UpdateTooltip_unableToPaste(Button pastePositionButton, Button pasteRotationButton, Button pasteScaleButton)
        {
            pastePositionButton.SetEnabled(false);
            pasteRotationButton.SetEnabled(false);
            pasteScaleButton.SetEnabled(false);

            pastePositionButton.tooltip = "A valid Value isn't copied";
            pasteRotationButton.tooltip = "A valid Value isn't copied";
            pasteScaleButton.tooltip = "A valid Value isn't copied";
        }

        /// <summary>
        /// This checks if any Vector3 is currently copied to systemCopyBuffer, then returns that
        /// </summary>
        /// <param name="exists"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        private void GetVector3FromCopyBuffer(out bool exists, out float x, out float y, out float z)
        {
            exists = false;
            x = 0; y = 0; z = 0;

            string copyBuffer = EditorGUIUtility.systemCopyBuffer;
            if (copyBuffer != null)
            {
                if (copyBuffer.Contains("Vector3"))
                {
                    if (copyBuffer.Length > 9)
                    {
                        copyBuffer = copyBuffer.Substring(8, copyBuffer.Length - 9);
                        string[] valueStrings = copyBuffer.Split(',');
                        if (valueStrings.Length == 3)
                        {
                            char userDecimalSeparator = Convert.ToChar(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

                            string sanitizedValueString_x = valueStrings[0].Replace(userDecimalSeparator == ',' ? '.' : ',', userDecimalSeparator);
                            if (float.TryParse(sanitizedValueString_x, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out x))
                                exists = true;

                            if (exists)
                            {
                                string sanitizedValueString_y = valueStrings[1].Replace(userDecimalSeparator == ',' ? '.' : ',', userDecimalSeparator);
                                if (!float.TryParse(sanitizedValueString_y, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out y))
                                    exists = false;
                            }

                            if (exists)
                            {
                                string sanitizedValueString_z = valueStrings[2].Replace(userDecimalSeparator == ',' ? '.' : ',', userDecimalSeparator);
                                if (!float.TryParse(sanitizedValueString_z, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out z))
                                    exists = false;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// When multiple transform is selected, this checks if a list of Vector3 is currently copied to systemCopyBuffer, then returns that
        /// </summary>
        /// <param name="exists"></param>
        /// <param name="values"></param>
        private void GetVector3ListFromCopyBuffer(out bool exists, out List<string> values)
        {
            exists = true;
            values = new List<string>();

            string copyBuffer = EditorGUIUtility.systemCopyBuffer;

            if (!copyBuffer.Contains(hierarchyCopyIdentifier))
            {
                exists = false;
                return;
            }

            copyBuffer = copyBuffer.Substring(hierarchyCopyIdentifier.Length, copyBuffer.Length - hierarchyCopyIdentifier.Length);

            string[] copiedItems = copyBuffer.Split('\n');
            foreach (string s in copiedItems)
            {
                if (!string.IsNullOrEmpty(s))
                    values.Add(s);
            }
        }

        private Vector3 GetVector3FromString(string value, out bool exists)
        {
            exists = false;
            float x = 0;
            float y = 0;
            float z = 0;

            if (value != null)
            {
                if (value.Contains("Vector3"))
                {
                    if (value.Length > 9)
                    {
                        value = value.Substring(8, value.Length - 9);
                        string[] valueStrings = value.Split(',');
                        if (valueStrings.Length == 3)
                        {
                            if (float.TryParse(valueStrings[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out x))
                                exists = true;

                            if (exists)
                            {
                                if (!float.TryParse(valueStrings[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out y))
                                    exists = false;
                            }

                            if (exists)
                            {
                                if (!float.TryParse(valueStrings[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out z))
                                    exists = false;
                            }
                        }
                    }
                }
            }

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// When multiple transform is selected, this copies a list of their position
        /// </summary>
        /// <param name="targets"></param>
        /// <param name="worldSpace"></param>
        private void CopyMultipleSelectToBuffer_position(Transform[] targets, bool worldSpace)
        {
            string copyString = hierarchyCopyIdentifier;

            foreach (Transform t in targets)
            {
                copyString += "\n";
                if (worldSpace)
                    copyString += "Vector3" + t.position.ToString();
                else
                    copyString += "Vector3" + t.localPosition.ToString();
            }

            EditorGUIUtility.systemCopyBuffer = copyString;
        }

        private void CopyMultipleSelectToBuffer_rotation(Transform[] targets, bool worldSpace)
        {
            string copyString = hierarchyCopyIdentifier;

            foreach (Transform t in targets)
            {
                copyString += "\n";

                if (worldSpace)
                    copyString += "Vector3" + t.eulerAngles.ToString();
                else
                    copyString += "Vector3" + t.localEulerAngles.ToString();
            }

            EditorGUIUtility.systemCopyBuffer = copyString;
        }

        private void CopyMultipleSelectToBuffer_scale(Transform[] targets, bool worldSpace)
        {
            string copyString = hierarchyCopyIdentifier;

            foreach (Transform t in targets)
            {
                copyString += "\n";
                if (worldSpace)
                    copyString += "Vector3" + t.lossyScale.ToString();
                else
                    copyString += "Vector3" + t.localScale.ToString();
            }

            EditorGUIUtility.systemCopyBuffer = copyString;
        }
    }
}