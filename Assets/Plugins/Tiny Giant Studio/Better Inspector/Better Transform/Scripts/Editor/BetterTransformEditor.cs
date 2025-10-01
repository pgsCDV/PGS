using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.VFX;
using Debug = UnityEngine.Debug;

namespace TinyGiantStudio.BetterInspector
{
    /// <summary>
    /// Methods containing the word Update can be called multiple times to update to reflect changes.
    /// Methods containing the word Setup are called once when the inspector is created.
    ///
    ///
    /// Note to self:
    /// KEEP ALL FOLDOUTS HIDDEN BY DEFAULT IN THE UXML FILE
    /// 1. Maybe add texture icon instead of the handles for _showSizeGizmosLabelHandle
    ///
    /// Maybe:
    /// 1. Swappable to IMGUI transform
    /// 2. Randomize Rotation, Position and Scale
    /// 4. Angles on Gizmo
    ///
    /// To-do
    /// 1. Enable/disable width adapt
    ///
    /// </summary>

    [CanEditMultipleObjects]
    [CustomEditor(typeof(Transform))]
    public class BetterTransformEditor : Editor
    {
        #region Variable Declaration

        #region Referenced in the Inspector

        /// <summary>
        /// If reference is lost, retrieved from file location
        /// </summary>
        [SerializeField]
        private VisualTreeAsset visualTreeAsset;

        private readonly string visualTreeAssetFileLocation = "Assets/Plugins/Tiny Giant Studio/Better Inspector/Better Transform/Scripts/Editor/BetterTransform.uxml";

        [SerializeField]
        private VisualTreeAsset folderTemplate;

        [SerializeField]
        private StyleSheet animatedFoldoutStyleSheet;
        private readonly string animatedFoldoutStyleSheetFileLocation = "Assets/Plugins/Tiny Giant Studio/Better Inspector/Common Scripts/Editor/StyleSheets/CustomFoldout_Animated.uss";

        private readonly string folderTemplateFileLocation = "Assets/Plugins/Tiny Giant Studio/Better Inspector/Better Transform/Scripts/Editor/Templates/CustomFoldoutTemplate.uxml";

        #endregion Referenced in the Inspector

        private VisualElement root;

        private Transform transform;
        private SerializedObject soTarget;

        private BetterTransformSettings editorSettings;

        private readonly string prefabOverrideLabel = "prefab_override_label";

        private Editor originalEditor;
        private List<Editor> otherBetterTransformEditors = new List<Editor>();

        private CustomFoldoutSetup customFoldoutSetup;

        private GroupBox topGroupBox;

        private GroupBox performanceLoggingGroupBox;
        private Stopwatch stopwatch;

        #endregion Variable Declaration

        private float time;
        private float totalMS = 0;
        private bool logPerformance;
        private bool logDetailedPerformance;

        #region Unity Stuff

        private static bool domainReloaded = false;

        [InitializeOnLoadMethod]
        private static void MyInitializationMethod()
        {
            domainReloaded = true;
        }

        private void Awake()
        {
            //On domain reload
            if (domainReloaded)
            {
                domainReloaded = false;

                sizeSetupDone = false;
                settingsFieldSetupDone = false;
                noteSetupCompleted = false;
            }
        }

        private void OnEnable()
        {
            //On domain reload
            if (domainReloaded)
            {
                domainReloaded = false;

                sizeSetupDone = false;
                settingsFieldSetupDone = false;
                noteSetupCompleted = false;
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < otherBetterTransformEditors.Count; i++)
                DestroyImmediate(otherBetterTransformEditors[i]);

            if (originalEditor != null)
                DestroyImmediate(originalEditor);
        }

        /// <summary>
        /// CreateInspectorGUI is called for UIToolkit inspectors.
        /// OnInspectorGUI is called for IMGUI.
        ///
        /// Note: This is called each time something else is selected with this one locked.
        /// </summary>
        /// <returns>What should be shown on the inspector</returns>
        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            if (target == null)
                return root;

            ////These are to test different cultures where DecimalSeparator is different.
            ////It is required for copy pasting
            //Debug.Log(System.Globalization.CultureInfo.CurrentCulture);
            //System.Globalization.CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            //Debug.Log(System.Globalization.CultureInfo.CurrentCulture);
            //Debug.Log(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator);

            editorSettings = BetterTransformSettings.instance;
            logPerformance = editorSettings.logPerformance;
            logDetailedPerformance = editorSettings.logDetailedPerformance;

            sizeSetupDone = false;

            if (logPerformance)
            {
                if (stopwatch != null) stopwatch.Reset();
                else stopwatch = new Stopwatch();

                stopwatch.Start();
            }

            if (logPerformance)
            {
                time = stopwatch.ElapsedMilliseconds - totalMS;
                totalMS += time;
                if (logDetailedPerformance)
                    LogDelay("Start", time);
            }

            domainReloaded = false;

            transform = target as Transform;
            soTarget = new SerializedObject(target);

            if (logPerformance)
            {
                time = stopwatch.ElapsedMilliseconds - totalMS;
                totalMS += time;
                LogDelay("Serializing target time", time);
            }

            //In-case reference to the asset is lost, retrieve it from file location
            if (visualTreeAsset == null)
            {
                visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(visualTreeAssetFileLocation);

                if (logPerformance)
                {
                    time = stopwatch.ElapsedMilliseconds - totalMS;
                    totalMS += time;
                    LogDelay("Visual Tree Asset wasn't assigned, loading from asset database", time);
                }
            }
            //If can't find the Better Transform UXML,
            //Show the default inspector
            if (visualTreeAsset == null)
            {
                LoadDefaultEditor(root);

                if (logPerformance)
                {
                    LogDelay("Total time spent", stopwatch.ElapsedMilliseconds);
                    stopwatch.Stop();
                }

                return root;
            }

            visualTreeAsset.CloneTree(root);

            if (logPerformance)
            {
                time = stopwatch.ElapsedMilliseconds - totalMS;
                totalMS += time;
                LogDelay("Cloning visual asset tree", time);
            }

            FirstTimeSetup();

            if (animatedFoldoutStyleSheet == null)
                animatedFoldoutStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(animatedFoldoutStyleSheetFileLocation);

            if (animatedFoldoutStyleSheet != null) //This shouldn't happen though. Just added for just in case, didn't get any error
            {
                if (editorSettings.animatedFoldout)
                    root.styleSheets.Add(animatedFoldoutStyleSheet);
            }

            Button pingSelfButton = root.Q<Button>("PingSelfButton");
            pingSelfButton.clicked += () =>
            {
                ///Multi ping is commented out because PingObject only pings the last one.
                //if(targets.Length == 1)
                EditorGUIUtility.PingObject(transform);
                //else
                //{
                //    foreach (var t in targets)
                //    {
                //        if(t == null) continue;
                //        EditorGUIUtility.PingObject(t as Transform);
                //    }
                //}
            };
            if (editorSettings.pingSelfButton)
                pingSelfButton.style.display = DisplayStyle.Flex;
            else
                pingSelfButton.style.display = DisplayStyle.None;


            //Finish code above this line------------------
            if (logPerformance)
            {
                LogDelay("Total time spent", stopwatch.ElapsedMilliseconds);
                stopwatch.Stop();
            }

            StartSizeSchedule();





            return root;
        }

        private VisualElement sizeUpdateScheduleHolder;

        private void StartSizeSchedule()
        {
            RemoveSizeUpdateScheduler();

            if (!editorSettings.ConstantSizeUpdate)
                return;

            sizeUpdateScheduleHolder = new VisualElement();
            root.Add(sizeUpdateScheduleHolder);

            sizeUpdateScheduleHolder.schedule.Execute(() => UpdateSize(true)).Every(3000).ExecuteLater(3000); //1000 ms = 1 s
        }

        private void RemoveSizeUpdateScheduler()
        {
            if (sizeUpdateScheduleHolder == null)
                return;

            sizeUpdateScheduleHolder.RemoveFromHierarchy();
        }

        private void FirstTimeSetup()
        {
            if (root == null) return;

            if (customFoldoutSetup == null)
                customFoldoutSetup = new CustomFoldoutSetup();

            topGroupBox = root.Q<GroupBox>("TopGroupBox");

            UpdatePerformanceLoggingGroupBox();

            if (logPerformance)
            {
                time = stopwatch.ElapsedMilliseconds - totalMS;
                totalMS += time;
                if (logDetailedPerformance)
                    LogDelay("Prerequisite", time);
            }

            SetupSizeCommon();

            if (targets.Length == 1)
            {
                if (logPerformance)
                {
                    time = stopwatch.ElapsedMilliseconds - totalMS;
                    totalMS += time;
                    LogDelay("Settings", time);
                }

                if (editorSettings.ShowSizeFoldout || editorSettings.ShowSizeInLine)
                    SetupSize(customFoldoutSetup);
                else
                    HideSize();

                if (logPerformance)
                {
                    time = stopwatch.ElapsedMilliseconds - totalMS;
                    totalMS += time;
                    LogDelay("Size (Hidden or visible)", time);
                }
            }
            else
            {
                HideSize();
            }

            SetupMainControls();
            UpdateMainControls();

            QuickActions quickActions = new();
            quickActions.HookLocalTransform(targets, root.Q<GroupBox>("BothSpaceToolbarForLocalSpace"));

            if (logPerformance)
            {
                time = stopwatch.ElapsedMilliseconds - totalMS;
                totalMS += time;
                LogDelay("Position/Rotation/Size Fields", time);
            }

            UpdatePasteButtons();

            if (logPerformance)
            {
                time = stopwatch.ElapsedMilliseconds - totalMS;
                totalMS += time;
                if (logDetailedPerformance)
                    LogDelay("Paste Buttons", time);
            }

            SetupNote();
            if (logPerformance)
            {
                time = stopwatch.ElapsedMilliseconds - totalMS;
                totalMS += time;
                LogDelay("Notes", time);
            }

            SetupParentChild(root, customFoldoutSetup);
            if (logPerformance)
            {
                time = stopwatch.ElapsedMilliseconds - totalMS;
                totalMS += time;
                LogDelay("Parent & Child GameObject Informations", time);
            }

            SetupAddFunctionality();
            if (logPerformance)
            {
                time = stopwatch.ElapsedMilliseconds - totalMS;
                totalMS += time;
                if (logDetailedPerformance)
                    LogDelay("Add Button", time);
            }
            SetupAnimatorCompability();
            if (logPerformance)
            {
                time = stopwatch.ElapsedMilliseconds - totalMS;
                totalMS += time;
                if (logDetailedPerformance)
                    LogDelay("Animator Compatibility", time);
            }

            SetupViewWidthAdaption();
            if (logPerformance)
            {
                time = stopwatch.ElapsedMilliseconds - totalMS;
                totalMS += time;
                if (logDetailedPerformance)
                    LogDelay("View Width Adaption", time);
            }
            SetupInspectorColor();
            if (logPerformance)
            {
                time = stopwatch.ElapsedMilliseconds - totalMS;
                //totalMS += time;
                LogDelay("Inspector Color", time);
            }
        }

        private void LogDelay(string cause, float delay) => Debug.Log("<color=white>" + cause + "</color> : <color=yellow>" + delay + "ms</color>");

        private void UpdatePerformanceLoggingGroupBox()
        {
            if (editorSettings.logPerformance)
                TurnOnPerformanceLogging();
            else if (performanceLoggingGroupBox != null)
                TurnOffPerformanceLogging();
        }

        private void TurnOnPerformanceLogging()
        {
            //In case of a domain reload, the element is not deleted but reference is lost.
            if (performanceLoggingGroupBox == null)
                performanceLoggingGroupBox = root.Q<GroupBox>("PerformanceLoggingGroup");

            //If one doesn't exist, create it
            if (performanceLoggingGroupBox == null)
                CreatePerformanceLoggingGroupBox();

            performanceLoggingGroupBox.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// Unity 2023.2.18f1 seems to be slow at loading large UXML files, so, removing some less used stuff from UXML to C#
        /// </summary>
        private void CreatePerformanceLoggingGroupBox()
        {
            performanceLoggingGroupBox = new GroupBox();

            Button button = new Button();
            button.text = "Turn Off performance logging";
            button.clicked += TurnOffPerformanceLogging;
            performanceLoggingGroupBox.Add(button);

            performanceLoggingGroupBox.Add(new HelpBox("Please note that console logs can negatively impact performance of the inspector. So, the delays you will see here will be higher than normal usage. However, these logs serve a crucial purpose—they assist in identifying resource-intensive features. Just remember to turn it off when not needed.", HelpBoxMessageType.Info));

            root.Add(performanceLoggingGroupBox);
        }

        private void TurnOffPerformanceLogging()
        {
            editorSettings.logPerformance = false;

            performanceLoggingGroupBox.style.display = DisplayStyle.None;
            editorSettings.Save();
            Toggle performanceLoggingToggle = root.Q<Toggle>("PerformanceLoggingToggle");
            performanceLoggingToggle.value = false;
        }

        public Transform originalTransform;

        public VisualElement CreateInspectorInsideAnother(Transform newOriginal)
        {
            originalTransform = newOriginal;
            return CreateInspectorGUI();
        }

        #endregion Unity Stuff

        #region Main Controls

        #region Variables

        private Button worldSpaceButton;
        private Label worldSpaceLabel;
        private Button localSpaceButton;
        private Label localSpaceLabel;

        private GroupBox defaultEditorGroupBox;
        private GroupBox customEditorGroupBox;
        private GroupBox toolbarGroupBox;

        private readonly string positionProperty = "m_LocalPosition";
        private GroupBox positionGroupBox;
        private Label positionLabel;
        private Vector3Field localPositionField;
        private Vector3Field worldPositionField;
        private Button copyPositionButton;
        private Button pastePositionButton;
        private Button resetPositionButton;
        private VisualElement positionPrefabOverrideMark;
        private VisualElement positionDefaultPrefabOverrideMark;

        private readonly string rotationProperty = "m_LocalRotation";
        private GroupBox rotationGroupBox;
        private Label rotationLabel;
        private Vector3Field localRotationField;

        /// <summary>
        /// transform.eulerAngles
        /// </summary>
        private Vector3Field worldRotationField;

        /// <summary>
        /// An internal editor only property that used to store the value you set in the local rotation field.
        /// </summary>
        private SerializedProperty serializedEulerHint;

        private SerializedProperty rotationSerializedProperty;
        private PropertyField quaternionRotationPropertyField;
        private Button copyRotationButton;
        private Button pasteRotationButton;
        private Button resetRotationButton;

        private VisualElement rotationPrefabOverrideMark;
        private VisualElement rotationDefaultPrefabOverrideMark;

        private readonly string scaleProperty = "m_LocalScale";
        private GroupBox scaleGroupBox;
        private GroupBox scaleLabelGroupbox;
        private Label scaleLabel;
        private Vector3Field boundLocalScaleField;
        private Vector3Field localScaleField;
        private Vector3Field worldScaleField;

        //private SerializedProperty m_ConstrainProportionsScaleProperty; //doesn't work

        private Button copyScaleButton;
        private Button pasteScaleButton;
        private Button resetScaleButton;
        private VisualElement scalePrefabOverrideMark;
        private Button scaleAspectRatioLocked;
        private Button scaleAspectRatioUnlocked;

        private readonly string worldPositionReadOnlyTooltip = "World position is readonly if multiple object is selected.";
        private readonly string worldRotationReadOnlyTooltip = "World rotation is readonly if multiple object is selected.";
        private readonly string worldScaleReadOnlyTooltip = "World scale is readonly if multiple object is selected.";

        #endregion Variables

        private IVisualElementScheduledItem scheduledItem;

        //Called when the inspector window first shows up
        private void SetupMainControls()
        {
            SetupWorkSpace();

            scheduledItem = worldSpaceButton.schedule.Execute(UpdateWorldSpaceFields_WhenInWorldSpaceWorkspace).Every(1000).StartingIn(5000);

            toolbarGroupBox = root.Q<GroupBox>("NormalToolbar");
            defaultEditorGroupBox = root.Q<GroupBox>("DefaultUnityInspector");
            customEditorGroupBox = root.Q<GroupBox>("CustomEditorGroupBox");

            if (targets.Length > 1)
                LoadDefaultEditor(defaultEditorGroupBox);

            SetupPosition();
            SetupRotation();
            SetupScale();

            toolbarGroupBox.RegisterCallback<MouseOverEvent>(e => { UpdatePasteButtons(); });
        }

        //Called during various instances where the inspector window is updated
        private void UpdateMainControls()
        {
            UpdateWorkSpaceButtons();

            UpdatePosition();

            UpdateRotation();

            UpdateScale();

            if (editorSettings.ShowCopyPasteButtons)
            {
                var toolbarGroupBox = root.Q<GroupBox>("ToolbarsGroupBox");
                toolbarGroupBox.style.display = DisplayStyle.Flex;
                if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Both)
                    toolbarGroupBox.Q<GroupBox>("BothSpaceToolbarForLocalSpace").style.display = DisplayStyle.Flex;
                else
                    toolbarGroupBox.Q<GroupBox>("BothSpaceToolbarForLocalSpace").style.display = DisplayStyle.None;
            }
            else
                root.Q<GroupBox>("ToolbarsGroupBox").style.display = DisplayStyle.None;

            if (editorSettings.ShowSizeInLine || editorSettings.ShowSizeFoldout)
                UpdateSize(true);

            if (editorSettings.LoadDefaultInspector || targets.Length > 1)
            {
                switch (editorSettings.CurrentWorkSpace)
                {
                    case BetterTransformSettings.WorkSpace.Local:
                        customEditorGroupBox.style.display = DisplayStyle.None;
                        defaultEditorGroupBox.style.display = DisplayStyle.Flex;
                        if (defaultEditorGroupBox.childCount == 0)
                            LoadDefaultEditor(defaultEditorGroupBox);

                        root.Q<Label>("LocalFieldLabel").style.display = DisplayStyle.None;
                        root.Q<Label>("WorldFieldLabel").style.display = DisplayStyle.None;

                        break;

                    case BetterTransformSettings.WorkSpace.World:
                        customEditorGroupBox.style.display = DisplayStyle.Flex;
                        defaultEditorGroupBox.style.display = DisplayStyle.None;

                        root.Q<Label>("LocalFieldLabel").style.display = DisplayStyle.None;
                        root.Q<Label>("WorldFieldLabel").style.display = DisplayStyle.None;

                        break;

                    case BetterTransformSettings.WorkSpace.Both:
                        customEditorGroupBox.style.display = DisplayStyle.Flex;
                        defaultEditorGroupBox.style.display = DisplayStyle.Flex;
                        if (defaultEditorGroupBox.childCount == 0)
                            LoadDefaultEditor(defaultEditorGroupBox);

                        root.Q<Label>("LocalFieldLabel").style.display = DisplayStyle.Flex;
                        root.Q<Label>("WorldFieldLabel").style.display = DisplayStyle.Flex;

                        break;
                }
            }
            else
            {
                if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Both)
                {
                    customEditorGroupBox.style.display = DisplayStyle.Flex;
                    defaultEditorGroupBox.style.display = DisplayStyle.Flex;
                    if (defaultEditorGroupBox.childCount == 0)
                        LoadDefaultEditor(defaultEditorGroupBox);

                    root.Q<Label>("LocalFieldLabel").style.display = DisplayStyle.Flex;
                    root.Q<Label>("WorldFieldLabel").style.display = DisplayStyle.Flex;
                }
                else
                {
                    customEditorGroupBox.style.display = DisplayStyle.Flex;
                    defaultEditorGroupBox.style.display = DisplayStyle.None;

                    root.Q<Label>("LocalFieldLabel").style.display = DisplayStyle.None;
                    root.Q<Label>("WorldFieldLabel").style.display = DisplayStyle.None;
                }
            }

            if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World || editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Both)
                scheduledItem.Resume();
            else
                scheduledItem.Pause();
        }

        private void UpdatePasteButtons()
        {
            bool exists;

            if (targets.Length == 1)
            {
                GetVector3FromCopyBuffer(out exists, out float x, out float y, out float z);

                if (exists)
                {
                    pastePositionButton.SetEnabled(true);
                    pasteRotationButton.SetEnabled(true);
                    pasteScaleButton.SetEnabled(true);

                    if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
                    {
                        pastePositionButton.tooltip = "Paste " + x + "," + y + "," + z + " to local position.";
                        pasteRotationButton.tooltip = "Paste " + x + "," + y + "," + z + " to local rotation.";
                        pasteScaleButton.tooltip = "Paste " + x + "," + y + "," + z + " to local scale.";
                    }
                    else
                    {
                        pastePositionButton.tooltip = "Paste " + x + "," + y + "," + z + " to world position.";
                        pasteRotationButton.tooltip = "Paste " + x + "," + y + "," + z + " to world rotation.";
                        pasteScaleButton.tooltip = "Paste " + x + "," + y + "," + z + " to world scale.";
                    }
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

                    pastePositionButton.tooltip = "Paste " + valueString + "to " + editorSettings.CurrentWorkSpace + " position.";
                    pasteRotationButton.tooltip = "Paste " + valueString + "to " + editorSettings.CurrentWorkSpace + " rotation.";
                    pasteScaleButton.tooltip = "Paste " + valueString + "to " + editorSettings.CurrentWorkSpace + " scale.";
                }
            }

            if (!exists)
            {
                pastePositionButton.SetEnabled(false);
                pasteRotationButton.SetEnabled(false);
                pasteScaleButton.SetEnabled(false);

                pastePositionButton.tooltip = "A valid Value isn't copied";
                pasteRotationButton.tooltip = "A valid Value isn't copied";
                pasteScaleButton.tooltip = "A valid Value isn't copied";
            }
        }

        #region Workspace

        private Toggle sizeFoldoutToggle;

        private Label siblingIndexLabel;

        /// <summary>
        /// The local/global workspace button at the top of the transform
        /// </summary>
        /// <param name="root"></param>
        private void SetupWorkSpace()
        {
            worldSpaceButton = topGroupBox.Q<Button>("WorldSpaceButton");
            localSpaceButton = topGroupBox.Q<Button>("LocalSpaceButton");

            localSpaceLabel = topGroupBox.Q<Label>("LocalSpaceLabel");
            worldSpaceLabel = topGroupBox.Q<Label>("WorldSpaceLabel");

            //worldSpaceButton.clickable = null; //Not needed since this is called only once and at the beginning
            worldSpaceButton.clicked += () =>
            {
                editorSettings.CurrentWorkSpace = BetterTransformSettings.WorkSpace.Local;
                UpdateMainControls();
                UpdateSize();
            };
            //localSpaceButton.clickable = null; //Not needed since this is called only once and at the beginning
            localSpaceButton.clicked += () =>
            {
                editorSettings.CurrentWorkSpace = BetterTransformSettings.WorkSpace.World;
                UpdateMainControls();
                UpdateSize();
            };

            if (editorSettings.ShowSizeFoldout)
            {
                sizeFoldout ??= root.Q<GroupBox>("SizeFoldout");

                if (sizeFoldoutToggle == null)
                {
                    sizeFoldoutToggle = sizeFoldout.Q<Toggle>("FoldoutToggle");
                    sizeFoldoutToggle.tooltip = "World size calculates the size of an object based off of world axis.\n\n" +
                        "Local size is the size of the object in 0 angle local rotation. \n" +
                        "This can be impacted by it's parent's rotation and scale.\n" +
                        "Only local size is shown when both world space and local space is shown.";
                }
            }

            siblingIndexLabel = topGroupBox.Q<Label>("SiblingIndexLabel");

            if (editorSettings.showSiblingIndex && transform.parent)
            {
                UpdateSiblingIndex(transform, siblingIndexLabel);
            }
            else
            {
                siblingIndexLabel.style.display = DisplayStyle.None;
                //Debug.Log("Hiding sibling index");
            }
        }

        private void UpdateWorkSpaceButtons()
        {
            switch (editorSettings.CurrentWorkSpace)
            {
                case BetterTransformSettings.WorkSpace.Local:
                    worldSpaceButton.style.display = DisplayStyle.None;
                    localSpaceButton.style.display = DisplayStyle.Flex;

                    if (sizeFoldoutToggle != null)
                        sizeFoldoutToggle.text = "Local Size";

                    SceneView.RepaintAll();
                    break;

                case BetterTransformSettings.WorkSpace.World:
                    worldSpaceButton.style.display = DisplayStyle.Flex;
                    localSpaceButton.style.display = DisplayStyle.None;

                    if (sizeFoldoutToggle != null)
                        sizeFoldoutToggle.text = "World Size";

                    SceneView.RepaintAll();
                    break;

                case BetterTransformSettings.WorkSpace.Both:
                    localSpaceButton.style.display = DisplayStyle.None;
                    worldSpaceButton.style.display = DisplayStyle.None;

                    if (sizeFoldoutToggle != null)
                        sizeFoldoutToggle.text = "Local Size";

                    SceneView.RepaintAll();
                    break;
            }
        }

        private void UpdateWorldSpaceFields_WhenInWorldSpaceWorkspace()
        {
            if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                UpdateWorldSpaceFields();
        }

        private void UpdateWorldSpaceFields()
        {
            //Not sure why, but a user reported they received a null reference error for target here
            //Due to version difference, couldn't confirm the line.
            //Remove this later after further testing.
            if (transform == null)
                transform = target as Transform;

            if (transform == null)
                return;

            if (editorSettings.roundPositionField)
                worldPositionField.SetValueWithoutNotify(RoundedVector3v2(transform.position));
            else
                worldPositionField.SetValueWithoutNotify(transform.position);

            if (editorSettings.roundRotationField)
                worldRotationField.SetValueWithoutNotify(RoundedVector3v2(transform.eulerAngles));
            else
                worldRotationField.SetValueWithoutNotify(RoundedVector3(transform.eulerAngles));

            if (editorSettings.roundScaleField)
                worldScaleField.SetValueWithoutNotify(RoundedVector3v2(transform.lossyScale));
            else
                worldScaleField.SetValueWithoutNotify(transform.lossyScale);
        }

        #endregion Workspace

        #region Position

        private void SetupPosition()
        {
            positionGroupBox = customEditorGroupBox.Q<GroupBox>("Position");

            positionLabel = positionGroupBox.Q<Label>("PositionLabel");

            SetupPosition_fields();
            SetupPosition_buttons();

            positionPrefabOverrideMark = positionGroupBox.Q<VisualElement>("PrefabOverrideMark");
            positionDefaultPrefabOverrideMark = positionGroupBox.Q<VisualElement>("DefaultPrefabOverrideMark");
        }

        private void UpdatePosition()
        {
            UpdatePosition_label();

            UpdatePosition_fields();

            UpdatePosition_buttons();

            UpdatePosition_prefabOverrideIndicator();
        }

        private void UpdatePosition_label()
        {
            if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
                positionLabel.tooltip = "The local position of this GameObject relative to the parent.";
            else //If world space or both is chosen, the custom label will show world position
            {
                positionLabel.tooltip = "The world position of this GameObject.";
                if (targets.Length > 1)
                {
                    positionLabel.tooltip += "\n" + worldPositionReadOnlyTooltip;
                    positionLabel.SetEnabled(false);
                }
            }

            UpdatePositionLabelContextMenu();
        }

        /// <summary>
        /// The right click menu on the position label.
        /// </summary>
        private void UpdatePositionLabelContextMenu()
        {
            //Remove the old context menu
            if (contextualMenuManipulatorForPositionLabel != null)
                positionLabel.RemoveManipulator(contextualMenuManipulatorForPositionLabel);

            UpdateContextMenuForPosition();

            positionLabel.AddManipulator(contextualMenuManipulatorForPositionLabel);

            void UpdateContextMenuForPosition()
            {
                contextualMenuManipulatorForPositionLabel = new ContextualMenuManipulator((evt) =>
                {
                    evt.menu.AppendAction("Position :", (x) => { }, DropdownMenuAction.AlwaysDisabled);
                    evt.menu.AppendSeparator();

                    evt.menu.AppendAction("Copy property path", (x) => CopyPositionPropertyPath(), DropdownMenuAction.AlwaysEnabled);

                    if (editorSettings.roundPositionField)
                        evt.menu.AppendAction("Round out field values for the inspector", (x) => TogglePositionFieldRounding(), DropdownMenuAction.Status.Checked);
                    else
                        evt.menu.AppendAction("Round out field values for the inspector", (x) => TogglePositionFieldRounding(), DropdownMenuAction.Status.Normal);

                    if (HasPrefabOverride_position())
                    {
                        evt.menu.AppendSeparator();
                        if (HasPrefabOverride_position(true))
                            evt.menu.AppendAction("Apply to Prefab '" + PrefabUtility.GetCorrespondingObjectFromSource(transform.gameObject).name + "'", (x) => ApplyPositionChangeToPrefab(), DropdownMenuAction.AlwaysEnabled);
                        else
                            evt.menu.AppendAction("Apply to Prefab '" + PrefabUtility.GetCorrespondingObjectFromSource(transform.gameObject).name + "'", (x) => ApplyPositionChangeToPrefab(), DropdownMenuAction.AlwaysDisabled);

                        evt.menu.AppendAction("Revert", (x) => RevertPositionChangeToPrefab(), DropdownMenuAction.AlwaysEnabled);
                    }

                    evt.menu.AppendSeparator();

                    evt.menu.AppendAction("Copy", (x) => CopyPosition(), DropdownMenuAction.AlwaysEnabled);
                    GetVector3FromCopyBuffer(out bool exists, out float x, out float y, out float z);
                    if (exists)
                        evt.menu.AppendAction("Paste", (x) => PastePosition(), DropdownMenuAction.AlwaysEnabled);
                    else
                        evt.menu.AppendAction("Paste", (x) => PastePosition(), DropdownMenuAction.AlwaysDisabled);

                    evt.menu.AppendAction("Reset", (x) => ResetPosition(), DropdownMenuAction.AlwaysEnabled);
                });
            }

            void CopyPositionPropertyPath()
            {
                EditorGUIUtility.systemCopyBuffer = positionProperty;
            }

            void ApplyPositionChangeToPrefab()
            {
                if (soTarget.FindProperty(positionProperty).prefabOverride)
                {
                    PrefabUtility.ApplyPropertyOverride(soTarget.FindProperty(positionProperty), PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(transform), InteractionMode.UserAction);
                }
            }

            void RevertPositionChangeToPrefab()
            {
                if (soTarget.FindProperty("m_LocalPosition").prefabOverride)
                {
                    PrefabUtility.RevertPropertyOverride(soTarget.FindProperty(positionProperty), InteractionMode.UserAction);
                }
            }
        }

        private void TogglePositionFieldRounding()
        {
            editorSettings.roundPositionField = !editorSettings.roundPositionField;
            editorSettings.Save();

            if (editorSettings.roundPositionField)
            {
                localPositionField.SetValueWithoutNotify(RoundedVector3v2(transform.localPosition));
                worldPositionField.SetValueWithoutNotify(RoundedVector3v2(transform.position));
            }
            else
            {
                localPositionField.SetValueWithoutNotify(transform.localPosition);
                worldPositionField.SetValueWithoutNotify(transform.position);
            }

            UpdatePositionLabelContextMenu();

            if (roundPositionFieldToggle != null) roundPositionFieldToggle.SetValueWithoutNotify(editorSettings.roundPositionField);
        }

        /// <summary>
        /// This is the right click menu on the label
        /// </summary>
        private ContextualMenuManipulator contextualMenuManipulatorForPositionLabel;

        private HelpBox bigNumberWarning;
        private bool isPositionUpdatedByWorldField = false;

        private void SetupPosition_fields()
        {
            localPositionField = positionGroupBox.Q<Vector3Field>("LocalPosition");
            worldPositionField = positionGroupBox.Q<Vector3Field>("WorldPosition");

            if (targets.Length > 1)
            {
                worldPositionField.SetEnabled(false);
                worldPositionField.tooltip = worldPositionReadOnlyTooltip;
            }

            //Because the bound local position field updates this, the field needs to be re-rounded after a single frame to not be ignored when the binding updates this
            //that is done in the RegisterLocalPositionFieldValueChangedCallBack() method
            if (editorSettings.roundPositionField)
                localPositionField.SetValueWithoutNotify(RoundedVector3v2(transform.localPosition));

            //This makes sure the binding operation is done before the callback is registered to avoid it calling the change
            localPositionField.schedule.Execute(() => RegisterLocalPositionFieldValueChangedCallBack());

            worldPositionField.RegisterValueChangedCallback(ev =>
            {
                //This doesn't work with the recorder: //Undo isn't required here because the transform position update will record the Undo
                Undo.RecordObject(transform, "Position change on " + transform.gameObject.name);

                isPositionUpdatedByWorldField = true;
                transform.position = ev.newValue;

                if (editorSettings.roundPositionField)
                    worldPositionField.SetValueWithoutNotify(RoundedVector3v2(ev.newValue));
            });

            bigNumberWarning = root.Q<HelpBox>("BigNumberWarning");
        }

        private void RegisterLocalPositionFieldValueChangedCallBack()
        {
            //This is also called by world position field update
            localPositionField.RegisterValueChangedCallback(ev =>
            {
                if (!isPositionUpdatedByWorldField)
                {
                    UpdateWorldPositionField();
                }

                //Debug.Log(ev.newValue.ToString("F20"));

                Undo.RecordObject(transform, "Position change on " + transform.gameObject.name);

                soTarget.Update();
                UpdatePosition_prefabOverrideIndicator();
                UpdatePosition_label();
                UpdateSize();
                UpdateWarningIfRequired();

                if (editorSettings.roundPositionField)
                    localPositionField.SetValueWithoutNotify(RoundedVector3v2(ev.newValue));

                localPositionField.schedule.Execute(() => UpdateAnimatorState_PositionFields()).ExecuteLater(100); //1000 ms = 1 s
            });

            //Because the bound local position field updates this, the field needs to be rounded after a single frame to not be ignored when the binding updates this
            if (editorSettings.roundPositionField)
                localPositionField.SetValueWithoutNotify(RoundedVector3v2(transform.localPosition));
        }

        private void UpdateWorldPositionField()
        {
            if (editorSettings.roundPositionField)
                worldPositionField.SetValueWithoutNotify(RoundedVector3v2(transform.position));
            else
                worldPositionField.SetValueWithoutNotify(transform.position);

            if (targets.Length == 1)
                return;

            float commonX = transform.position.x;
            bool isCommonX = true;
            float commonY = transform.position.y;
            bool isCommonY = true;
            float commonZ = transform.position.z;
            bool isCommonZ = true;

            foreach (Transform t in targets.Cast<Transform>())
            {
                if (isCommonX)
                    if (t.position.x != commonX)
                        isCommonX = false;

                if (isCommonY)
                    if (t.position.y != commonY)
                        isCommonY = false;

                if (isCommonZ)
                    if (t.position.z != commonZ)
                        isCommonZ = false;

                if (!isCommonX && !isCommonY && !isCommonZ)
                    break;
            }

            var xField = worldPositionField.Q<FloatField>("unity-x-input");
            if (!isCommonX)
                xField.showMixedValue = true;
            else
            {
                xField.showMixedValue = false;
                //xField.RemoveFromClassList(mixedValueLabelClass);
            }

            var yField = worldPositionField.Q<FloatField>("unity-y-input");
            if (!isCommonY)
                yField.showMixedValue = true;
            else
            {
                yField.showMixedValue = false;
                //yField.RemoveFromClassList(mixedValueLabelClass);
                //yField.value = transform.position.y;
            }

            var zField = worldPositionField.Q<FloatField>("unity-z-input");
            if (!isCommonZ)
                zField.showMixedValue = true;
            else
                zField.showMixedValue = false;
        }

        private void UpdatePosition_fields()
        {
            UpdateWorldPositionField();
            //worldPositionField.SetValueWithoutNotify(transform.position);

            //Don't need to set local position field because it is a bound field created in the UIBuilder

            switch (editorSettings.CurrentWorkSpace)
            {
                case BetterTransformSettings.WorkSpace.Local:
                    localPositionField.style.display = DisplayStyle.Flex;
                    worldPositionField.style.display = DisplayStyle.None;
                    break;

                case BetterTransformSettings.WorkSpace.World:
                    localPositionField.style.display = DisplayStyle.None;
                    worldPositionField.style.display = DisplayStyle.Flex;
                    break;

                case BetterTransformSettings.WorkSpace.Both:
                    localPositionField.style.display = DisplayStyle.None; //The default inspector will be used to show local fields
                    worldPositionField.style.display = DisplayStyle.Flex;
                    break;
            }

            UpdateWarningIfRequired();
        }

        private void UpdateWarningIfRequired()
        {
            if (Mathf.Abs(transform.position.x) > 100000 || Mathf.Abs(transform.position.y) > 100000 || Mathf.Abs(transform.position.z) > 100000)
            {
                if (bigNumberWarning != null)
                    bigNumberWarning.style.display = DisplayStyle.Flex;
                else
                    CreateBigNumberWarning();
            }
            else
            {
                if (bigNumberWarning != null)
                    bigNumberWarning.style.display = DisplayStyle.None;
            }
        }

        private void CreateBigNumberWarning()
        {
            bigNumberWarning = new HelpBox("Due to floating-point precision limitations, it is recommended to bring the world coordinates within a smaller range.", HelpBoxMessageType.Warning);
            root.Add(bigNumberWarning);
        }

        private void SetupPosition_buttons()
        {
            var positionToolbar = toolbarGroupBox.Q<GroupBox>("PositionToolbar");
            copyPositionButton = positionToolbar.Q<Button>("Copy");
            pastePositionButton = positionToolbar.Q<Button>("Paste");
            resetPositionButton = positionToolbar.Q<Button>("Reset");

            copyPositionButton.clicked += () =>
            {
                CopyPosition();
            };
            pastePositionButton.clicked += () =>
            {
                PastePosition();
            };
            resetPositionButton.clicked += () =>
            {
                ResetPosition();
            };
        }

        private void ResetPosition()
        {
            if (targets.Length == 1)
            {
                Undo.RecordObject(transform, "Reset position of " + transform.gameObject.name);
                if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
                    transform.localPosition = Vector3.zero;
                else
                    transform.position = Vector3.zero;
                EditorUtility.SetDirty(transform);
            }
            else
            {
                Undo.RecordObjects(targets, "Reset positions.");
                foreach (Transform t in targets.Cast<Transform>())
                {
                    if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                        t.position = Vector3.zero;
                    else
                        t.localPosition = Vector3.zero;
                    EditorUtility.SetDirty(t);
                }

                UpdateWorldPositionField();
            }
        }

        private void UpdatePosition_buttons()
        {
            copyPositionButton.tooltip = "Copy " + GetCurrentWorkspaceForToolbar() + " position.";
            pastePositionButton.tooltip = "Paste to " + GetCurrentWorkspaceForToolbar() + " position.";
            resetPositionButton.tooltip = "Reset " + GetCurrentWorkspaceForToolbar() + " position to zero.";
        }

        private void UpdatePosition_prefabOverrideIndicator()
        {
            if (!HasPrefabOverride_position())
            {
                positionPrefabOverrideMark.style.display = DisplayStyle.None;
                positionDefaultPrefabOverrideMark.style.display = DisplayStyle.None;

                positionLabel.RemoveFromClassList(prefabOverrideLabel);
                worldPositionField.RemoveFromClassList(prefabOverrideLabel);
            }
            else
            {
                if (!HasPrefabOverride_position(true))
                {
                    positionDefaultPrefabOverrideMark.style.display = DisplayStyle.Flex;
                    positionPrefabOverrideMark.style.display = DisplayStyle.None;
                }
                else
                {
                    positionPrefabOverrideMark.style.display = DisplayStyle.Flex;
                    positionDefaultPrefabOverrideMark.style.display = DisplayStyle.None;
                }
                positionLabel.AddToClassList(prefabOverrideLabel);
                worldPositionField.AddToClassList(prefabOverrideLabel);
            }
        }

        private void CopyPosition()
        {
            if (targets.Length == 1)
            {
                if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
                    EditorGUIUtility.systemCopyBuffer = "Vector3" + transform.localPosition.ToString("F20");
                else
                    EditorGUIUtility.systemCopyBuffer = "Vector3" + transform.position.ToString("F20");

                //Debug.Log(transform.position.ToString("F20"));
                UpdatePasteButtons();
            }
            else //Copying multiple targets
            {
                CopyMultipleSelectToBuffer_position();
            }
        }

        private void PastePosition()
        {
            if (targets.Length == 1)
            {
                GetVector3FromCopyBuffer(out bool exists, out float x, out float y, out float z);
                if (!exists)
                    return;

                Undo.RecordObject(transform, "Position Paste on " + transform.gameObject.name);

                if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
                    transform.localPosition = new Vector3(x, y, z);
                else
                    transform.position = new Vector3(x, y, z);

                EditorUtility.SetDirty(transform);
            }
            else
            {
                GetVector3ListFromCopyBuffer(out bool exists, out List<string> values);
                //values.Reverse();

                if (!exists) return;

                var transforms = targets.Cast<Transform>().ToList();

                //for (int i = transforms.Count() - 1; i >= 0; i--)
                for (int i = 0; i < transforms.Count; i++)
                {
                    if (values.Count <= i)
                        break;

                    var value = GetVector3FromString(values[i], out bool exists2);

                    if (!exists2) continue;

                    Undo.RecordObject(transforms[i], "Position Paste on " + transforms[i].gameObject.name);

                    if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                        transforms[i].position = value;
                    else
                        transforms[i].localPosition = value;

                    EditorUtility.SetDirty(transforms[i]);
                }
            }
        }

        /// <summary>
        ///
        ///
        /// </summary>
        /// <param name="checkDefaultOverride">
        /// Certain properties on the root GameObject of a Prefab instance are considered default overrides.
        /// These are overridden by default and are usually rarely applied or reverted.
        /// Most apply and revert operations will ignore default overrides.
        /// https://docs.unity3d.com/ScriptReference/PrefabUtility.IsDefaultOverride.html
        /// </param>
        /// <returns></returns>
        private bool HasPrefabOverride_position(bool checkDefaultOverride = false)
        {
            if (soTarget.FindProperty(positionProperty).prefabOverride)
            {
                if (checkDefaultOverride)
                    if (soTarget.FindProperty(positionProperty).isDefaultOverride)
                        return false;
                return true;
            }
            else
                return false;
        }

        #endregion Position

        #region Rotation

        private void SetupRotation()
        {
            rotationGroupBox = customEditorGroupBox.Q<GroupBox>("Rotation");
            rotationLabel = rotationGroupBox.Q<Label>("RotationLabel");

            SetupRotation_fields();
            SetupRotation_buttons();

            rotationPrefabOverrideMark = rotationGroupBox.Q<VisualElement>("PrefabOverrideMark");
            rotationDefaultPrefabOverrideMark = rotationGroupBox.Q<VisualElement>("DefaultPrefabOverrideMark");
        }

        private void UpdateRotation()
        {
            UpdateRotation_label();
            UpdateRotation_fields();
            UpdateRotation_buttons();

            UpdateRotation_prefabOverrideIndicator();
        }

        private void UpdateRotation_label()
        {
            if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
                rotationLabel.tooltip = "The local rotation of this GameObject relative to the parent.\n\n" +
                    "Unity uses quaternions to store rotations, but displays them as Euler angles in the Inspector to make it easier for people to use.\n\n" +
                    "An internal editor only property is used to store the value you set in the field.";
            else
            {
                rotationLabel.tooltip = "The world rotation of this GameObject.\n\n" +
                    "Unity uses quaternions internally to store rotations, but displays them as Euler angles in the Inspector to make it easier for people to use.\n\n" +
                    "The value you set in the field for global rotation isn't saved anywhere. " +
                    "That's why it is retrieved from the quaternion rotation of the transform and although it is effectively the value you set, it can often look different.";

                if (targets.Length > 1)
                {
                    rotationLabel.tooltip += "\n" + worldRotationReadOnlyTooltip;
                    rotationLabel.SetEnabled(false);
                }
            }
            UpdateRotationLabelContextMenu();
        }

        private void UpdateRotationLabelContextMenu()
        {
            if (contextualMenuManipulatorForRotationLabel != null)
                rotationLabel.RemoveManipulator(contextualMenuManipulatorForRotationLabel);

            UpdateContextMenuForRotation();

            rotationLabel.AddManipulator(contextualMenuManipulatorForRotationLabel);

            void UpdateContextMenuForRotation()
            {
                contextualMenuManipulatorForRotationLabel = new ContextualMenuManipulator((evt) =>
                {
                    evt.menu.AppendAction("Rotation :", (x) => { }, DropdownMenuAction.AlwaysDisabled);
                    evt.menu.AppendSeparator();

                    evt.menu.AppendAction("Copy property path", (x) => CopyRotationPropertyPath(), DropdownMenuAction.AlwaysEnabled);

                    if (editorSettings.roundRotationField)
                        evt.menu.AppendAction("Round out field values for the inspector", (x) => ToggleRotationFieldRounding(), DropdownMenuAction.Status.Checked);
                    else
                        evt.menu.AppendAction("Round out field values for the inspector", (x) => ToggleRotationFieldRounding(), DropdownMenuAction.Status.Normal);

                    if (HasPrefabOverride_rotation())
                    {
                        evt.menu.AppendSeparator();
                        if (HasPrefabOverride_rotation(true))
                            evt.menu.AppendAction("Apply to Prefab '" + PrefabUtility.GetCorrespondingObjectFromSource(transform.gameObject).name + "'", (x) => ApplyRotationChangeToPrefab(), DropdownMenuAction.AlwaysEnabled);
                        else
                            evt.menu.AppendAction("Apply to Prefab '" + PrefabUtility.GetCorrespondingObjectFromSource(transform.gameObject).name + "'", (x) => ApplyRotationChangeToPrefab(), DropdownMenuAction.AlwaysDisabled);

                        evt.menu.AppendAction("Revert", (x) => RevertRotationChangeToPrefab(), DropdownMenuAction.AlwaysEnabled);
                    }

                    evt.menu.AppendSeparator();

                    evt.menu.AppendAction("Copy Euler Angles", (x) => CopyRotationEulerAngles(), DropdownMenuAction.AlwaysEnabled);
                    evt.menu.AppendAction("Copy Quaternion", (x) => CopyRotationQuaternion(), DropdownMenuAction.AlwaysEnabled);

                    GetVector3FromCopyBuffer(out bool exists, out float x, out float y, out float z);
                    GetQuaternionFromCopyBuffer(out bool quaternionExists, out float qx, out float qy, out float qz, out float qw);
                    if (exists || quaternionExists)
                        evt.menu.AppendAction("Paste", (x) => PasteRotation(), DropdownMenuAction.AlwaysEnabled);
                    else
                        evt.menu.AppendAction("Paste", (x) => PasteRotation(), DropdownMenuAction.AlwaysDisabled);

                    evt.menu.AppendAction("Reset", (x) => ResetRotation(), DropdownMenuAction.AlwaysEnabled);
                });
            }

            void CopyRotationPropertyPath()
            {
                EditorGUIUtility.systemCopyBuffer = rotationProperty;
            }

            void ApplyRotationChangeToPrefab()
            {
                if (soTarget.FindProperty(rotationProperty).prefabOverride)
                {
                    PrefabUtility.ApplyPropertyOverride(soTarget.FindProperty(rotationProperty), PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(transform), InteractionMode.UserAction);
                }
            }

            void RevertRotationChangeToPrefab()
            {
                if (soTarget.FindProperty(rotationProperty).prefabOverride)
                {
                    PrefabUtility.RevertPropertyOverride(soTarget.FindProperty(rotationProperty), InteractionMode.UserAction);
                }
            }
        }

        private void ToggleRotationFieldRounding()
        {
            editorSettings.roundRotationField = !editorSettings.roundRotationField;
            editorSettings.Save();

            if (editorSettings.roundRotationField)
            {
                localRotationField.SetValueWithoutNotify(RoundedVector3v2(serializedEulerHint.vector3Value));
                worldRotationField.SetValueWithoutNotify(RoundedVector3v2(transform.eulerAngles));
            }
            else
            {
                localRotationField.SetValueWithoutNotify(serializedEulerHint.vector3Value);
                worldRotationField.SetValueWithoutNotify(transform.eulerAngles);
            }

            UpdateRotationLabelContextMenu();
            if (roundRotationFieldToggle != null) roundRotationFieldToggle.SetValueWithoutNotify(editorSettings.roundRotationField);
        }

        /// <summary>
        /// This is the right click menu on the label
        /// </summary>
        private ContextualMenuManipulator contextualMenuManipulatorForRotationLabel;

        private void CopyRotationEulerAngles()
        {
            if (targets.Length == 1)
            {
                if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
                    EditorGUIUtility.systemCopyBuffer = "Vector3" + transform.localEulerAngles.ToString("F20"); //This used to be :myTarget.localRotation.eulerAngles.ToString(). Is there any difference?
                else
                    EditorGUIUtility.systemCopyBuffer = "Vector3" + transform.eulerAngles.ToString("F20");
            }
            else
            {
                CopyMultipleSelectToBuffer_rotation();
            }
            UpdatePasteButtons();
        }

        private void CopyRotationQuaternion()
        {
            if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                EditorGUIUtility.systemCopyBuffer = "Quaternion" + transform.rotation.ToString("F20");
            else
                EditorGUIUtility.systemCopyBuffer = "Quaternion" + transform.localRotation.ToString("F20");

            UpdateMainControls();

            UpdatePasteButtons();
        }

        private void PasteRotation()
        {
            if (targets.Length == 1)
            {
                GetVector3FromCopyBuffer(out bool exists, out float x, out float y, out float z);
                if (exists)
                {
                    Undo.RecordObject(transform, "Rotation Paste on " + transform.gameObject.name);

                    //These three shouldn't be needed but were added because for some reason,
                    //the world rotation field were not being updated without them because isRotationUpdatedByWorldField was true when it shouldn't be
                    temporarilyRotatedToCheckSize = false;
                    isRotationUpdatedByWorldField = false;
                    isRotateUpdatedByLocalField = false;

                    if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
                        transform.localRotation = Quaternion.Euler(x, y, z);
                    else
                        transform.rotation = Quaternion.Euler(x, y, z);

                    //soTarget.Update();
                    EditorUtility.SetDirty(transform);
                }

                GetQuaternionFromCopyBuffer(out bool quaternionExists, out float qx, out float qy, out float qz, out float qw);
                if (quaternionExists)
                {
                    Undo.RecordObject(transform, "Rotation Quaternion Paste on " + transform.gameObject.name);

                    //These three shouldn't be needed but were added because for some reason,
                    //the world rotation field were not being updated without them because isRotationUpdatedByWorldField was true when it shouldn't be
                    temporarilyRotatedToCheckSize = false;
                    isRotationUpdatedByWorldField = false;
                    isRotateUpdatedByLocalField = false;

                    if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                        transform.rotation = new Quaternion(qx, qy, qz, qw);
                    else
                        transform.localRotation = new Quaternion(qx, qy, qz, qw);

                    soTarget.Update();
                    EditorUtility.SetDirty(transform);
                }
            }
            else
            {
                GetVector3ListFromCopyBuffer(out bool exists, out List<string> values);
                //values.Reverse();

                if (!exists) return;

                var transforms = targets.Cast<Transform>().ToList();

                //for (int i = transforms.Count() - 1; i >= 0; i--)
                for (int i = 0; i < transforms.Count; i++)
                {
                    if (values.Count <= i)
                        break;

                    var value = GetVector3FromString(values[i], out bool exists2);

                    if (!exists2) continue;

                    Undo.RecordObject(transforms[i], "Rotation Paste on " + transforms[i].gameObject.name);

                    //These three shouldn't be needed but were added because for some reason,
                    //the world rotation field were not being updated without them because isRotationUpdatedByWorldField was true when it shouldn't be
                    temporarilyRotatedToCheckSize = false;
                    isRotationUpdatedByWorldField = false;
                    isRotateUpdatedByLocalField = false;

                    if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                        transforms[i].rotation = Quaternion.Euler(value.x, value.y, value.z);
                    else
                        transforms[i].localRotation = Quaternion.Euler(value.x, value.y, value.z);

                    //soTarget.Update();
                    EditorUtility.SetDirty(transform);
                }
            }
        }

        private bool isRotateUpdatedByLocalField = false;
        private bool isRotationUpdatedByWorldField = false;

        private void SetupRotation_fields()
        {
            serializedEulerHint = soTarget.FindProperty("m_LocalEulerAnglesHint");

            localRotationField = rotationGroupBox.Q<Vector3Field>("LocalRotation");

            if (editorSettings.roundRotationField)
                localRotationField.SetValueWithoutNotify(RoundedVector3v2(serializedEulerHint.vector3Value));
            else
                localRotationField.SetValueWithoutNotify(RoundedVector3(serializedEulerHint.vector3Value));

            //Setting the fields in the codes above should be unnecessary. Remove them later after testing.
            localRotationField.schedule.Execute(() => ScheduleUpdateRotationField()).ExecuteLater(0);

            localRotationField.RegisterValueChangedCallback(ev =>
            {
                Undo.RecordObject(transform, "Rotation change on " + transform.gameObject.name);

                isRotateUpdatedByLocalField = true;

                serializedEulerHint.vector3Value = ev.newValue; //This doesn't change the rotation
                soTarget.ApplyModifiedProperties(); //Can't update rotation if this is called after setting transform.localrotaion or before setting serializedEulerHint

                transform.localRotation = Quaternion.Euler(ev.newValue);

                UpdateRotation_prefabOverrideIndicator();

                if (editorSettings.roundRotationField)
                    localRotationField.SetValueWithoutNotify(RoundedVector3v2(ev.newValue));
            });

            worldRotationField = rotationGroupBox.Q<Vector3Field>("WorldRotation");
            if (targets.Length > 1)
            {
                worldRotationField.SetEnabled(false);
                worldRotationField.tooltip = worldRotationReadOnlyTooltip;
            }
            worldRotationField.RegisterValueChangedCallback(ev =>
            {
                isRotationUpdatedByWorldField = true;

                Undo.RecordObject(transform, "Rotation change on " + transform.gameObject.name);
                transform.eulerAngles = ev.newValue;
                //The field are updated by the quaternionRotation
            });

            quaternionRotationPropertyField = root.Q<PropertyField>("QuaternionRotation");

            //This is the hidden rotation field which tracks the actual rotation.
            rotationSerializedProperty = soTarget.FindProperty(rotationProperty);
            quaternionRotationPropertyField.TrackPropertyValue(rotationSerializedProperty, RotationUpdated);
        }

        /// <summary>
        /// This is only called once during setup.
        /// This is called after a single frame update to overwrite the binding's value update and apply rounding if required
        /// </summary>
        private void ScheduleUpdateRotationField()
        {
            if (editorSettings.roundRotationField)
                localRotationField.SetValueWithoutNotify(RoundedVector3v2(serializedEulerHint.vector3Value));
            else
                localRotationField.SetValueWithoutNotify(RoundedVector3(serializedEulerHint.vector3Value));
        }

        private Vector3 rotationVector3Cached;

        /// <summary>
        /// This is called only if the rotation is updated by code
        /// </summary>
        /// <param name="property"></param>
        private void RotationUpdated(SerializedProperty property)
        {
            if (temporarilyRotatedToCheckSize)
            {
                temporarilyRotatedToCheckSize = false;
                //This is required because of the Undo function.
                if (rotationVector3Cached == RoundedVector3(transform.localRotation.eulerAngles))
                    return;
            }

            //First update the target
            soTarget.ApplyModifiedProperties();
            soTarget.Update();

            rotationVector3Cached = RoundedVector3(transform.localRotation.eulerAngles);

            //Then update fields
            if (!isRotateUpdatedByLocalField)
            {
                if (editorSettings.roundRotationField)
                    localRotationField.SetValueWithoutNotify(RoundedVector3v2(rotationVector3Cached));
                else
                    localRotationField.SetValueWithoutNotify(rotationVector3Cached);

                serializedEulerHint.vector3Value = rotationVector3Cached;
            }
            else
                isRotateUpdatedByLocalField = false;

            if (!isRotationUpdatedByWorldField)
                UpdateWorldRotationField();
            //worldRotationField.SetValueWithoutNotify(RoundedVector3(transform.eulerAngles));
            else
                isRotationUpdatedByWorldField = false;

            UpdateRotation_label();
            UpdateRotation_prefabOverrideIndicator();

            UpdateSize();

            quaternionRotationPropertyField.schedule.Execute(() => UpdateAnimatorState_RotationFields()).ExecuteLater(100); //1000 ms = 1 s
        }

        private void UpdateWorldRotationField()
        {
            if (editorSettings.roundRotationField)
                worldRotationField.SetValueWithoutNotify(RoundedVector3v2(transform.eulerAngles));
            else
                worldRotationField.SetValueWithoutNotify(RoundedVector3(transform.eulerAngles));

            if (targets.Length == 1)
                return;

            float commonX = transform.rotation.x;
            bool isCommonX = true;
            float commonY = transform.rotation.y;
            bool isCommonY = true;
            float commonZ = transform.rotation.z;
            bool isCommonZ = true;

            foreach (Transform t in targets.Cast<Transform>())
            {
                if (isCommonX)
                    if (t.rotation.x != commonX)
                        isCommonX = false;

                if (isCommonY)
                    if (t.rotation.y != commonY)
                        isCommonY = false;

                if (isCommonZ)
                    if (t.rotation.z != commonZ)
                        isCommonZ = false;

                if (!isCommonX && !isCommonY && !isCommonZ)
                    break;
            }

            var xField = worldRotationField.Q<FloatField>("unity-x-input");
            if (!isCommonX)
                xField.showMixedValue = true;
            else
                xField.showMixedValue = false;

            var yField = worldRotationField.Q<FloatField>("unity-y-input");
            if (!isCommonY)
                yField.showMixedValue = true;
            else
                yField.showMixedValue = false;

            var zField = worldRotationField.Q<FloatField>("unity-z-input");
            if (!isCommonZ)
                zField.showMixedValue = true;
            else
                zField.showMixedValue = false;
        }

        private void UpdateRotation_fields()
        {
            UpdateWorldRotationField();
            //worldRotationField.SetValueWithoutNotify(transform.eulerAngles);

            switch (editorSettings.CurrentWorkSpace)
            {
                case BetterTransformSettings.WorkSpace.Local:
                    localRotationField.style.display = DisplayStyle.Flex;
                    worldRotationField.style.display = DisplayStyle.None;
                    break;

                case BetterTransformSettings.WorkSpace.World:
                    worldRotationField.style.display = DisplayStyle.Flex;
                    localRotationField.style.display = DisplayStyle.None;
                    break;

                case BetterTransformSettings.WorkSpace.Both:
                    worldRotationField.style.display = DisplayStyle.Flex;
                    localRotationField.style.display = DisplayStyle.None;
                    break;
            }
        }

        private void SetupRotation_buttons()
        {
            var rotationToolbar = toolbarGroupBox.Q<GroupBox>("RotationToolbar");
            copyRotationButton = rotationToolbar.Q<Button>("Copy");
            pasteRotationButton = rotationToolbar.Q<Button>("Paste");
            resetRotationButton = rotationToolbar.Q<Button>("Reset");

            copyRotationButton.clicked += () =>
            {
                CopyRotationEulerAngles();
            };
            pasteRotationButton.clicked += () =>
            {
                PasteRotation();
            };
            resetRotationButton.clicked += () =>
            {
                ResetRotation();
            };
        }

        private void ResetRotation()
        {
            if (targets.Length == 1)
            {
                Undo.RecordObject(transform, "Reset rotation of " + transform.gameObject.name);
                if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
                {
                    isRotateUpdatedByLocalField = true;
                    localRotationField.SetValueWithoutNotify(Vector3.zero);

                    serializedEulerHint.vector3Value = Vector3.zero;
                    soTarget.ApplyModifiedProperties();
                    transform.localRotation = Quaternion.Euler(Vector3.zero);
                }
                else
                {
                    //These three shouldn't be needed but were added because for some reason,
                    //the world rotation field were not being updated without them because isRotationUpdatedByWorldField was true when it shouldn't be
                    temporarilyRotatedToCheckSize = false;
                    isRotationUpdatedByWorldField = false;
                    isRotateUpdatedByLocalField = false;

                    transform.eulerAngles = Vector3.zero;
                }
                EditorUtility.SetDirty(transform);
            }
            else
            {
                foreach (Transform t in targets.Cast<Transform>())
                {
                    Undo.RecordObject(t, "Reset rotation of " + t.gameObject.name);
                    if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                    {
                        //These three shouldn't be needed but were added because for some reason,
                        //the world rotation field were not being updated without them because isRotationUpdatedByWorldField was true when it shouldn't be
                        temporarilyRotatedToCheckSize = false;
                        isRotationUpdatedByWorldField = false;
                        isRotateUpdatedByLocalField = false;

                        t.eulerAngles = Vector3.zero;
                    }
                    else
                    {
                        isRotateUpdatedByLocalField = true;
                        localRotationField.SetValueWithoutNotify(Vector3.zero);

                        serializedEulerHint.vector3Value = Vector3.zero;
                        soTarget.ApplyModifiedProperties();
                        t.localRotation = Quaternion.Euler(Vector3.zero);
                    }
                    EditorUtility.SetDirty(t);
                }

                UpdateWorldRotationField();
            }
        }

        private void UpdateRotation_buttons()
        {
            copyRotationButton.tooltip = "Copy " + GetCurrentWorkspaceForToolbar() + " Euler Angles rotation";
            pasteRotationButton.tooltip = "Paste to " + GetCurrentWorkspaceForToolbar() + " Euler Angles rotation";
            resetRotationButton.tooltip = "Reset " + GetCurrentWorkspaceForToolbar() + " Euler Angles rotation to zero.";
        }

        private void UpdateRotation_prefabOverrideIndicator()
        {
            if (!HasPrefabOverride_rotation())
            {
                rotationPrefabOverrideMark.style.display = DisplayStyle.None;
                rotationDefaultPrefabOverrideMark.style.display = DisplayStyle.None;

                rotationLabel.RemoveFromClassList(prefabOverrideLabel);
                localRotationField.RemoveFromClassList(prefabOverrideLabel);
                worldRotationField.RemoveFromClassList(prefabOverrideLabel);
            }
            else
            {
                if (!HasPrefabOverride_position(true) && PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject))
                {
                    rotationDefaultPrefabOverrideMark.style.display = DisplayStyle.Flex;
                    rotationPrefabOverrideMark.style.display = DisplayStyle.None;
                }
                else
                {
                    rotationDefaultPrefabOverrideMark.style.display = DisplayStyle.None;
                    rotationPrefabOverrideMark.style.display = DisplayStyle.Flex;
                }

                rotationLabel.AddToClassList(prefabOverrideLabel);
                localRotationField.AddToClassList(prefabOverrideLabel);
                worldRotationField.AddToClassList(prefabOverrideLabel);
            }
        }

        private bool HasPrefabOverride_rotation(bool checkDefaultOverride = false)
        {
            if (soTarget.FindProperty(rotationProperty).prefabOverride)
            {
                if (checkDefaultOverride)
                    if (soTarget.FindProperty(rotationProperty).isDefaultOverride)
                        return false;
                return true;
            }
            else
                return false;
        }

        #endregion Rotation

        #region Scale

        private void SetupScale()
        {
            scaleGroupBox = customEditorGroupBox.Q<GroupBox>("Scale");
            scaleLabelGroupbox = scaleGroupBox.Q<GroupBox>("ScaleLabelGroupbox");
            scaleLabel = scaleGroupBox.Q<Label>("ScaleLabel");

            SetupScale_fields();
            SetupScale_buttons();

            scalePrefabOverrideMark = scaleGroupBox.Q<VisualElement>("PrefabOverrideMark");

            //m_ConstrainProportionsScaleProperty = serializedObject.FindProperty("m_ConstrainProportionsScale");

            scaleAspectRatioLocked = scaleGroupBox.Q<Button>("AspectRatioLocked");
            scaleAspectRatioLocked.clicked += () =>
            {
                editorSettings.LockSizeAspectRatio = false;
                //m_ConstrainProportionsScaleProperty.boolValue = false;
                UpdateScaleAspectRationButton();
                UpdateSize_AspectRationButton();
            };
            scaleAspectRatioUnlocked = scaleGroupBox.Q<Button>("AspectRatioUnlocked");
            scaleAspectRatioUnlocked.clicked += () =>
            {
                editorSettings.LockSizeAspectRatio = true;
                //m_ConstrainProportionsScaleProperty.boolValue = true;
                UpdateScaleAspectRationButton();
                UpdateSize_AspectRationButton();
            };

            UpdateScaleAspectRationButton();
        }

        private void SetupScale_fields()
        {
            localScaleField = scaleGroupBox.Q<Vector3Field>("LocalScale");
            worldScaleField = scaleGroupBox.Q<Vector3Field>("LossyScale");

            if (targets.Length > 1)
            {
                worldScaleField.SetEnabled(false);
                worldScaleField.tooltip = worldScaleReadOnlyTooltip;
            }

            localScaleField.RegisterValueChangedCallback(ev =>
            {
                SetLocalScale(ev.newValue);
            });

            worldScaleField.RegisterValueChangedCallback(ev =>
            {
                SetGlobalScale(transform, ev.newValue);
            });

            boundLocalScaleField = scaleGroupBox.Q<Vector3Field>("BoundLocalScale");
            //This makes sure the binding operation is done before the callback is registered to avoid it calling the change
            boundLocalScaleField.schedule.Execute(() => RegisterBoundLocalPositionFieldValueChangedCallBack());
            //RegisterBoundLocalPositionFieldValueChangedCallBack();

            scaleBeingUpdatedBySize = false;
        }

        private void RegisterBoundLocalPositionFieldValueChangedCallBack()
        {
            boundLocalScaleField.RegisterValueChangedCallback((EventCallback<ChangeEvent<Vector3>>)(ev =>
            {
                if (ev.newValue != ev.previousValue)
                {
                    Undo.RecordObject(transform, "Scale change on " + transform.gameObject.name);

                    if (editorSettings.roundScaleField)
                        localScaleField.SetValueWithoutNotify(RoundedVector3v2(transform.localScale));
                    else
                        localScaleField.SetValueWithoutNotify(RoundedVector3(transform.localScale));

                    SetWorldScaleField();

                    soTarget.Update();
                    UpdateScale_prefabOverrideIndicator();
                    UpdateScale_label();
                    EditorUtility.SetDirty(transform);

                    if (!scaleBeingUpdatedBySize)
                        UpdateSize();

                    scaleBeingUpdatedBySize = false;

                    boundLocalScaleField.schedule.Execute(() => UpdateAnimatorState_ScaleFields()).ExecuteLater(100); //1000 ms = 1 s
                }
            }));
        }

        private void SetWorldScaleField()
        {
            if (editorSettings.roundScaleField)
                worldScaleField.SetValueWithoutNotify(RoundedVector3v2(transform.lossyScale));
            else
                worldScaleField.SetValueWithoutNotify(RoundedVector3(transform.lossyScale));

            if (targets.Length == 1)
                return;

            float commonX = transform.lossyScale.x;
            bool isCommonX = true;
            float commonY = transform.lossyScale.y;
            bool isCommonY = true;
            float commonZ = transform.lossyScale.z;
            bool isCommonZ = true;

            foreach (Transform t in targets.Cast<Transform>())
            {
                if (isCommonX)
                    if (t.lossyScale.x != commonX)
                        isCommonX = false;

                if (isCommonY)
                    if (t.lossyScale.y != commonY)
                        isCommonY = false;

                if (isCommonZ)
                    if (t.lossyScale.z != commonZ)
                        isCommonZ = false;

                if (!isCommonX && !isCommonY && !isCommonZ)
                    break;
            }

            var xField = worldScaleField.Q<FloatField>("unity-x-input");
            if (!isCommonX)
                xField.showMixedValue = true;
            else
                xField.showMixedValue = false;

            var yField = worldScaleField.Q<FloatField>("unity-y-input");
            if (!isCommonY)
                yField.showMixedValue = true;
            else
                yField.showMixedValue = false;

            var zField = worldScaleField.Q<FloatField>("unity-z-input");
            if (!isCommonZ)
                zField.showMixedValue = true;
            else
                zField.showMixedValue = false;
        }

        private void SetupScale_buttons()
        {
            var scaleToolbar = toolbarGroupBox.Q<GroupBox>("ScaleToolbar");
            copyScaleButton = scaleToolbar.Q<Button>("Copy");
            pasteScaleButton = scaleToolbar.Q<Button>("Paste");
            resetScaleButton = scaleToolbar.Q<Button>("Reset");

            copyScaleButton.clicked += () =>
            {
                CopyScale();
            };
            pasteScaleButton.clicked += () =>
            {
                PasteScale();
            };
            resetScaleButton.clicked += () =>
            {
                ResetScale();
            };
        }

        private void ResetScale()
        {
            if (targets.Length == 1)
            {
                Undo.RecordObject(transform, "Reset position of " + transform.gameObject.name);
                if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
                    transform.localScale = Vector3.one;
                else
                    SetGlobalScale(transform, Vector3.one, true);
                EditorUtility.SetDirty(transform);
            }
            else
            {
                foreach (Transform t in targets.Cast<Transform>())
                {
                    Undo.RecordObject(t, "Reset position of " + t.gameObject.name);
                    if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
                        t.localScale = Vector3.one;
                    else
                        SetGlobalScale(transform, Vector3.one, true);
                    EditorUtility.SetDirty(transform);
                }
            }
        }

        private string lockedAspectRatioDisabledFieldTooltip = "Can't change field value from zero when aspect ratio is locked. Please unlock and change it.";

        private void UpdateScaleAspectRationButton()
        {
            var scaleField_x_local = localScaleField.Q<FloatField>("unity-x-input");
            var scaleField_x_world = worldScaleField.Q<FloatField>("unity-x-input");

            var scaleField_y_local = localScaleField.Q<FloatField>("unity-y-input");
            var scaleField_y_world = worldScaleField.Q<FloatField>("unity-y-input");

            var scaleField_z_local = localScaleField.Q<FloatField>("unity-z-input");
            var scaleField_z_world = worldScaleField.Q<FloatField>("unity-z-input");

            if (editorSettings.LockSizeAspectRatio)
            {
                scaleAspectRatioLocked.style.display = DisplayStyle.Flex;
                scaleAspectRatioUnlocked.style.display = DisplayStyle.None;

                if (targets.Length == 1)
                {
                    Vector3 localScale = transform.localScale;
                    if (localScale.x == 0)
                    {
                        scaleField_x_local.SetEnabled(false);
                        scaleField_x_local.tooltip = lockedAspectRatioDisabledFieldTooltip;
                        scaleField_x_world.SetEnabled(false);
                        scaleField_x_world.tooltip = lockedAspectRatioDisabledFieldTooltip;
                    }
                    if (localScale.y == 0)
                    {
                        scaleField_y_local.SetEnabled(false);
                        scaleField_y_local.tooltip = lockedAspectRatioDisabledFieldTooltip;
                        scaleField_y_world.SetEnabled(false);
                        scaleField_y_world.tooltip = lockedAspectRatioDisabledFieldTooltip;
                    }
                    if (localScale.z == 0)
                    {
                        scaleField_z_local.SetEnabled(false);
                        scaleField_z_local.tooltip = lockedAspectRatioDisabledFieldTooltip;
                        scaleField_z_world.SetEnabled(false);
                        scaleField_z_world.tooltip = lockedAspectRatioDisabledFieldTooltip;
                    }
                }
            }
            else
            {
                scaleAspectRatioLocked.style.display = DisplayStyle.None;
                scaleAspectRatioUnlocked.style.display = DisplayStyle.Flex;

                scaleField_x_local.SetEnabled(true);
                scaleField_x_local.tooltip = string.Empty;

                scaleField_x_world.SetEnabled(true);
                scaleField_x_world.tooltip = string.Empty;

                scaleField_y_local.SetEnabled(true);
                scaleField_y_local.tooltip = string.Empty;

                scaleField_y_world.SetEnabled(true);
                scaleField_y_world.tooltip = string.Empty;

                scaleField_z_local.SetEnabled(true);
                scaleField_z_local.tooltip = string.Empty;

                scaleField_z_world.SetEnabled(true);
                scaleField_z_world.tooltip = string.Empty;
            }
        }

        private void UpdateScale()
        {
            UpdateScale_label();
            UpdateScale_fields();
            UpdateScale_buttons();

            UpdateScale_prefabOverrideIndicator();
        }

        private void UpdateScale_label()
        {
            if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
                scaleLabel.tooltip = "The local scaling of this GameObject relative to the parent.";
            else
            {
                scaleLabel.tooltip = "The world scaling of this GameObject.";
                if (targets.Length > 1)
                {
                    scaleLabel.tooltip += "\n" + worldScaleReadOnlyTooltip;
                    scaleLabel.SetEnabled(false);
                }
            }
            UpdateScaleLabelContextMenu();
        }

        private void UpdateScale_fields()
        {
            if (editorSettings.roundScaleField)
                localScaleField.SetValueWithoutNotify(RoundedVector3v2(transform.localScale));
            else
                localScaleField.SetValueWithoutNotify(RoundedVector3(transform.localScale));

            SetWorldScaleField();

            switch (editorSettings.CurrentWorkSpace)
            {
                case BetterTransformSettings.WorkSpace.Local:
                    localScaleField.style.display = DisplayStyle.Flex;
                    worldScaleField.style.display = DisplayStyle.None;
                    break;

                case BetterTransformSettings.WorkSpace.World:
                    localScaleField.style.display = DisplayStyle.None;
                    worldScaleField.style.display = DisplayStyle.Flex;
                    break;

                case BetterTransformSettings.WorkSpace.Both:
                    localScaleField.style.display = DisplayStyle.None; //Uses the default inspector's local field for this when showing both
                    worldScaleField.style.display = DisplayStyle.Flex;
                    break;
            }
        }

        private void UpdateScale_buttons()
        {
            if (editorSettings.ShowCopyPasteButtons)
            {
                copyScaleButton.style.display = DisplayStyle.Flex;
                pasteScaleButton.style.display = DisplayStyle.Flex;
                resetScaleButton.style.display = DisplayStyle.Flex;

                copyScaleButton.tooltip = "Copy " + GetCurrentWorkspaceForToolbar() + " scale.";
                pasteScaleButton.tooltip = "Paste to " + GetCurrentWorkspaceForToolbar() + " scale.";
                resetScaleButton.tooltip = "Reset " + GetCurrentWorkspaceForToolbar() + " scale to one.";
                if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                    resetScaleButton.tooltip += "\n" + "Please note that world scale takes into account of parent rotation and scale. So, setting it to one won't always result in 1. Sometimes, its just a number close to it.";
            }
            else
            {
                copyScaleButton.style.display = DisplayStyle.None;
                pasteScaleButton.style.display = DisplayStyle.None;
                resetScaleButton.style.display = DisplayStyle.None;
            }
        }

        private string GetCurrentWorkspaceForToolbar()
        {
            if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Both)
                return "World";
            return editorSettings.CurrentWorkSpace.ToString();
        }

        /// <summary>
        /// This is the right click menu on the label
        /// </summary>
        private ContextualMenuManipulator contextualMenuManipulatorForScaleLabel;

        private void UpdateScaleLabelContextMenu()
        {
            if (contextualMenuManipulatorForScaleLabel != null)
                scaleLabel.RemoveManipulator(contextualMenuManipulatorForScaleLabel);

            UpdateContextMenuForScale();

            scaleLabel.AddManipulator(contextualMenuManipulatorForScaleLabel);

            void UpdateContextMenuForScale()
            {
                contextualMenuManipulatorForScaleLabel = new ContextualMenuManipulator((evt) =>
                {
                    evt.menu.AppendAction("Scale :", (x) => CopyScalePropertyPath(), DropdownMenuAction.Status.Disabled);
                    evt.menu.AppendSeparator();

                    evt.menu.AppendAction("Copy property path", (x) => CopyScalePropertyPath(), DropdownMenuAction.AlwaysEnabled);

                    if (editorSettings.roundScaleField)
                        evt.menu.AppendAction("Round out field values for the inspector", (x) => ToggleScaleFieldRounding(), DropdownMenuAction.Status.Checked);
                    else
                        evt.menu.AppendAction("Round out field values for the inspector", (x) => ToggleScaleFieldRounding(), DropdownMenuAction.Status.Normal);

                    if (HasPrefabOverride_scale())
                    {
                        evt.menu.AppendSeparator();
                        evt.menu.AppendAction("Apply to Prefab '" + PrefabUtility.GetCorrespondingObjectFromSource(transform.gameObject).name + "'", (x) => ApplyScaleChangeToPrefab(), DropdownMenuAction.AlwaysEnabled);
                        evt.menu.AppendAction("Revert", (x) => RevertScaleChangeToPrefab(), DropdownMenuAction.AlwaysEnabled);
                    }

                    evt.menu.AppendSeparator();

                    evt.menu.AppendAction("Copy", (x) => CopyScale(), DropdownMenuAction.AlwaysEnabled);
                    GetVector3FromCopyBuffer(out bool exists, out float x, out float y, out float z);
                    if (exists)
                        evt.menu.AppendAction("Paste", (x) => PasteScale(), DropdownMenuAction.AlwaysEnabled);
                    else
                        evt.menu.AppendAction("Paste", (x) => PasteScale(), DropdownMenuAction.AlwaysDisabled);

                    evt.menu.AppendAction("Reset", (x) => ResetScale(), DropdownMenuAction.AlwaysEnabled);
                });
            }

            void CopyScalePropertyPath()
            {
                EditorGUIUtility.systemCopyBuffer = scaleProperty;
            }

            void ApplyScaleChangeToPrefab()
            {
                if (soTarget.FindProperty(scaleProperty).prefabOverride)
                {
                    PrefabUtility.ApplyPropertyOverride(soTarget.FindProperty(scaleProperty), PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(transform), InteractionMode.UserAction);
                }
            }

            void RevertScaleChangeToPrefab()
            {
                if (soTarget.FindProperty(scaleProperty).prefabOverride)
                {
                    PrefabUtility.RevertPropertyOverride(soTarget.FindProperty(scaleProperty), InteractionMode.UserAction);
                }
            }
        }

        private bool HasPrefabOverride_scale()
        {
            if (soTarget.FindProperty(scaleProperty).prefabOverride)
                return true;
            else
                return false;
        }

        private void CopyScale()
        {
            if (targets.Length == 1)
            {
                if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
                    EditorGUIUtility.systemCopyBuffer = "Vector3" + transform.localScale.ToString("F10");
                else
                    EditorGUIUtility.systemCopyBuffer = "Vector3" + transform.lossyScale.ToString("F10");
            }
            else
            {
                CopyMultipleSelectToBuffer_scale();
            }

            UpdatePasteButtons();
        }

        private void PasteScale()
        {
            if (targets.Length == 1)
            {
                GetVector3FromCopyBuffer(out bool exists, out float x, out float y, out float z);

                if (!exists)
                    return;

                Undo.RecordObject(transform, "Scale Paste on " + transform.gameObject.name);

                if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Local)
                    transform.localScale = new Vector3(x, y, z);
                else
                    SetGlobalScale(transform, new Vector3(x, y, z), true);

                soTarget.Update();
                EditorUtility.SetDirty(transform);
            }
            else
            {
                GetVector3ListFromCopyBuffer(out bool exists, out List<string> values);
                //values.Reverse();

                if (!exists) return;

                var transforms = targets.Cast<Transform>().ToList();

                //for (int i = transforms.Count() - 1; i >= 0; i--)
                for (int i = 0; i < transforms.Count; i++)
                {
                    if (values.Count <= i)
                        break;

                    var value = GetVector3FromString(values[i], out bool exists2);

                    if (!exists2) continue;

                    Undo.RecordObject(transforms[i], "Scale Paste on " + transforms[i].gameObject.name);

                    if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                        SetGlobalScale(transforms[i], value, true);
                    else
                        transforms[i].localScale = value;

                    soTarget.Update();
                    EditorUtility.SetDirty(transforms[i]);
                }
            }
        }

        private void ToggleScaleFieldRounding()
        {
            editorSettings.roundScaleField = !editorSettings.roundScaleField;
            editorSettings.Save();

            if (editorSettings.roundScaleField)
            {
                localScaleField.SetValueWithoutNotify(RoundedVector3v2(transform.localScale));
                worldScaleField.SetValueWithoutNotify(RoundedVector3v2(transform.lossyScale));
            }
            else
            {
                localScaleField.SetValueWithoutNotify(transform.localScale);
                worldScaleField.SetValueWithoutNotify(transform.lossyScale);
            }

            UpdateScaleLabelContextMenu();

            if (roundScaleFieldToggle != null) roundScaleFieldToggle.SetValueWithoutNotify(editorSettings.roundScaleField);
        }

        private bool scaleBeingUpdatedBySize = false;

        private void UpdateScale_prefabOverrideIndicator()
        {
            if (!HasPrefabOverride_scale())
            {
                scalePrefabOverrideMark.style.display = DisplayStyle.None;
                scaleLabel.RemoveFromClassList(prefabOverrideLabel);
                localScaleField.RemoveFromClassList(prefabOverrideLabel);
                worldScaleField.RemoveFromClassList(prefabOverrideLabel);
            }
            else
            {
                scalePrefabOverrideMark.style.display = DisplayStyle.Flex;
                scaleLabel.AddToClassList(prefabOverrideLabel);
                localScaleField.AddToClassList(prefabOverrideLabel);
                worldScaleField.AddToClassList(prefabOverrideLabel);
            }
        }

        private void SetLocalScale(Vector3 newLocalScale)
        {
            Undo.RecordObject(transform, "Scale change on " + transform.gameObject.name);
            if (editorSettings.LockSizeAspectRatio)
                transform.localScale = AspectRatioAppliedLocalScale(transform.localScale, newLocalScale);
            else
                transform.localScale = newLocalScale;
        }

        private void SetGlobalScale(Transform t, Vector3 newLossyScale, bool ignoreAspectRatioLock = false)
        {
            Undo.RecordObject(t, "Scale change on " + t.gameObject.name);
            if (!editorSettings.LockSizeAspectRatio || ignoreAspectRatioLock)
            {
                if (t.parent == null)
                {
                    t.localScale = newLossyScale;
                    return;
                }

                Vector3 originalLocalScale = t.localScale;
                Vector3 oldLossyScale = t.lossyScale;

                float newX = originalLocalScale.x;
                float newY = originalLocalScale.y;
                float newZ = originalLocalScale.z;

                if (!Mathf.Approximately(newLossyScale.x, oldLossyScale.x))
                    newX = newLossyScale.x / t.parent.lossyScale.x;

                if (!Mathf.Approximately(newLossyScale.y, oldLossyScale.y))
                    newY = newLossyScale.y / t.parent.lossyScale.y;

                if (!Mathf.Approximately(newLossyScale.z, oldLossyScale.z))
                    newZ = newLossyScale.z / t.parent.lossyScale.z;

                Vector3 newScale = new Vector3(newX, newY, newZ);
                if (IsInfinity(newScale))
                    Debug.LogWarning("<color=yellow>Unable to set world scale</color> because the target world scale :" + newScale + " contains infinity. The most common cause of this issue is any object in it's parent hierarchy has scale with 0 value in any axis.", transform);
                else
                    t.localScale = new Vector3(newX, newY, newZ);
            }
            else
            {
                //Vector3 newLocalScale = Multiply(t.localScale, Divide(newLossyScale, t.lossyScale));
                t.localScale = AspectRatioAppliedWorldScale(newLossyScale);
            }
        }

        private bool IsInfinity(Vector3 vector3)
        {
            if (vector3.x == Mathf.Infinity)
                return true;
            if (vector3.y == Mathf.Infinity)
                return true;
            if (vector3.z == Mathf.Infinity)
                return true;

            return false;
        }

        private Vector3 nonZeroValue = Vector3.one;

        private Vector3 AspectRatioAppliedLocalScale(Vector3 currentLocalScale, Vector3 newLocalScale)
        {
            if (currentLocalScale.x != newLocalScale.x)
            {
                if (newLocalScale.x == 0)
                    nonZeroValue = currentLocalScale;

                float multiplier = newLocalScale.x / currentLocalScale.x;

                if (float.IsFinite(multiplier))
                    return new Vector3(newLocalScale.x, (currentLocalScale.y * multiplier), (currentLocalScale.z * multiplier));
                else
                {
                    multiplier = newLocalScale.x / nonZeroValue.x;
                    return new Vector3(newLocalScale.x, (nonZeroValue.y * multiplier), (nonZeroValue.z * multiplier));
                }
            }
            else if (currentLocalScale.y != newLocalScale.y)
            {
                if (newLocalScale.y == 0)
                    nonZeroValue = currentLocalScale;

                float multiplier = newLocalScale.y / currentLocalScale.y;
                if (float.IsFinite(multiplier))
                    return new Vector3((currentLocalScale.x * multiplier), newLocalScale.y, (currentLocalScale.z * multiplier));
                else
                {
                    Debug.Log(newLocalScale);
                    multiplier = newLocalScale.y / nonZeroValue.y;
                    return new Vector3((nonZeroValue.x * multiplier), newLocalScale.y, (nonZeroValue.z * multiplier));
                }
            }
            else if (currentLocalScale.z != newLocalScale.z)
            {
                if (newLocalScale.z == 0)
                    nonZeroValue = currentLocalScale;

                float multiplier = newLocalScale.z / currentLocalScale.z;
                if (float.IsFinite(multiplier))
                    return new Vector3((currentLocalScale.x * multiplier), (currentLocalScale.y * multiplier), newLocalScale.z);
                else
                {
                    multiplier = newLocalScale.z / nonZeroValue.z;
                    return new Vector3((nonZeroValue.x * multiplier), (nonZeroValue.y * multiplier), newLocalScale.z);
                }
            }

            return newLocalScale;
        }

        private Vector3 AspectRatioAppliedWorldScale(Vector3 newLossyScale)
        {
            Vector3 currentLossyScale = transform.lossyScale;

            Vector3 currentLocalScale = transform.localScale;
            Vector3 newLocalScale = Multiply(transform.localScale, Divide(newLossyScale, transform.lossyScale));

            if (currentLossyScale.x != newLossyScale.x)
            {
                if (newLossyScale.x == 0)
                    nonZeroValue = currentLossyScale;

                float multiplier = newLossyScale.x / currentLossyScale.x;

                if (float.IsFinite(multiplier))
                {
                    return new Vector3(newLocalScale.x, (currentLocalScale.y * multiplier), (currentLocalScale.z * multiplier));
                }
                else
                {
                    multiplier = newLossyScale.x / nonZeroValue.x;
                    return new Vector3(newLossyScale.x, (nonZeroValue.y * multiplier), (nonZeroValue.z * multiplier));
                }
            }
            else if (currentLossyScale.y != newLossyScale.y)
            {
                if (newLossyScale.y == 0)
                    nonZeroValue = currentLossyScale;

                float multiplier = newLossyScale.y / currentLossyScale.y;

                if (float.IsFinite(multiplier))
                {
                    return new Vector3((currentLocalScale.x * multiplier), newLocalScale.y, (currentLocalScale.z * multiplier));
                }
                else
                {
                    multiplier = newLossyScale.y / nonZeroValue.y;
                    return new Vector3((nonZeroValue.x * multiplier), newLossyScale.y, (nonZeroValue.z * multiplier));
                }
            }
            else if (currentLossyScale.z != newLossyScale.z)
            {
                if (newLossyScale.z == 0)
                    nonZeroValue = currentLossyScale;

                float multiplier = newLossyScale.z / currentLossyScale.z;

                if (float.IsFinite(multiplier))
                {
                    return new Vector3((currentLocalScale.x * multiplier), (currentLocalScale.y * multiplier), newLocalScale.z);
                }
                else
                {
                    multiplier = newLossyScale.z / nonZeroValue.z;
                    return new Vector3((nonZeroValue.x * multiplier), (nonZeroValue.y * multiplier), newLossyScale.z);
                }
            }

            return newLocalScale;
        }

        #endregion Scale

        private void GetQuaternionFromCopyBuffer(out bool exists, out float x, out float y, out float z, out float w)
        {
            exists = false;
            x = 0; y = 0; z = 0; w = 0;

            string copyBuffer = EditorGUIUtility.systemCopyBuffer;
            if (copyBuffer != null)
            {
                if (copyBuffer.Contains("Quaternion"))
                {
                    if (copyBuffer.Length > 9)
                    {
                        copyBuffer = copyBuffer.Substring(11, copyBuffer.Length - 12);
                        string[] valueStrings = copyBuffer.Split(',');
                        if (valueStrings.Length == 4)
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
                            if (exists)
                            {
                                if (!float.TryParse(valueStrings[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out w))
                                    exists = false;
                            }
                        }
                    }
                }
            }
        }

        //todo: Remove duplicate code and use the one in Quick actions
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

        private string hierarchyCopyIdentifier = "Hierarchy Copy";

        //todo: Move this function to QuickActionsClass
        private void CopyMultipleSelectToBuffer_position()
        {
            string copyString = hierarchyCopyIdentifier;

            foreach (Transform t in targets.Cast<Transform>())
            {
                copyString += "\n";
                if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                    copyString += "Vector3" + t.position.ToString();
                else
                    copyString += "Vector3" + t.localPosition.ToString();
            }

            EditorGUIUtility.systemCopyBuffer = copyString;

            UpdatePasteButtons();
        }

        private void CopyMultipleSelectToBuffer_scale()
        {
            string copyString = hierarchyCopyIdentifier;

            foreach (Transform t in targets.Cast<Transform>())
            {
                copyString += "\n";
                if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                    copyString += "Vector3" + t.lossyScale.ToString();
                else
                    copyString += "Vector3" + t.localScale.ToString();
            }

            EditorGUIUtility.systemCopyBuffer = copyString;

            UpdatePasteButtons();
        }

        private void CopyMultipleSelectToBuffer_rotation()
        {
            string copyString = hierarchyCopyIdentifier;

            foreach (Transform t in targets.Cast<Transform>())
            {
                copyString += "\n";

                if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                    copyString += "Vector3" + t.eulerAngles.ToString();
                else
                    copyString += "Vector3" + t.localEulerAngles.ToString();
            }

            EditorGUIUtility.systemCopyBuffer = copyString;

            UpdatePasteButtons();
        }

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

        #endregion Main Controls

        #region Size

        #region Variable

        private GroupBox sizeFoldout;
        private DropdownField unitDropDownField;
        private Vector3Field sizeFoldoutField;
        private GroupBox sizeCenterFoldoutGroup; //This is inside the size foldout
        private Vector3Field sizeCenterFoldoutField;
        private Bounds currentBound;

        private Button gizmoOnButton;
        private Button gizmoOffButton;

        private Button hierarchySizeButton;
        private Button selfSizeButton;

        private Button sizeAspectRatioUnlocked;
        private Button sizeAspectRatioLocked;

        private IntegerField maxChildCountForSizeCalculation;

        private Button refreshSizeButton;

        private Button rendererSizeButton;
        private Button filterSizeButton;

        private GroupBox inlineSizeGroupBox;
        private GroupBox inlineSizeButtonsGroupBox;

        private Button manualUpdateButton;

        private Vector3 targetVector3;

        private bool sizeSetupDone = false;

        #endregion Variable

        private void SetupSizeCommon()
        {
            sizeFoldout = root.Q<GroupBox>("SizeFoldout");

            sizeFoldoutField = sizeFoldout.Q<Vector3Field>("SizeFoldoutField");
            sizeCenterFoldoutGroup = sizeFoldout.Q<GroupBox>("SizeCenterFoldoutGroup");
            sizeCenterFoldoutField = sizeCenterFoldoutGroup.Q<Vector3Field>("CenterFoldoutField");
        }

        private void HideSize()
        {
            sizeFoldout.style.display = DisplayStyle.None;

            if (inlineSizeGroupBox == null)
                inlineSizeGroupBox = root.Q<GroupBox>("InlineSizeGroupBox");
            if (inlineSizeButtonsGroupBox == null)
                inlineSizeButtonsGroupBox = root.Q<GroupBox>("InlineSizeButtonsGroupBox");

            inlineSizeGroupBox.style.display = DisplayStyle.None;
            inlineSizeButtonsGroupBox.style.display = DisplayStyle.None;
        }

        private void SetupSize(CustomFoldoutSetup customFoldoutSetup)
        {
            if (sizeSetupDone) return;

            sizeSetupDone = true;

            customFoldoutSetup.SetupFoldout(sizeFoldout);

            sizeFoldoutInformationLabel = root.Q<Label>("sizeInformationLabel");

            if (editorSettings.ShowSizeFoldout && targets.Length == 1)
                sizeFoldout.style.display = DisplayStyle.Flex;
            else
                sizeFoldout.style.display = DisplayStyle.None;

            unitDropDownField = sizeFoldout.Q<DropdownField>("UnitsDropDownField");
            if (ScalesManager.instance.GetAvailableUnits().ToList().Count == 0) ScalesManager.instance.Reset();
            unitDropDownField.choices = ScalesManager.instance.GetAvailableUnits().ToList();
            unitDropDownField.index = ScalesManager.instance.selectedUnit;
            unitDropDownField.RegisterValueChangedCallback(ev =>
            {
                var myScales = ScalesManager.instance;
                myScales.selectedUnit = unitDropDownField.index;
                EditorUtility.SetDirty(myScales);

                UpdateSize();
            });

            sizeFoldoutField.RegisterValueChangedCallback(ev =>
            {
                sizeFoldoutField.schedule.Execute(() => SetSizeAsynch()).ExecuteLater(500);
            });

            sizeCenterFoldoutGroup.SetEnabled(false);

            refreshSizeButton = sizeFoldout.Q<Button>("RefreshSizeButton");
            refreshSizeButton.clicked += () =>
            {
                UpdateSize();
            };

            gizmoOnButton = sizeFoldout.Q<Button>("GizmoOn");
            gizmoOnButton.clicked += () =>
            {
                editorSettings.ShowSizeGizmo = false;
                UpdateSize_ShowGizmoButton();
                SceneView.RepaintAll();
            };
            gizmoOffButton = sizeFoldout.Q<Button>("GizmoOff");
            gizmoOffButton.clicked += () =>
            {
                editorSettings.ShowSizeGizmo = true;
                UpdateSize_ShowGizmoButton();
                SceneView.RepaintAll();
            };
            UpdateSize_ShowGizmoButton();

            hierarchySizeButton = sizeFoldout.Q<Button>("HierarchySize");
            selfSizeButton = sizeFoldout.Q<Button>("SelfSize");

            sizeAspectRatioLocked = sizeFoldout.Q<Button>("AspectRatioLocked");
            sizeAspectRatioLocked.clicked += () =>
            {
                editorSettings.LockSizeAspectRatio = false;
                UpdateSize_AspectRationButton();
                UpdateScaleAspectRationButton();
            };

            sizeAspectRatioUnlocked = sizeFoldout.Q<Button>("AspectRatioUnlocked");
            sizeAspectRatioUnlocked.clicked += () =>
            {
                editorSettings.LockSizeAspectRatio = true;
                UpdateSize_AspectRationButton();
                UpdateScaleAspectRationButton();
            };

            hierarchySizeButton.clicked += () =>
            {
                editorSettings.IncludeChildBounds = false;
                UpdateSizeInclusionButtons();

                SceneView.RepaintAll();
            };
            selfSizeButton.clicked += () =>
            {
                editorSettings.IncludeChildBounds = true;
                UpdateSizeInclusionButtons();

                SceneView.RepaintAll();
            };

            UpdateSize_AspectRationButton();

            sizeCopyButton = sizeFoldout.Q<Button>("Copy");
            sizeCopyButton.clicked += () =>
            {
                float unitMultiplier = ScalesManager.instance.CurrentUnitValue();
                //EditorGUIUtility.systemCopyBuffer = "Vector3" + RoundedVector3(currentBound.size * unitMultiplier).ToString();
                EditorGUIUtility.systemCopyBuffer = "Vector3" + (currentBound.size * unitMultiplier).ToString("F20");
            };

            sizePasteButton = sizeFoldout.Q<Button>("Paste");
            sizePasteButton.clicked += () =>
            {
                GetVector3FromCopyBuffer(out bool exists, out float x, out float y, out float z);
                if (!exists)
                    return;

                Undo.RecordObject(transform, "Size Paste on " + transform.gameObject.name);
                SetSize(new Vector3(x, y, z));
                EditorUtility.SetDirty(transform);

                float unitMultiplier = ScalesManager.instance.CurrentUnitValue();
                sizeFoldoutField.SetValueWithoutNotify(RoundedVector3(currentBound.size * unitMultiplier));
                sizeCenterFoldoutField.SetValueWithoutNotify(RoundedVector3(currentBound.center * unitMultiplier));
            };

            sizeResetButton = sizeFoldout.Q<Button>("Reset");
            sizeResetButton.clicked += () =>
            {
                Undo.RecordObject(transform, "Size Reset on " + transform.gameObject.name);
                SetSize(Vector3.one);
                EditorUtility.SetDirty(transform);

                float unitMultiplier = ScalesManager.instance.CurrentUnitValue();
                sizeFoldoutField.SetValueWithoutNotify(RoundedVector3(Vector3.one * unitMultiplier));
                sizeCenterFoldoutField.SetValueWithoutNotify(RoundedVector3(currentBound.center * unitMultiplier));
            };

            inlineSizeGroupBox = root.Q<GroupBox>("InlineSizeGroupBox");
            inlineSizeButtonsGroupBox = root.Q<GroupBox>("InlineSizeButtonsGroupBox");
            SetupInLineSizeView();

            if (editorSettings.IncludeChildBounds)
            {
                hierarchySizeButton.style.display = DisplayStyle.Flex;
                selfSizeButton.style.display = DisplayStyle.None;
            }
            else
            {
                hierarchySizeButton.style.display = DisplayStyle.None;
                selfSizeButton.style.display = DisplayStyle.Flex;
            }

            if (editorSettings.ShowSizeCenter)
                sizeCenterFoldoutGroup.style.display = DisplayStyle.Flex;
            else
                sizeCenterFoldoutGroup.style.display = DisplayStyle.None;

            rendererSizeButton = root.Q<Button>("RendererSizeButton");
            rendererSizeButton.clicked += () =>
            {
                editorSettings.CurrentSizeType = BetterTransformSettings.SizeType.Filter;
                UpdateSizeTypeButtons();
                UpdateSize();
            };
            filterSizeButton = root.Q<Button>("FilterSizeButton");
            filterSizeButton.clicked += () =>
            {
                editorSettings.CurrentSizeType = BetterTransformSettings.SizeType.Renderer;
                UpdateSizeTypeButtons();
                UpdateSize();
            };
            UpdateSizeTypeButtons();

            maxChildCountForSizeCalculation = root.Q<IntegerField>("MaxChildCountForSizeCalculation");
            maxChildCountForSizeCalculation.value = editorSettings.MaxChildCountForSizeCalculation;
            maxChildCountForSizeCalculation.RegisterValueChangedCallback(ev =>
            {
                editorSettings.MaxChildCountForSizeCalculation = ev.newValue;
            });
        }

        private bool manuallyUpdatedSize = false;

        private void CreateTooManyChildForAutoSizeCalculationWarning()
        {
            manualUpdateButton = new Button();
            manualUpdateButton.text = "Check Size";
            manualUpdateButton.tooltip = "Too many child objects for automatic size calculation. You can change the amount from setting.";
            manualUpdateButton.clicked += () =>
            {
                manuallyUpdatedSize = true;
                UpdateSize();
            };

            var rootHolder = root.Q<VisualElement>("RootHolder");
            int index = rootHolder.IndexOf(sizeFoldout);
            rootHolder.Insert(index, manualUpdateButton);
            //root.Add(manualUpdateButton);
        }

        private void UpdateSizeTypeButtons()
        {
            var rendererSizeButton = root.Q<Button>("RendererSizeButton");
            var filterSizeButton = root.Q<Button>("FilterSizeButton");

            if (editorSettings.CurrentSizeType == BetterTransformSettings.SizeType.Filter)
            {
                rendererSizeButton.style.display = DisplayStyle.None;
                filterSizeButton.style.display = DisplayStyle.Flex;
            }
            else
            {
                rendererSizeButton.style.display = DisplayStyle.Flex;
                filterSizeButton.style.display = DisplayStyle.None;
            }
        }

        private void SetupInLineSizeView()
        {
            if (editorSettings.ShowSizeInLine)
            {
                inlineSizeGroupBox.Add(sizeFoldoutField.parent.parent);
                inlineSizeButtonsGroupBox.Add(unitDropDownField);
                inlineSizeButtonsGroupBox.Add(sizeFoldout.Q<GroupBox>("SizeTypeGroupBox"));
                inlineSizeButtonsGroupBox.Add(sizeFoldout.Q<GroupBox>("CalculationTypeGroupBox"));
                inlineSizeButtonsGroupBox.Add(sizeFoldout.Q<GroupBox>("GizmoGroupBox"));
            }
        }

        private void UpdateSizeFoldout()
        {
            if (editorSettings.ShowSizeFoldout && targets.Length == 1)
                sizeFoldout.style.display = DisplayStyle.Flex;
            else
                sizeFoldout.style.display = DisplayStyle.None;

            if (editorSettings.ShowSizeFoldout || editorSettings.ShowSizeInLine)
            {
                if (ScalesManager.instance.GetAvailableUnits().ToList().Count == 0) ScalesManager.instance.Reset();
                unitDropDownField.choices = ScalesManager.instance.GetAvailableUnits().ToList();

                unitDropDownField.index = ScalesManager.instance.selectedUnit;

                UpdateSizeInclusionButtons();
            }

            if (editorSettings.ShowSizeCenter)
                sizeCenterFoldoutGroup.style.display = DisplayStyle.Flex;
            else
                sizeCenterFoldoutGroup.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// SetupInLineSizeView() is called to update this first time
        /// </summary>
        private void UpdateInLineSizeView()
        {
            if (editorSettings.ShowSizeInLine)
            {
                inlineSizeGroupBox.Add(sizeFoldoutField.parent.parent);

                inlineSizeButtonsGroupBox.Add(unitDropDownField);
                inlineSizeButtonsGroupBox.Add(sizeFoldout.Q<GroupBox>("SizeTypeGroupBox"));
                inlineSizeButtonsGroupBox.Add(sizeFoldout.Q<GroupBox>("CalculationTypeGroupBox"));
                inlineSizeButtonsGroupBox.Add(sizeFoldout.Q<GroupBox>("GizmoGroupBox"));
            }
            else //show size in foldout
            {
                GroupBox content = sizeFoldout.Q<GroupBox>("Content");
                content.Insert(0, sizeFoldoutField.parent.parent);

                GroupBox InlineSizeButtonsGroupBox = root.Q<GroupBox>("InlineSizeButtonsGroupBox");

                GroupBox header = sizeFoldout.Q<GroupBox>("Header");
                header.Add(InlineSizeButtonsGroupBox.Q<GroupBox>("GizmoGroupBox"));
                header.Add(InlineSizeButtonsGroupBox.Q<GroupBox>("SizeTypeGroupBox"));
                header.Add(InlineSizeButtonsGroupBox.Q<GroupBox>("CalculationTypeGroupBox"));
                header.Add(unitDropDownField);
            }
        }

        private void UpdateSize_ShowGizmoButton()
        {
            if (editorSettings.ShowSizeGizmo)
            {
                gizmoOnButton.style.display = DisplayStyle.Flex;
                gizmoOffButton.style.display = DisplayStyle.None;
            }
            else
            {
                gizmoOnButton.style.display = DisplayStyle.None;
                gizmoOffButton.style.display = DisplayStyle.Flex;
            }
        }

        private void UpdateSize_AspectRationButton()
        {
            if (editorSettings.LockSizeAspectRatio)
            {
                sizeAspectRatioLocked.style.display = DisplayStyle.Flex;
                sizeAspectRatioUnlocked.style.display = DisplayStyle.None;
            }
            else
            {
                sizeAspectRatioLocked.style.display = DisplayStyle.None;
                sizeAspectRatioUnlocked.style.display = DisplayStyle.Flex;
            }
        }

        private void UpdateSizeInclusionButtons()
        {
            if (editorSettings.IncludeChildBounds)
            {
                hierarchySizeButton.style.display = DisplayStyle.Flex;
                selfSizeButton.style.display = DisplayStyle.None;
                UpdateSize();
            }
            else
            {
                hierarchySizeButton.style.display = DisplayStyle.None;
                selfSizeButton.style.display = DisplayStyle.Flex;
                UpdateSize();
            }
        }

        private Button sizeCopyButton;
        private Button sizePasteButton;
        private Button sizeResetButton;
        private Label sizeFoldoutInformationLabel;

        private void UpdateSize(bool showWarningIfTooManyChild = false)
        {
            if (targets.Length != 1) return;

            //If not showing the size foldout, no need to update it
            if (!editorSettings.ShowSizeFoldout && !editorSettings.ShowSizeInLine) return;

            //On domain reload, the reference is lost.
            if (sizeFoldout == null)
            {
                sizeSetupDone = false;

                SetupSize(new CustomFoldoutSetup());
                //Debug.Log("size foldout setup was required.");
            }

            currentBound = CheckSize(transform, showWarningIfTooManyChild);

            //Do not show size foldout if the object has zero size, aka, no renderer
            if (currentBound.size == Vector3.zero)
            {
                sizeFoldoutInformationLabel ??= root.Q<Label>("sizeInformationLabel");
                if (sizeFoldoutInformationLabel != null) //If it is still not found for some reason.
                {
                    if (editorSettings.showWhySizeIsHiddenLabel)
                    {
                        sizeFoldoutInformationLabel.style.display = DisplayStyle.Flex;

                        if (!editorSettings.IncludeChildBounds)
                        {
                            if (transform.childCount > 0)
                                sizeFoldoutInformationLabel.text = "Size is hidden because this object has no mesh with size and child object's size is ignored because self size is selected";
                            else
                            {
                                if (editorSettings.CurrentSizeType == BetterTransformSettings.SizeType.Filter && transform.GetComponent<Renderer>() != null)
                                    sizeFoldoutInformationLabel.text = "Size is hidden because size type of filter is selected and this object has a renderer.";
                                else if (editorSettings.CurrentSizeType == BetterTransformSettings.SizeType.Renderer && editorSettings.ignoreParticleAndVFXInSizeCalculation && (transform.GetComponent<ParticleSystem>() != null || transform.GetComponent<VisualEffect>()))
                                    sizeFoldoutInformationLabel.text = "Size is hidden because the size of particle systems and visual effect are ignored in setting.";
                                else
                                    sizeFoldoutInformationLabel.text = "Size is hidden because this object has no mesh with size.";
                            }
                        }
                        else
                        {
                            if (manuallyUpdatedSize) //Pressed the check size button
                                sizeFoldoutInformationLabel.text = "Size is hidden because this object and it's child objects have no mesh with size.";
                            else
                            {
                                if (transform.childCount > 0)
                                    sizeFoldoutInformationLabel.text = "Size is hidden because this object and it's child objects have no mesh with size or size needs to be updated.";
                                else
                                {
                                    if (editorSettings.CurrentSizeType == BetterTransformSettings.SizeType.Filter && transform.GetComponent<Renderer>() != null)
                                        sizeFoldoutInformationLabel.text = "Size is hidden because size type of filter is selected and this object has a renderer.";
                                    else if (editorSettings.CurrentSizeType == BetterTransformSettings.SizeType.Renderer && editorSettings.ignoreParticleAndVFXInSizeCalculation && (transform.GetComponent<ParticleSystem>() != null || transform.GetComponent<VisualEffect>()))
                                        sizeFoldoutInformationLabel.text = "Size is hidden because the size of particle systems and visual effect are ignored in setting.";
                                    else
                                        sizeFoldoutInformationLabel.text = "Size is hidden because this object has no mesh with size.";
                                }
                            }
                        }
                    }
                    else
                    {
                        sizeFoldoutInformationLabel.style.display = DisplayStyle.None;
                    }
                }
                HideSize();

                return;
            }

            sizeFoldoutInformationLabel ??= root.Q<Label>("sizeInformationLabel");
            if (sizeFoldoutInformationLabel != null) //Paranoid if statement. Its 5AM and I am still working
                sizeFoldoutInformationLabel.style.display = DisplayStyle.None;

            if (editorSettings.ShowSizeFoldout && targets.Length == 1)
                sizeFoldout.style.display = DisplayStyle.Flex;
            else if (editorSettings.ShowSizeInLine && targets.Length == 1)
            {
                if (inlineSizeButtonsGroupBox == null)
                    inlineSizeGroupBox = root.Q<GroupBox>("InlineSizeGroupBox");
                inlineSizeGroupBox.style.display = DisplayStyle.Flex;

                if (inlineSizeButtonsGroupBox == null)
                    inlineSizeButtonsGroupBox = root.Q<GroupBox>("InlineSizeButtonsGroupBox");
                inlineSizeButtonsGroupBox.style.display = DisplayStyle.Flex;
            }

            float unitMultiplier = ScalesManager.instance.CurrentUnitValue();

            if (sizeFoldoutField == null)
                SetupSize(customFoldoutSetup);

            sizeFoldoutField.SetValueWithoutNotify(RoundedVector3(currentBound.size * unitMultiplier));

            sizeCenterFoldoutField.SetValueWithoutNotify(RoundedVector3(currentBound.center * unitMultiplier));
        }

        private void SetSizeAsynch()
        {
            targetVector3 = sizeFoldoutField.value;

            float minimum = 0.1f;
            if (targetVector3.x <= minimum) targetVector3.x = minimum;
            if (targetVector3.y <= minimum) targetVector3.y = minimum;
            if (targetVector3.z <= minimum) targetVector3.z = minimum;

            SetSize(targetVector3);
        }

        /// <summary>
        /// Settings the size
        /// </summary>
        /// <param name="newSize"></param>
        private void SetSize(Vector3 newSize)
        {
            float unitMultiplier = ScalesManager.instance.CurrentUnitValue();

            currentBound = CheckSize(transform);
            Vector3 originalSize = currentBound.size * unitMultiplier;

            if (newSize == originalSize)
                return;

            scaleBeingUpdatedBySize = true;

            Vector3 newLocalScale = Multiply(transform.localScale, Divide(newSize, originalSize));

            if (editorSettings.LockSizeAspectRatio)
            {
                Undo.RecordObject(transform, "Scale change on " + transform.gameObject.name);
                transform.localScale = AspectRatioAppliedLocalScale(transform.localScale, newLocalScale);
                EditorUtility.SetDirty(transform);

                currentBound = CheckSize(transform);

                //If the size field is not updated to avoid losing focus,
                //the old size x that wasn't changed impacts the value you are trying to put in Y
                sizeFoldoutField.SetValueWithoutNotify(currentBound.size);
            }
            else
            {
                Undo.RecordObject(transform, "Scale change on " + transform.gameObject.name);
                transform.localScale = newLocalScale;
                EditorUtility.SetDirty(transform);

                currentBound.size = newSize;
            }

            sizeCenterFoldoutField.schedule.Execute(() => JustUpdateCenter()).ExecuteLater(200);

            //Update the gizmo
            SceneView.RepaintAll();
        }

        //void SizeFoldoutUnfocusedAfterSettingLockedAspectRatioSize()
        //{
        //    currentBound = CheckSize(transform);
        //    sizeFoldoutField.SetValueWithoutNotify(currentBound.size);
        //}

        private void JustUpdateCenter()
        {
            currentBound.center = CheckSize(transform).center;
            sizeCenterFoldoutField.SetValueWithoutNotify(currentBound.center);
        }

        #region Check Size

        private bool temporarilyRotatedToCheckSize = false;

        /// <summary>
        ///
        /// </summary>
        /// <param name="target"></param>
        /// <param name="showWarningIfTooManyChild">If set to true, this will stop the update if there are too much child object. if false, it will always update</param>
        /// <returns></returns>
        private Bounds CheckSize(Transform target, bool showWarningIfTooManyChild = false)
        {
#if UNITY_2021_1_OR_NEWER
            if (editorSettings.IncludeChildBounds)
                return GetSizeWithChildren(target, showWarningIfTooManyChild);
            else
                return GetSelfBounds(target);
#else
           if (editorSettings.IncludeChildBounds)
                return GetSizeWithChildren(target);
            else
                return GetRendererSelfBoundsForOlderUnityVersion(target);
#endif
        }

#if !UNITY_2021_1_OR_NEWER

        Bounds GetRendererSelfBoundsForOlderUnityVersion(Transform target)
        {
            if (target.GetComponent<Renderer>() == null)
                return new Bounds(Vector3.zero, Vector3.zero);

            Quaternion currentRotation;
            if (target.parent == null)
            {
                currentRotation = target.localRotation;
                target.localRotation = Quaternion.Euler(Vector3.zero);
            }
            else
            {
                currentRotation = target.rotation;
                target.rotation = Quaternion.Euler(Vector3.zero);
            }

            Bounds bounds = target.GetComponent<Renderer>().bounds;
            bounds.center -= target.position;

            if (target.parent == null)
                target.localRotation = currentRotation;
            else
                target.rotation = currentRotation;

            return bounds;
        }
#endif

        private Bounds GetSelfBounds(Transform target)
        {
            if (manualUpdateButton != null)
                manualUpdateButton.style.display = DisplayStyle.None;

            if (editorSettings.CurrentSizeType == BetterTransformSettings.SizeType.Renderer)
            {
                if (target.GetComponent<Renderer>() == null)
                    return new Bounds(Vector3.zero, Vector3.zero);

                if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                {
                    Bounds bounds = target.GetComponent<Renderer>().bounds;
                    bounds.center -= (target.position);

                    return bounds;
                }
                else
                {
#if UNITY_2021_1_OR_NEWER
                    Bounds bounds = target.GetComponent<Renderer>().localBounds;
#else
                    Bounds bounds = target.GetComponent<Renderer>().bounds; //todo
#endif
                    //bounds.center -= (target.position);
                    bounds.size = Multiply(transform.lossyScale, bounds.size);
                    bounds.center = Multiply(transform.lossyScale, bounds.center);

                    return bounds;
                }
            }
            else
            {
                MeshFilter meshFilter = target.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    if (!meshFilter.sharedMesh)
                        return new Bounds(Vector3.zero, Vector3.zero);

                    Bounds bounds = meshFilter.sharedMesh.bounds;
                    bounds.size = Multiply(bounds.size, target.lossyScale);

                    return bounds;
                }
                else
                {
                    if (target.GetComponent<RectTransform>())
                    {
                        Vector3[] v = new Vector3[4];
                        target.GetComponent<RectTransform>().GetLocalCorners(v);

                        Vector3 min = Vector3.positiveInfinity;
                        Vector3 max = Vector3.negativeInfinity;

                        foreach (Vector3 vector3 in v)
                        {
                            min = Vector3.Min(min, vector3);
                            max = Vector3.Max(max, vector3);
                        }

                        Bounds newBounds = new Bounds();
                        newBounds.SetMinMax(min, max);
                        return newBounds;
                    }
                }
            }

            return new Bounds(Vector3.zero, Vector3.zero);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="target"></param>
        /// <param name="showWarningIfTooManyChild">If set to true, this will stop the update if there are too much child object. if false, it will always update</param>
        /// <returns></returns>
        private Bounds GetSizeWithChildren(Transform target, bool showWarningIfTooManyChild = false)
        {
            if (editorSettings.CurrentSizeType == BetterTransformSettings.SizeType.Renderer)
            {
                if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                    return GetRendererWorldBounds(target, showWarningIfTooManyChild);
                else
                    return GetRendererLocalBounds(target, showWarningIfTooManyChild);
            }
            else
                return CheckFilterBoundsWithChildren(target, showWarningIfTooManyChild);
        }

        /// <summary>
        /// The difference between local and world space size is, the world space size checks axis in world space and local takes target rotation into consideration
        /// </summary>
        /// <param name="target"></param>
        /// <param name="showWarningIfTooManyChild">If set to true, this will stop the update if there are too much child object. if false, it will always update</param>
        /// <returns></returns>
        private Bounds GetRendererWorldBounds(Transform target, bool showWarningIfTooManyChild = false)
        {
            if (manualUpdateButton != null)
                manualUpdateButton.style.display = DisplayStyle.None;

            Bounds newBound = new Bounds();

            Transform[] transforms = target.GetComponentsInChildren<Transform>()
                                        .Where(t =>
                                            !editorSettings.ignoreParticleAndVFXInSizeCalculation ||
                                            (t.GetComponent<ParticleSystem>() == null && t.GetComponent<VisualEffect>() == null))
                                        .ToArray();

            if (transforms.Length == 0)
                return newBound;

            if (showWarningIfTooManyChild && transforms.Length > editorSettings.MaxChildCountForSizeCalculation)
            {
                if (manualUpdateButton != null)
                    manualUpdateButton.style.display = DisplayStyle.Flex;
                else
                    CreateTooManyChildForAutoSizeCalculationWarning();

                return newBound;
            }

            if (manualUpdateButton != null)
                manualUpdateButton.style.display = DisplayStyle.None;

            bool firstRenderer = true;
            for (int i = 0; i < transforms.Length; ++i)
            {
                if (transforms[i].GetComponent<Renderer>() == null)
                    continue;

                if (firstRenderer)
                {
                    Bounds bounds = transforms[i].GetComponent<Renderer>().bounds;
                    bounds.center -= (transforms[i].position);
                    if (transforms[i] != transform)
                        bounds.center += transforms[i].localPosition;
                    //There's no such thing as an "empty" Bounds,
                    //you must create it with the "first" one. That's why the declared bounds is replaced,
                    //otherwise, bound will always count the declared 0,0,0 location as a valid one;
                    newBound = bounds;
                    firstRenderer = false;
                }
                else
                {
                    Bounds bounds = transforms[i].GetComponent<Renderer>().bounds;
                    bounds.center -= (target.position);
                    newBound.Encapsulate(bounds);
                }
            }

            return newBound;
        }

        /// <summary>
        /// The difference between local and world space size is, the world space size checks axis in world space and local takes target rotation into consideration
        /// </summary>
        /// <param name="target"></param>
        /// <param name="showWarningIfTooManyChild">If set to true, this will stop the update if there are too much child object. if false, it will always update</param>
        /// <returns></returns>
        private Bounds GetRendererLocalBounds(Transform target, bool showWarningIfTooManyChild = false)
        {
            Bounds newBound = new Bounds();


            Transform[] transforms = target.GetComponentsInChildren<Transform>()
                            .Where(t =>
                                !editorSettings.ignoreParticleAndVFXInSizeCalculation ||
                                (t.GetComponent<ParticleSystem>() == null && t.GetComponent<VisualEffect>() == null))
                            .ToArray();

            if (transforms.Length == 0)
                return newBound;

            if (showWarningIfTooManyChild && transforms.Length > editorSettings.MaxChildCountForSizeCalculation)
            {
                if (manualUpdateButton != null)
                    manualUpdateButton.style.display = DisplayStyle.Flex;
                else
                    CreateTooManyChildForAutoSizeCalculationWarning();

                return newBound;
            }

            if (manualUpdateButton != null)
                manualUpdateButton.style.display = DisplayStyle.None;

            temporarilyRotatedToCheckSize = true;

            Quaternion currentRotation;
            if (target.parent == null)
            {
                currentRotation = target.localRotation;
                target.localRotation = Quaternion.Euler(Vector3.zero);
            }
            else
            {
                currentRotation = target.rotation;
                target.rotation = Quaternion.Euler(Vector3.zero);
            }

            bool firstRenderer = true;
            for (int i = 0; i < transforms.Length; ++i)
            {
                if (transforms[i].GetComponent<Renderer>() == null)
                    continue;

                if (firstRenderer)
                {
                    Bounds bounds = transforms[i].GetComponent<Renderer>().bounds;
                    bounds.center -= (transforms[i].position);
                    if (transforms[i] != transform)
                        bounds.center += transforms[i].localPosition;

                    //There's no such thing as an "empty" Bounds,
                    //you must create it with the "first" one. That's why the declared bounds is replaced,
                    //otherwise, bound will always count the declared 0,0,0 location as a valid one;
                    newBound = bounds;
                    firstRenderer = false;
                }
                else
                {
                    Bounds bounds = transforms[i].GetComponent<Renderer>().bounds;
                    bounds.center -= (target.position);
                    newBound.Encapsulate(bounds);
                }
            }

            if (target.parent == null)
                target.localRotation = currentRotation;
            else
                target.rotation = currentRotation;

            return newBound;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="target"></param>
        /// <param name="showWarningIfTooManyChild"></param>
        /// <returns></returns>
        private Bounds CheckFilterBoundsWithChildren(Transform target, bool showWarningIfTooManyChild = false)
        {
            Transform[] transforms = target.GetComponentsInChildren<Transform>();
            if (showWarningIfTooManyChild)
            {
                if (transforms.Length > editorSettings.MaxChildCountForSizeCalculation)
                {
                    if (manualUpdateButton != null)
                        manualUpdateButton.style.display = DisplayStyle.Flex;
                    else
                        CreateTooManyChildForAutoSizeCalculationWarning();
                    return new Bounds();
                }
            }

            //This button is never created unless required.
            if (manualUpdateButton != null)
                manualUpdateButton.style.display = DisplayStyle.None;

            MeshFilter[] meshFilters = target.GetComponentsInChildren<MeshFilter>();
            Matrix4x4 worldToTargetLocal = target.worldToLocalMatrix;

            Bounds bounds = new();
            bool firstBounds = true;

            foreach (MeshFilter meshFilter in meshFilters)
            {
                Mesh mesh = meshFilter.sharedMesh;

                if (mesh == null || mesh.vertexCount == 0) continue;

                Matrix4x4 meshToWorld = meshFilter.transform.localToWorldMatrix;

                Vector3[] vertices = mesh.vertices;
                foreach (Vector3 vertex in vertices)
                {
                    // Convert vertex to world space, applying position, rotation, and scale
                    Vector3 worldVertex = meshToWorld.MultiplyPoint3x4(vertex);

                    // Convert world space vertex back to target's local space
                    Vector3 localVertex = worldToTargetLocal.MultiplyPoint3x4(worldVertex);

                    if (firstBounds)
                    {
                        bounds = new Bounds(localVertex, Vector3.zero); // Initialize bounds in local space
                        firstBounds = false;
                    }
                    else
                    {
                        bounds.Encapsulate(localVertex);
                    }
                }
            }

            bounds.size = Multiply(bounds.size, transform.lossyScale);
            bounds.center = Multiply(bounds.center, transform.lossyScale);
            return bounds;
        }

        #endregion Check Size

        #endregion Size

        #region Notes

        private bool thisIsAnAsset = false;

        private TextField noteTextField;
        private readonly string noNoteString = "This does not have a note. Click to add one.";

        private Button noteToolbarButton;

        private string myNote;
        private NoteType noteType;
        private Color noteColor;
        bool showThisNoteInSceneView;

        private GroupBox noteEditGroupBox;
        private GroupBox noteTopGroupBox;
        private GroupBox noteBottomGroupBox;
        private Button noteChangeDoneButton;
        private Button noteChangeCancelButton;
        private Button noteDeleteButton;
        private EnumField noteTypeField;
        Toggle showThisNoteGizmoInSceneToggle;
        private ColorField noteColorField;

        private SceneNotesManager sceneNotesManager;

        private bool noteSetupCompleted = false;

        private void SetupNote()
        {
            noteTopGroupBox = root.Q<GroupBox>("NoteTopGroupBox");
            noteTopGroupBox.style.display = DisplayStyle.None;

            noteBottomGroupBox = root.Q<GroupBox>("NoteBottomGroupBox");
            noteBottomGroupBox.style.display = DisplayStyle.None;

            noteEditGroupBox = root.Q<GroupBox>("NoteEditGroupBox");
            noteEditGroupBox.style.display = DisplayStyle.None;

            thisIsAnAsset = AssetDatabase.Contains(transform);

            Label assetGUIDLabel = root.Q<Label>("GUID");
            assetGUIDLabel.style.display = DisplayStyle.None;

            if (thisIsAnAsset && editorSettings.showAssetGUID)
            {
                string myGUID = GetID();
                assetGUIDLabel.text = myGUID;
                assetGUIDLabel.tooltip = "GUID\n" + myGUID;
                assetGUIDLabel.style.display = DisplayStyle.Flex;
            }

            noteToolbarButton = root.Q<Button>("NoteToolbarButton");

            if (!editorSettings.ShowNotes)
            {
                noteToolbarButton.style.display = DisplayStyle.None;
                return;
            }

            InitialNoteSetup();
        }

        private bool initialNoteSetupDone = false;

        private void InitialNoteSetup()
        {
            if (initialNoteSetupDone)
                return;

            initialNoteSetupDone = true;

            noteColorField = root.Q<ColorField>("NoteBGColor");
            noteColorField.RegisterValueChangedCallback(e =>
            {
                noteColor = e.newValue;
            });

            if (!thisIsAnAsset)
                GetNotesManager();

            noteToolbarButton.RegisterCallback<MouseEnterEvent>(e =>
            {
                NoteToolbarHovered();
            });

            noteToolbarButton.clicked += ToggleNoteEditor;

            noteBottomGroupBox.Q<Button>().clicked += ToggleNoteEditor;
            noteTopGroupBox.Q<Button>().clicked += ToggleNoteEditor;

            //Try to get note
            if (string.IsNullOrEmpty(myNote))
            {
                if (thisIsAnAsset)
                {
                    myNote = editorSettings.GetNote(GetID());
                }
                else
                {
                    if (sceneNotesManager != null)
                    {
                        var note = sceneNotesManager.MyNote(transform);
                        if (note != null)
                        {
                            myNote = note.note;
                            noteType = note.noteType;
                            showThisNoteInSceneView = note.showInSceneView;
                        }
                        ////TODO. These two should be one
                        //myNote = sceneNotesManager.GetNote(transform);
                        //noteType = sceneNotesManager.GetNoteType(transform);
                    }
                }

                if (string.IsNullOrWhiteSpace(myNote))
                    myNote = noNoteString;
            }

            UpdateNoteType();
        }

        private void SetupNote_complete()
        {
            if (!initialNoteSetupDone)
            {
                InitialNoteSetup();
                //Debug.Log("Initial setup being done in full setup");
            }

            noteTextField = root.Q<TextField>("Note");
            if (noteColorField == null)
                noteColorField = root.Q<ColorField>("NoteBGColor");

            showThisNoteGizmoInSceneToggle = root.Q<Toggle>("ShowThisNoteGizmoInSceneToggle");
            showThisNoteGizmoInSceneToggle.RegisterValueChangedCallback(e =>
            {
                showThisNoteInSceneView = e.newValue;
            });

            noteTypeField = root.Q<EnumField>("NoteType");
            noteTypeField.Init(NoteType.tooltip);
            noteTypeField.RegisterValueChangedCallback(e =>
            {
                noteType = (NoteType)e.newValue;
                //UpdateNoteType();
            });

            noteChangeDoneButton = noteEditGroupBox.Q<Button>("NoteChangeDoneButton");
            noteChangeDoneButton.clicked += SaveNoteChange;

            noteChangeCancelButton = noteEditGroupBox.Q<Button>("NoteChangeCancelButton");
            noteChangeCancelButton.clicked += CancelNoteChange;

            noteDeleteButton = noteEditGroupBox.Q<Button>("NoteDeleteButton");
            noteDeleteButton.clicked += DeleteNote;

            noteSetupCompleted = true;
        }

        /// <summary>
        /// Doesn't create if nothing is in the scene
        /// </summary>
        private void GetNotesManager()
        {
            //Already assigned, no need to search
            if (sceneNotesManager != null)
                return;

            //Get from scene
            sceneNotesManager = SceneNotesManager.Instance;
        }

        private void IfReuquiredCreateSceneNoteManager()
        {
            //If found from scene, no need to do anything else
            if (sceneNotesManager != null)
                return;

            //Don't add stuff during game time.
            if (Application.isPlaying)
                return;

            GameObject sceneNoteManagerObject = new GameObject("Better Transform Scene Notes Manager"); //TODO: isn't there supposed to be a undo.creategameobjct  type method for editor
            sceneNotesManager = sceneNoteManagerObject.AddComponent<SceneNotesManager>();
            EditorUtility.SetDirty(sceneNoteManagerObject);
        }

        private void NoteToolbarHovered()
        {
            if (!noteSetupCompleted) SetupNote_complete();

            UpdateNoteTooltip();
        }

        //Settings uses current method to find the object again when cleaning up,
        //If this is updated, that needs to be updated as well.
        private string GetID()
        {
            if (AssetDatabase.Contains(transform))
            {
                string guid;
                long file;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out guid, out file);

                return guid;
            }

            return transform.GetInstanceID().ToString();
        }

        private void UpdateNoteTooltip()
        {
            //If failed,
            if (string.IsNullOrWhiteSpace(myNote))
                noteToolbarButton.tooltip = noNoteString;
            else
                noteToolbarButton.tooltip = myNote;
        }

        private void ToggleNoteEditor()
        {
            //if (!noteSetupCompleted) SetupNote_complete();
            if (NoteEditBoxOpen())
                noteEditGroupBox.style.display = DisplayStyle.None;
            else
                OpenNoteEditBox();
        }

        private bool NoteEditBoxOpen() => noteEditGroupBox.style.display != DisplayStyle.None;

        private void OpenNoteEditBox()
        {
            //noteColorField null check was added due to domain reload clearing the reference
            if (!noteSetupCompleted || noteColorField == null) SetupNote_complete();

            UpdateNoteTextFieldFromSavedNote();

            noteEditGroupBox.style.display = DisplayStyle.Flex;

            noteTypeField ??= root.Q<EnumField>("NoteType");
            showThisNoteGizmoInSceneToggle ??= root.Q<Toggle>("ShowThisNoteGizmoInSceneToggle");

            noteTypeField.value = GetNoteType();
            noteColor = GetNoteColor();
            noteColorField.value = noteColor;
            showThisNoteGizmoInSceneToggle.SetValueWithoutNotify(showThisNoteInSceneView);
            if (!editorSettings.ShowNotesOnGizmo)
            {
                showThisNoteGizmoInSceneToggle.SetEnabled(false);
                showThisNoteGizmoInSceneToggle.tooltip = "Notes in scene view has been turned off from settings.";
            }
            else
            {
                showThisNoteGizmoInSceneToggle.SetEnabled(true);
                if (showThisNoteGizmoInSceneToggle.tooltip == "Notes in scene view has been turned off from settings.")
                    showThisNoteGizmoInSceneToggle.tooltip = "";

            }

            UpdateNoteTextFieldFromSavedNote();
        }

        private NoteType GetNoteType()
        {
            if (thisIsAnAsset)
            {
                //TODO
            }

            if (sceneNotesManager != null)
            {
                return sceneNotesManager.GetNoteType(transform);
            }

            return NoteType.tooltip;
        }

        private Color GetNoteColor()
        {
            if (thisIsAnAsset)
            {
                //TODO
            }

            if (sceneNotesManager != null)
            {
                return sceneNotesManager.GetNoteColor(transform);
            }

            return Color.gray;
        }

        private void UpdateNoteTextFieldFromSavedNote()
        {
            if (noteTextField == null)
                noteTextField = root.Q<TextField>("Note");

            if (string.IsNullOrWhiteSpace(myNote) || myNote == noNoteString)
            {
                noteTextField.SetValueWithoutNotify("");
                noteDeleteButton.style.display = DisplayStyle.None;
            }
            else
            {
                noteTextField.SetValueWithoutNotify(myNote);
                noteDeleteButton.style.display = DisplayStyle.Flex;
            }
        }

        private void SaveNoteChange()
        {
            myNote = noteTextField.value;

            if (thisIsAnAsset)
            {
                editorSettings.SetNote(GetID(), myNote, noteType, noteColor, showThisNoteInSceneView);
            }
            else
            {
                IfReuquiredCreateSceneNoteManager();

                if (sceneNotesManager != null)
                {
                    sceneNotesManager.SetNote(transform, myNote, noteType, noteColor, showThisNoteInSceneView);
                }
            }

            UpdateNoteTooltip();
            UpdateNoteTextFieldFromSavedNote();
            UpdateNoteType();

            noteEditGroupBox.style.display = DisplayStyle.None;
        }

        private void UpdateNoteType()
        {
            noteToolbarButton.style.display = DisplayStyle.None;
            noteTopGroupBox.style.display = DisplayStyle.None;
            noteBottomGroupBox.style.display = DisplayStyle.None;

            if (targets.Count() > 1 || myNote == noNoteString || string.IsNullOrEmpty(myNote))
                return;

            if (noteType == NoteType.tooltip)
            {
                noteToolbarButton.style.display = DisplayStyle.Flex;
                noteTopGroupBox.style.display = DisplayStyle.None;
                noteBottomGroupBox.style.display = DisplayStyle.None;

                return;
            }

            noteToolbarButton.style.display = DisplayStyle.None;

            if (noteType == NoteType.fullWidthTop)
            {
                noteTopGroupBox.style.display = DisplayStyle.Flex;
                noteTopGroupBox.Q<Label>("Note").text = myNote;

                if (sceneNotesManager != null) noteTopGroupBox.style.backgroundColor = sceneNotesManager.GetNoteColor(transform);
                noteBottomGroupBox.style.display = DisplayStyle.None;
            }
            else
            {
                noteTopGroupBox.style.display = DisplayStyle.None;

                noteBottomGroupBox.style.display = DisplayStyle.Flex;
                noteBottomGroupBox.Q<Label>("Note").text = myNote;
                if (sceneNotesManager != null) noteBottomGroupBox.style.backgroundColor = sceneNotesManager.GetNoteColor(transform);
            }
        }

        private void CancelNoteChange()
        {
            UpdateNoteTextFieldFromSavedNote();

            noteEditGroupBox.style.display = DisplayStyle.None;
        }

        private void DeleteNote()
        {
            noteEditGroupBox.style.display = DisplayStyle.None;

            if (thisIsAnAsset)
            {
                editorSettings.DeleteNote(GetID());
            }
            else
            {
                if (sceneNotesManager != null)
                {
                    Debug.Log("Note deleted.");
                    sceneNotesManager.DeleteNote(transform);
                }
            }

            myNote = noNoteString;

            UpdateNoteTooltip();

            UpdateNoteType();

            noteEditGroupBox.style.display = DisplayStyle.None;
        }

        #endregion Notes

        #region Parent Child

        private GroupBox parentGroupBox;

        private void SetupParentChild(VisualElement root, CustomFoldoutSetup customFoldoutSetup)
        {
            if (!editorSettings.ShowParentChildTransform || targets.Length > 1)
            {
                root.Q<GroupBox>("ChildGroupBox").style.display = DisplayStyle.None;
                return;
            }

            if (folderTemplate == null)
                folderTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(folderTemplateFileLocation);

            if (folderTemplate == null)
                return;

            GroupBox rootHolder = root.Q<GroupBox>("RootHolder");

            SetupParent(customFoldoutSetup, rootHolder);
            SetupChildren(customFoldoutSetup, rootHolder);
        }

        private void UpdateSetupParentChildFoldouts()
        {
            GroupBox rootHolder = root.Q<GroupBox>("RootHolder");
            if (!editorSettings.ShowParentChildTransform)
            {
                rootHolder.Q<GroupBox>("ChildGroupBox").style.display = DisplayStyle.None;

                if (parentGroupBox != null)
                    parentGroupBox.style.display = DisplayStyle.None;

                return;
            }
            else
            {
                if (transform.childCount > 0)
                {
                    root.Q<GroupBox>("ChildGroupBox").style.display = DisplayStyle.Flex;
                    if (!alreadySetupChildren) SetupChildren(customFoldoutSetup, rootHolder);
                }
                if (transform.parent)
                {
                    if (parentGroupBox != null)
                        parentGroupBox.style.display = DisplayStyle.Flex;

                    if (!alreadySetupParent) SetupParent(customFoldoutSetup, rootHolder);
                }
            }
        }

        private bool alreadySetupChildren = false;

        private void SetupChildren(CustomFoldoutSetup customFoldoutSetup, GroupBox rootHolder)
        {
            alreadySetupChildren = true;
            GroupBox childGroupBox = rootHolder.Q<GroupBox>("ChildGroupBox");

            if (transform.childCount > 0)
            {
                childGroupBox.style.display = DisplayStyle.Flex;
                customFoldoutSetup.SetupFoldout(childGroupBox);

                GroupBox warning = rootHolder.Q<GroupBox>("TooManyChildForInspectorCreation");
                if (transform.childCount <= editorSettings.MaxChildInspector)
                {
                    warning.style.display = DisplayStyle.None;
                    foreach (Transform child in transform)
                    {
                        if (child != originalTransform)
                            CreateEditor(customFoldoutSetup, childGroupBox.Q<GroupBox>("Content"), child.gameObject.name, child);
                    }
                }
                else
                {
                    warning.style.display = DisplayStyle.Flex;
                }

                childGroupBox.Q<Label>("Label1").text = "Child count: " + transform.childCount;
                Transform[] transforms = transform.GetComponentsInChildren<Transform>();
                Label label2 = childGroupBox.Q<Label>("Label2");
                if (transforms.Length - 1 != transform.childCount)
                {
                    label2.text = "Recursive child count: " + (transforms.Length - 1);
                    label2.style.display = DisplayStyle.Flex;
                }
                else
                {
                    label2.style.display = DisplayStyle.None;
                }
            }
            else
            {
                childGroupBox.style.display = DisplayStyle.None;
            }
        }

        private bool alreadySetupParent = false;

        private void SetupParent(CustomFoldoutSetup customFoldoutSetup, GroupBox rootHolder)
        {
            if (transform.parent)
            {
                alreadySetupParent = true;
                if (transform.parent != originalTransform)
                {
                    if (parentGroupBox == null)
                    {
                        parentGroupBox = new GroupBox();
                        parentGroupBox.style.marginTop = 0;
                        parentGroupBox.style.marginRight = 0;
                        parentGroupBox.style.marginBottom = 0;
                        parentGroupBox.style.paddingTop = 0;
                        parentGroupBox.style.paddingBottom = 0;
                        rootHolder.Add(parentGroupBox);
                    }
                    CreateEditor(customFoldoutSetup, parentGroupBox, "(Parent) " + transform.parent.gameObject.name, transform.parent, true);
                }
            }
        }

        private void CreateEditor(CustomFoldoutSetup customFoldoutSetup, GroupBox rootHolder, string foldoutName, Transform targetTransform, bool margin = false)
        {
            VisualElement visualElement = new VisualElement();
            rootHolder.Add(visualElement);
            folderTemplate.CloneTree(visualElement);
            GroupBox container = visualElement.Q<GroupBox>("TemplateRoot");
            if (margin)
            {
                container.style.marginLeft = -7;
                container.style.marginRight = 0;
            }
            customFoldoutSetup.SetupFoldout(container);

            GroupBox content = container.Q<GroupBox>("Content");

            Toolbar editorHeader = container.Q<Toolbar>("EditorHeader");
            editorHeader.style.display = DisplayStyle.None;

            Button openEditorButton = container.Q<Button>("OpenEditorButton");
            openEditorButton.style.display = DisplayStyle.Flex;

            Toggle toggle = container.Q<Toggle>("FoldoutToggle");
            toggle.text = foldoutName;
            toggle.RegisterValueChangedCallback(e =>
            {
                if (openEditorButton.style.display == DisplayStyle.Flex)
                {
                    openEditorButton.style.display = DisplayStyle.None;
                    editorHeader.style.display = DisplayStyle.Flex;
                    OpenEditor(targetTransform, content);
                }
            });

            Label label1 = container.Q<Label>("Label1");
            UpdateSiblingIndex(targetTransform, label1);

            Label label2 = container.Q<Label>("Label2");
            label2.style.display = DisplayStyle.None;

            //    openEditorButton.clicked += () =>
            //    {
            //        openEditorButton.style.display = DisplayStyle.None;
            //        editorHeader.style.display = DisplayStyle.Flex;
            //        OpenEditor(targetTransform, content);
            //    };
        }

        private void UpdateSiblingIndex(Transform targetTransform, Label label)
        {
            if (targetTransform.parent)
            {
                int siblingIndex = targetTransform.GetSiblingIndex();
                label.text = "Sibling index: " + siblingIndex;
                label.tooltip = "The game object \"" + targetTransform.gameObject.name + "\" is the number " + (siblingIndex + 1) + " child object of \"" + targetTransform.parent.gameObject.name + "\".\n\n" +
                    "If a GameObject shares a parent with other GameObjects and are on the same level (i.e. they share the same direct parent), these GameObjects are known as siblings.\n" +
                    "The sibling index shows where each GameObject sits in this sibling hierarchy.";
            }
            else
            {
                label.style.display = DisplayStyle.None;
            }
        }

        private void OpenEditor(Transform targetTransform, GroupBox container)
        {
            BetterTransformEditor newEditor = (BetterTransformEditor)BetterTransformEditor.CreateEditor(targetTransform);

            if (originalTransform == null) originalTransform = transform;
            //VisualElement inspector = newEditor.CreateInspectorGUI();
            VisualElement inspector = newEditor.CreateInspectorInsideAnother(transform);

            otherBetterTransformEditors.Add(newEditor);
            container.Add(inspector);

            ToolbarButton pingButton = container.Q<ToolbarButton>("Ping");
            //TargetEditorName.text = targetTransform.gameObject.name;
            //TargetEditorName.text = "Ping";
            pingButton.clicked += () =>
            {
                EditorGUIUtility.PingObject(targetTransform.gameObject);
            };
        }

        #endregion Parent Child

        #region Add Functionality

        #region Variables

        private Button settingsButton;
        private GenericDropdownMenu settingsMenuButton;

        #endregion Variables

        private void SetupAddFunctionality()
        {
            settingsButton = topGroupBox.Q<Button>("AddButton");

            if (targets.Length != 1)
            {
                settingsButton.style.visibility = Visibility.Hidden;
                return;
            }

            settingsButton.clicked += () => OpenContextMenu_settings();
        }

        private void OpenContextMenu_settings()
        {
            UpdateContextMenu_addFunctionality();
            settingsMenuButton.DropDown(GetMenuRect(settingsButton), settingsButton, true);
        }

        private void UpdateContextMenu_addFunctionality()
        {
            settingsMenuButton = new GenericDropdownMenu();
            if (settingsFoldout == null || settingsFoldout.style.display == DisplayStyle.None)
                settingsMenuButton.AddItem("Open Settings", false, () => OpenSettings());
            else
                settingsMenuButton.AddItem("Close Settings", false, () => ToggleSettings(false));

            settingsMenuButton.AddSeparator("");

            if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.Both)
            {
                settingsMenuButton.AddItem("Both Space Together", true, () =>
                {
                    editorSettings.CurrentWorkSpace = BetterTransformSettings.WorkSpace.Local;
                    UpdateMainControls();
                    UpdateSize();
                });
                settingsMenuButton.AddDisabledItem("Default Inspector for Local Fields", true);
            }
            else
            {
                settingsMenuButton.AddItem("Both Space Together", false, () =>
                {
                    editorSettings.CurrentWorkSpace = BetterTransformSettings.WorkSpace.Both;
                    UpdateMainControls();
                    UpdateSize();
                });
                settingsMenuButton.AddItem("Default Inspector for Local Fields", editorSettings.LoadDefaultInspector, () =>
                {
                    editorSettings.LoadDefaultInspector = !editorSettings.LoadDefaultInspector;
                    UpdateMainControls();
                });
            }

            settingsMenuButton.AddSeparator("");
            if (editorSettings.ShowNotes)
            {
                if (!NoteEditBoxOpen())
                {
                    string noteLabel;
                    if (string.IsNullOrEmpty(myNote) || myNote == noNoteString)
                        noteLabel = "Add note";
                    else
                        noteLabel = "Modify note";
                    settingsMenuButton.AddItem(noteLabel, false, () => OpenNoteEditBox());
                }
            }

            settingsMenuButton.AddSeparator("");

            bool hierarchySize = editorSettings.IncludeChildBounds;
            settingsMenuButton.AddItem("Hierarchy Size", hierarchySize, () =>
            {
                editorSettings.IncludeChildBounds = true;
                UpdateSizeInclusionButtons();
                SceneView.RepaintAll();
            });
            settingsMenuButton.AddItem("Self Size", !hierarchySize, () =>
            {
                editorSettings.IncludeChildBounds = false;
                UpdateSizeInclusionButtons();
                SceneView.RepaintAll();
            });

            settingsMenuButton.AddSeparator("");

          
            settingsMenuButton.AddItem("Renderer Size", editorSettings.CurrentSizeType == BetterTransformSettings.SizeType.Renderer, () =>
            {
                editorSettings.CurrentSizeType = BetterTransformSettings.SizeType.Renderer;
                UpdateSizeTypeButtons();
                UpdateSize();
            });

            settingsMenuButton.AddItem("Mesh Filter Only Size", editorSettings.CurrentSizeType == BetterTransformSettings.SizeType.Filter, () =>
            {
                editorSettings.CurrentSizeType = BetterTransformSettings.SizeType.Filter;
                UpdateSizeTypeButtons();
                UpdateSize();
            });

            settingsMenuButton.AddSeparator("");
            settingsMenuButton.AddItem("Include Child Objects in Size Calculation", editorSettings.IncludeChildBounds, () =>
            {
                editorSettings.IncludeChildBounds = !editorSettings.IncludeChildBounds;
                UpdateSizeInclusionButtons();

                SceneView.RepaintAll();
            });


            if (editorSettings.CurrentSizeType == BetterTransformSettings.SizeType.Filter)
                settingsMenuButton.AddDisabledItem("Ignore Particle and VFX Renderer", editorSettings.ignoreParticleAndVFXInSizeCalculation);
            else
            {
                settingsMenuButton.AddItem("Ignore Particle and VFX Renderer", editorSettings.ignoreParticleAndVFXInSizeCalculation, () =>
                {
                    editorSettings.ignoreParticleAndVFXInSizeCalculation = !editorSettings.ignoreParticleAndVFXInSizeCalculation;
                    editorSettings.Save();
                    UpdateSize(true);
                    SceneView.RepaintAll();
                });
            }
        }

        private Rect GetMenuRect(VisualElement anchor)
        {
            var worldBound = anchor.worldBound;
            worldBound.xMin -= 250;
            worldBound.xMax += 0;
            return worldBound;
        }

        #endregion Add Functionality

        #region Settings

        /// <summary>
        /// These is no need to register callbacks for every field in the setting when it will remain unused most of the times,
        /// This can be used to track if the setup is done, and if not, can be done by calling SetupSettingsField() method.
        /// </summary>
        private bool settingsFieldSetupDone = false;

        private Toggle roundPositionFieldToggle;
        private Toggle roundRotationFieldToggle;
        private Toggle roundScaleFieldToggle;


        private void OpenSettings()
        {
            if (!settingsFieldSetupDone)
            {
                SetupSettingsFoldouts();
                SetupSettingsFields();
            }

            ToggleSettings();
        }

        private ColorField gizmoColorField;

        private GroupBox settingsFoldout;

        private void SetupSettingsFoldouts()
        {
            settingsFoldout = root.Q<GroupBox>("Settings");
            customFoldoutSetup.SetupFoldout(settingsFoldout);
            customFoldoutSetup.SetupFoldout(settingsFoldout.Q<GroupBox>("InspectorCustomizationSettings"));
            customFoldoutSetup.SetupFoldout(settingsFoldout.Q<GroupBox>("MainInformationSettings"));
            customFoldoutSetup.SetupFoldout(settingsFoldout.Q<GroupBox>("SizeGroupBox"));
            customFoldoutSetup.SetupFoldout(settingsFoldout.Q<GroupBox>("GizmoSettingsGroupBox"), "FoldoutToggle", "GizmoLabel");
            customFoldoutSetup.SetupFoldout(settingsFoldout.Q<GroupBox>("PrefabNotesGroupBox"), "FoldoutToggle", "PrefabNotesToggle");
            customFoldoutSetup.SetupFoldout(settingsFoldout.Q<GroupBox>("UtilitySettings"));

            GroupBox content = settingsFoldout.Q<GroupBox>("Content");
            Toggle foldoutToggle = settingsFoldout.Q<Toggle>("FoldoutToggle");
            foldoutToggle.RegisterValueChangedCallback(ev =>
            {
                ToggleSettings(ev.newValue);
            });
            foldoutToggle.value = false;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="value">Turn on or off</param>
        private void ToggleSettings(bool value)
        {
            Toggle foldoutToggle = settingsFoldout.Q<Toggle>("FoldoutToggle");
            GroupBox content = settingsFoldout.Q<GroupBox>("Content");

            //When on, this is called AFTER foldout value has been set. If this causes issue, schedule the binding
            ToggleSettings(customFoldoutSetup, settingsFoldout, foldoutToggle, content, value);
        }

        private void ToggleSettings()
        {
            Toggle foldoutToggle = settingsFoldout.Q<Toggle>("FoldoutToggle");
            GroupBox content = settingsFoldout.Q<GroupBox>("Content");

            //When on, this is called AFTER foldout value has been set. If this causes issue, schedule the binding
            ToggleSettings(customFoldoutSetup, settingsFoldout, foldoutToggle, content, !foldoutToggle.value);
        }

        private void ToggleSettings(CustomFoldoutSetup customFoldoutSetup, GroupBox settings, Toggle foldoutToggle, GroupBox content, bool turnOn)
        {
            customFoldoutSetup.SwitchContent(content, turnOn);

            //Turn on settings
            if (turnOn)
            {
                foldoutToggle.SetValueWithoutNotify(true);
                settings.style.display = DisplayStyle.Flex;
            }
            //Turn off settings
            else
            {
                foldoutToggle.schedule.Execute(() => TurnOffSettings(settings, foldoutToggle)).ExecuteLater(200);
            }
        }

        //TODO: Q the content
        //This is only called after the settings button is clicked.
        //This is to reduce workload when something is clicked and not unnecessarily assign stuff.
        private void SetupSettingsFields()
        {
            settingsFieldSetupDone = true;

            GroupBox inspectorSettingsFoldout = root.Q<GroupBox>("Settings");

            SetupFooter(root);

            Toggle defaultUnityInspectorToggle = inspectorSettingsFoldout.Q<Toggle>("DefaultUnityInspectorToggle");
            defaultUnityInspectorToggle.SetValueWithoutNotify(editorSettings.LoadDefaultInspector);
            defaultUnityInspectorToggle.RegisterValueChangedCallback(e =>
            {
                editorSettings.LoadDefaultInspector = e.newValue;
                UpdateMainControls();
            });

            Toggle foldoutAnimationsToggle = inspectorSettingsFoldout.Q<Toggle>("FoldoutAnimationsToggle");
            foldoutAnimationsToggle.SetValueWithoutNotify(editorSettings.animatedFoldout);
            foldoutAnimationsToggle.RegisterValueChangedCallback(e =>
            {
                editorSettings.animatedFoldout = e.newValue;
                editorSettings.Save();

                if (!e.newValue)
                {
                    if (root.styleSheets.Contains(animatedFoldoutStyleSheet))
                    {
                        root.styleSheets.Remove(animatedFoldoutStyleSheet);
                    }
                }
                else
                {
                    if (!root.styleSheets.Contains(animatedFoldoutStyleSheet))
                    {
                        root.styleSheets.Add(animatedFoldoutStyleSheet);
                    }
                }
            });

            ColorField inspectorColorField = inspectorSettingsFoldout.Q<ColorField>("InspectorColorField");
            Toggle overrideInspectorColor = inspectorSettingsFoldout.Q<Toggle>("OverrideInspectorColorToggle");
            ColorField foldoutColorField = inspectorSettingsFoldout.Q<ColorField>("FoldoutColorField");
            Toggle overrideFoldoutColorToggle = inspectorSettingsFoldout.Q<Toggle>("OverrideFoldoutColorToggle");
            SetupInspectorColorSettings(inspectorColorField, overrideInspectorColor, foldoutColorField, overrideFoldoutColorToggle);

            Toggle copyPasteButtonsToggle = inspectorSettingsFoldout.Q<Toggle>("CopyPasteButtonsToggle");
            SetupCopyPasteButtonsToggle(copyPasteButtonsToggle);

            IntegerField fieldRoundingField = inspectorSettingsFoldout.Q<IntegerField>("FieldRounding");
            SetupFieldRoundingField(fieldRoundingField);

            roundPositionFieldToggle = inspectorSettingsFoldout.Q<Toggle>("RoundPositionFieldToggle");
            roundPositionFieldToggle.value = editorSettings.roundPositionField;
            roundPositionFieldToggle.RegisterValueChangedCallback(ev => { TogglePositionFieldRounding(); });

            roundRotationFieldToggle = inspectorSettingsFoldout.Q<Toggle>("RoundRotationFieldToggle");
            roundRotationFieldToggle.value = editorSettings.roundRotationField;
            roundRotationFieldToggle.RegisterValueChangedCallback(ev => { ToggleRotationFieldRounding(); });

            roundScaleFieldToggle = inspectorSettingsFoldout.Q<Toggle>("RoundScaleFieldToggle");
            roundScaleFieldToggle.value = editorSettings.roundScaleField;
            roundScaleFieldToggle.RegisterValueChangedCallback(ev => { ToggleScaleFieldRounding(); });

            Toggle sizeFoldoutToggle = inspectorSettingsFoldout.Q<Toggle>("ShowSizeFoldoutToggle");
            SetupSizeFoldoutToggle(inspectorSettingsFoldout, sizeFoldoutToggle);

            Toggle showWhySizeFoldoutIsHidden = inspectorSettingsFoldout.Q<Toggle>("ShowWhySizeFoldoutIsHidden");
            showWhySizeFoldoutIsHidden.SetValueWithoutNotify(editorSettings.showWhySizeIsHiddenLabel);
            showWhySizeFoldoutIsHidden.RegisterValueChangedCallback(e =>
            {
                editorSettings.showWhySizeIsHiddenLabel = e.newValue;
                editorSettings.Save();
                UpdateSize(false);
            });

            Toggle ignoreParticleAndVFX = inspectorSettingsFoldout.Q<Toggle>("IgnoreParticleAndVFX");
            ignoreParticleAndVFX.SetValueWithoutNotify(editorSettings.ignoreParticleAndVFXInSizeCalculation);
            ignoreParticleAndVFX.RegisterValueChangedCallback(e =>
            {
                editorSettings.ignoreParticleAndVFXInSizeCalculation = e.newValue;
                editorSettings.Save();
                UpdateSize(true);
                SceneView.RepaintAll();
            });

            Toggle gizmoLabel = inspectorSettingsFoldout.Q<Toggle>("GizmoLabel");
            SetupGizmoLabel(inspectorSettingsFoldout, gizmoLabel);

            IntegerField sizeGizmoLabelSizeField = inspectorSettingsFoldout.Q<IntegerField>("SizeGizmoLabelSize");
            SetupSizeGizmoLabelSizeField(sizeGizmoLabelSizeField);

            gizmoColorField = inspectorSettingsFoldout.Q<ColorField>("GizmoColor");
            SetupGizmoColorField(gizmoColorField);

            Toggle sizeGizmoLabelBothSideToggle = inspectorSettingsFoldout.Q<Toggle>("SizeGizmoLabelBothSide");
            SetupSizeGizmoLabelBothSideToggle(inspectorSettingsFoldout, sizeGizmoLabelBothSideToggle);

            IntegerField minimumSizeForDoubleLabel = inspectorSettingsFoldout.Q<IntegerField>("MinimumSizeForDoubleLabel");
            SetupMinimumSizeForDoubleLabel(minimumSizeForDoubleLabel);

            IntegerField gizmoMaximumDecimalPoints = inspectorSettingsFoldout.Q<IntegerField>("GizmoMaximumDecimalPoints");
            gizmoMaximumDecimalPoints.value = editorSettings.GizmoMaximumDecimalPoints;
            gizmoMaximumDecimalPoints.RegisterValueChangedCallback(ev =>
            {
                if (ev.newValue < 0)
                {
                    gizmoMaximumDecimalPoints.SetValueWithoutNotify(0);
                    editorSettings.GizmoMaximumDecimalPoints = 0;
                    SceneView.RepaintAll();
                    return;
                }

                editorSettings.GizmoMaximumDecimalPoints = ev.newValue;
                SceneView.RepaintAll();
            });

            Toggle showSiblingIndexToggle = inspectorSettingsFoldout.Q<Toggle>("ShowSiblingIndexToggle");
            showSiblingIndexToggle.value = editorSettings.showSiblingIndex;
            showSiblingIndexToggle.RegisterValueChangedCallback(ev =>
            {
                editorSettings.showSiblingIndex = ev.newValue;

                if (editorSettings.showSiblingIndex && transform.parent)
                {
                    siblingIndexLabel.style.display = DisplayStyle.Flex;
                    UpdateSiblingIndex(transform, siblingIndexLabel);
                }
                else
                {
                    siblingIndexLabel.style.display = DisplayStyle.None;
                }
            });

            Toggle ShowAssetGUID = inspectorSettingsFoldout.Q<Toggle>("ShowAssetGUID");
            ShowAssetGUID.value = editorSettings.showAssetGUID;
            ShowAssetGUID.RegisterValueChangedCallback(ev =>
            {
                editorSettings.showAssetGUID = ev.newValue;
                editorSettings.Save();

                Label idLabel = root.Q<Label>("GUID");
                idLabel.style.display = DisplayStyle.None;
                if (editorSettings.showAssetGUID)
                    if (thisIsAnAsset)
                    {
                        idLabel.style.display = DisplayStyle.Flex;
                        string id = GetID();
                        idLabel.text = id;
                        idLabel.tooltip = "GUID\n" + id;
                    }
            });

            Toggle labelHandlesToggle = inspectorSettingsFoldout.Q<Toggle>("LabelHandles");
            SetupLabelHandlesToggle(labelHandlesToggle);

            Toggle parentChildTransformsToggle = inspectorSettingsFoldout.Q<Toggle>("ParentChildTransformsToggle");
            SetupParentChildTransformsToggle(parentChildTransformsToggle);

            Toggle pingSelfButton = inspectorSettingsFoldout.Q<Toggle>("PingSelfButton");
            pingSelfButton.SetValueWithoutNotify(editorSettings.pingSelfButton);
            pingSelfButton.RegisterValueChangedCallback(e =>
            {
                editorSettings.pingSelfButton = e.newValue;

                if (e.newValue)
                    root.Q<Button>("PingSelfButton").style.display = DisplayStyle.Flex;
                else
                    root.Q<Button>("PingSelfButton").style.display = DisplayStyle.None;
            });



            IntegerField maxChildInspector = inspectorSettingsFoldout.Q<IntegerField>("MaxChildInspector");
            SetupMaxChildInspector(maxChildInspector);

            Button scaleSettingsButton = root.Q<Button>("ScaleSettingsButton"); //TODO: Why is it checking root instead of inspector settings foldout
            scaleSettingsButton.clicked += () => { SettingsService.OpenProjectSettings("Project/Tiny Giant Studio/Scale Settings"); };

            Toggle autoSizeUpdate = inspectorSettingsFoldout.Q<Toggle>("ConstantlyUpdateSize");
            autoSizeUpdate.value = editorSettings.ConstantSizeUpdate;
            autoSizeUpdate.RegisterValueChangedCallback(e =>
             {
                 editorSettings.ConstantSizeUpdate = e.newValue;
                 if (e.newValue == true)
                     StartSizeSchedule();
                 else
                     RemoveSizeUpdateScheduler();
             });

            Toggle prefabNotesToggle = inspectorSettingsFoldout.Q<Toggle>("PrefabNotesToggle");
            UpdateNotesToggle(prefabNotesToggle);

            Toggle showNotesInSceneGizmoToggle = inspectorSettingsFoldout.Q<Toggle>("ShowNotesInSceneGizmoToggle");
            showNotesInSceneGizmoToggle.SetValueWithoutNotify(editorSettings.ShowNotesOnGizmo);
            showNotesInSceneGizmoToggle.RegisterValueChangedCallback(e =>
            {
                editorSettings.ShowNotesOnGizmo = e.newValue;
                editorSettings.Save();
                SceneView.RepaintAll();
            });

            Label notesCountLabel = inspectorSettingsFoldout.Q<Label>("NotesCount");
            notesCountLabel.text = editorSettings.NoteCount().ToString();

            Label notesInSceneCount = inspectorSettingsFoldout.Q<Label>("NotesInSceneCount");
            if (thisIsAnAsset)
            {
                notesInSceneCount.style.display = DisplayStyle.None;
            }
            if (sceneNotesManager == null)
            {
                GetNotesManager();
            }

            if (sceneNotesManager != null)
            {
                notesInSceneCount.style.display = DisplayStyle.Flex;
                notesInSceneCount.text = sceneNotesManager.notes.Count().ToString();
            }

            Button debugLogButton = inspectorSettingsFoldout.Q<Button>("DebugLogButton");
            debugLogButton.clicked += () =>
            {
                editorSettings.DebugLogAllNotes();
            };

            Button deleteAllNoteButton = inspectorSettingsFoldout.Q<Button>("DeleteAllNotesButton");
            deleteAllNoteButton.clicked += () =>
            {
                if (EditorUtility.DisplayDialog("Note permanent deletion", "This will permanently delete all notes. Are you sure?", "Yes", "No"))
                {
                    editorSettings.DeleteAllNotes();
                    notesCountLabel.text = editorSettings.NoteCount().ToString();
                }
            };
            Button cleanupNotesButton = inspectorSettingsFoldout.Q<Button>("CleanupNotesButton");
            cleanupNotesButton.clicked += () =>
            {
                int option = EditorUtility.DisplayDialogComplex("Clean-up Notes",
            "This will attempt to remove all unused notes. This isn't always accurate and you can't undo it. Are you sure you want to proceed?",
            "Yes",
            "Yes, but debug.log the removed notes",
            "Cancel");

                switch (option)
                {
                    // Yes.
                    case 0:
                        editorSettings.CleanupNotes();
                        break;

                    // Yes with log.
                    case 1:
                        editorSettings.CleanupNotes(true);
                        break;

                    // Cancel.
                    case 2:
                        Debug.Log("Note cleanup canceled.");
                        break;

                    default:
                        Debug.LogError("Unrecognized option.");
                        break;
                }
            };

            //UpdateInLineSizeView();
            //UpdateSizeLabelSettings(inspectorSettingsFoldout);
            UpdateDoubleSidedLabelSettings(inspectorSettingsFoldout);

            Toggle performanceLoggingToggle = inspectorSettingsFoldout.Q<Toggle>("PerformanceLoggingToggle");
            Toggle detailedPerformanceLoggingToggle = inspectorSettingsFoldout.Q<Toggle>("DetailedPerformanceLoggingToggle");

            performanceLoggingToggle.value = editorSettings.logPerformance;
            performanceLoggingToggle.RegisterValueChangedCallback(e =>
            {
                editorSettings.logPerformance = e.newValue;
                editorSettings.Save();

                if (editorSettings.logPerformance)
                    detailedPerformanceLoggingToggle.style.display = DisplayStyle.Flex;
                else
                    detailedPerformanceLoggingToggle.style.display = DisplayStyle.None;

                UpdatePerformanceLoggingGroupBox();
            });

            if (editorSettings.logPerformance)
                detailedPerformanceLoggingToggle.style.display = DisplayStyle.Flex;
            else
                detailedPerformanceLoggingToggle.style.display = DisplayStyle.None;
            detailedPerformanceLoggingToggle.value = editorSettings.logDetailedPerformance;
            detailedPerformanceLoggingToggle.RegisterValueChangedCallback(e =>
            {
                editorSettings.logDetailedPerformance = e.newValue;
                editorSettings.Save();
                UpdatePerformanceLoggingGroupBox();
            });

            Button resetInspectorSettingsToMinimal = root.Q<Button>("ResetInspectorSettingsToMinimal");
            resetInspectorSettingsToMinimal.clicked += () =>
            {
                editorSettings.ResetToMinimal();
                Reset();
            };
            Button resetSettingsButton = root.Q<Button>("ResetButton");
            //There is no need to call the methods to update since the fields call them on value changes automatically.
            resetSettingsButton.clicked += () =>
            {
                editorSettings.ResetToDefault();
                Reset();
            };

            void Reset()
            {
                inspectorSettingsFoldout.Q<Toggle>("DefaultUnityInspectorToggle").SetValueWithoutNotify(editorSettings.LoadDefaultInspector);

                inspectorColorField.value = editorSettings.InspectorColor;
                overrideInspectorColor.value = editorSettings.OverrideInspectorColor;
                foldoutColorField.value = editorSettings.FoldoutColor;
                overrideFoldoutColorToggle.value = editorSettings.OverrideFoldoutColor;

                copyPasteButtonsToggle.value = editorSettings.ShowCopyPasteButtons;
                fieldRoundingField.value = editorSettings.FieldRoundingAmount;

                sizeFoldoutToggle.value = editorSettings.ShowSizeFoldout;

                gizmoLabel.value = editorSettings.ShowSizeGizmoLabel;

                sizeGizmoLabelSizeField.value = editorSettings.SizeGizmoLabelSize;
                gizmoColorField.value = editorSettings.SizeGizmoColor;
                sizeGizmoLabelBothSideToggle.value = editorSettings.ShowSizeGizmoLabelOnBothSide;
                minimumSizeForDoubleLabel.value = editorSettings.MinimumSizeForDoubleSidedLabel;
                gizmoMaximumDecimalPoints.value = editorSettings.GizmoMaximumDecimalPoints;
                labelHandlesToggle.value = editorSettings.ShowSizeGizmosLabelHandle;
                parentChildTransformsToggle.value = editorSettings.ShowParentChildTransform;
                maxChildInspector.value = editorSettings.MaxChildInspector;
                maxChildCountForSizeCalculation.value = editorSettings.MaxChildCountForSizeCalculation;

                prefabNotesToggle.value = editorSettings.ShowNotes;

                showSiblingIndexToggle.value = editorSettings.showSiblingIndex;
                ShowAssetGUID.value = editorSettings.showAssetGUID;

                performanceLoggingToggle.value = editorSettings.logPerformance;

                SceneView.RepaintAll();
            }
        }

        private void UpdateNotesToggle(Toggle prefabNotesToggle)
        {
            prefabNotesToggle.value = editorSettings.ShowNotes;
            prefabNotesToggle.RegisterValueChangedCallback(ev =>
            {
                editorSettings.ShowNotes = ev.newValue;
                //if (!ev.newValue)
                //{
                //    noteToolbarButton.style.display = DisplayStyle.None;
                //    noteEditGroupBox.style.display = DisplayStyle.None;
                //}
                //else
                //{
                //    noteToolbarButton.style.display = DisplayStyle.Flex;
                //}
                InitialNoteSetup();
                UpdateNoteType();
            });
        }

        private void SetupInspectorColorSettings(ColorField inspectorColorField, Toggle overrideInspectorColor, ColorField foldoutColorField, Toggle overrideFoldoutColorToggle)
        {
            overrideInspectorColor.value = editorSettings.OverrideInspectorColor;
            overrideInspectorColor.RegisterValueChangedCallback(ev =>
            {
                editorSettings.OverrideInspectorColor = ev.newValue;
                if (!editorSettings.OverrideInspectorColor)
                    inspectorColorField.SetEnabled(false);
                else
                    inspectorColorField.SetEnabled(true);
                UpdateInspectorColor();
            });
            inspectorColorField.value = editorSettings.InspectorColor;
            if (!editorSettings.OverrideInspectorColor)
                inspectorColorField.SetEnabled(false);
            else
                inspectorColorField.SetEnabled(true);
            inspectorColorField.RegisterValueChangedCallback(ev =>
            {
                editorSettings.InspectorColor = ev.newValue;
                UpdateInspectorColor();
            });

            overrideFoldoutColorToggle.value = editorSettings.OverrideFoldoutColor;
            overrideFoldoutColorToggle.RegisterValueChangedCallback(ev =>
            {
                editorSettings.OverrideFoldoutColor = ev.newValue;
                if (!editorSettings.OverrideFoldoutColor)
                    foldoutColorField.SetEnabled(false);
                else
                    foldoutColorField.SetEnabled(true);
                UpdateInspectorColor();
            });

            if (!editorSettings.OverrideFoldoutColor)
                foldoutColorField.SetEnabled(false);
            else
                foldoutColorField.SetEnabled(true);

            foldoutColorField.value = editorSettings.FoldoutColor;
            foldoutColorField.RegisterValueChangedCallback(ev =>
            {
                editorSettings.FoldoutColor = ev.newValue;
                UpdateInspectorColor();
            });
            UpdateInspectorColor();
        }

        private void SetupCopyPasteButtonsToggle(Toggle copyPasteButtonsToggle)
        {
            copyPasteButtonsToggle.value = editorSettings.ShowCopyPasteButtons;
            copyPasteButtonsToggle.RegisterValueChangedCallback(ev =>
            {
                editorSettings.ShowCopyPasteButtons = ev.newValue;
                UpdateMainControls();
            });
        }

        private void SetupFieldRoundingField(IntegerField fieldRoundingField)
        {
            fieldRoundingField.value = editorSettings.FieldRoundingAmount;
            fieldRoundingField.RegisterValueChangedCallback(ev =>
            {
                editorSettings.FieldRoundingAmount = ev.newValue;
                UpdateMainControls();
            });
        }

        private void SetupSizeFoldoutToggle(GroupBox settingsFoldout, Toggle sizeFoldoutToggle)
        {
            Toggle showSizeInLineToggle = settingsFoldout.Q<Toggle>("ShowSizeInlineToggle");
            GroupBox gizmoSettingsGroupBox = settingsFoldout.Q<GroupBox>("GizmoSettingsGroupBox");
            Toggle showSizeCenterToggle = settingsFoldout.Q<Toggle>("ShowSizeCenterToggle");

            sizeFoldoutToggle.value = editorSettings.ShowSizeFoldout;
            sizeFoldoutToggle.RegisterValueChangedCallback(ev =>
            {
                SetupSize(customFoldoutSetup);
                SetupViewWidthAdaptionForSize();

                editorSettings.ShowSizeFoldout = ev.newValue;
                UpdateSizeFoldout();
                UpdateInLineSizeView();

                if (editorSettings.ShowSizeFoldout)
                    showSizeInLineToggle.SetEnabled(false);
                else
                    showSizeInLineToggle.SetEnabled(true);

                if (!editorSettings.ShowSizeInLine && !editorSettings.ShowSizeFoldout)
                {
                    gizmoSettingsGroupBox.SetEnabled(false);
                    showSizeCenterToggle.SetEnabled(false);
                    maxChildCountForSizeCalculation.SetEnabled(false);
                    gizmoColorField.SetEnabled(false);
                }
                else
                {
                    gizmoSettingsGroupBox.SetEnabled(true);
                    showSizeCenterToggle.SetEnabled(true);
                    maxChildCountForSizeCalculation.SetEnabled(true);
                }

                SceneView.RepaintAll();
            });

            if (editorSettings.ShowSizeFoldout)
            {
                showSizeInLineToggle.SetEnabled(false);
                showSizeCenterToggle.SetEnabled(true);
            }
            showSizeInLineToggle.value = editorSettings.ShowSizeInLine;
            showSizeInLineToggle.schedule.Execute(() => BindShowSizeInLineToggle(sizeFoldoutToggle, showSizeInLineToggle, gizmoSettingsGroupBox, showSizeCenterToggle));

            if (editorSettings.ShowSizeInLine)
            {
                sizeFoldoutToggle.SetEnabled(false);
                showSizeCenterToggle.SetEnabled(false);
            }

            showSizeCenterToggle.value = editorSettings.ShowSizeCenter;
            showSizeCenterToggle.schedule.Execute(() => BindSizeCenterToggle(showSizeCenterToggle));

            if (!editorSettings.ShowSizeInLine && !editorSettings.ShowSizeFoldout)
            {
                gizmoSettingsGroupBox.SetEnabled(false);
                showSizeCenterToggle.SetEnabled(false);
            }
            else
            {
                gizmoSettingsGroupBox.SetEnabled(true);

                if (editorSettings.ShowSizeInLine)
                    showSizeCenterToggle.SetEnabled(false);
                else
                    showSizeCenterToggle.SetEnabled(true);
            }
        }

        private void BindShowSizeInLineToggle(Toggle sizeFoldoutToggle, Toggle showSizeInLineToggle, GroupBox gizmoSettingsGroupBox, Toggle showSizeCenterToggle)
        {
            showSizeInLineToggle.RegisterValueChangedCallback(ev =>
            {
                SetupSize(customFoldoutSetup);
                SetupViewWidthAdaptionForSize();

                editorSettings.ShowSizeInLine = ev.newValue;
                UpdateSizeFoldout();
                UpdateInLineSizeView();

                if (editorSettings.ShowSizeInLine)
                {
                    sizeFoldoutToggle.SetEnabled(false);
                    showSizeCenterToggle.SetEnabled(false);
                }
                else
                {
                    sizeFoldoutToggle.SetEnabled(true);
                    showSizeCenterToggle.SetEnabled(true);
                }

                if (!editorSettings.ShowSizeInLine && !editorSettings.ShowSizeFoldout)
                {
                    gizmoSettingsGroupBox.SetEnabled(false);
                }
                else
                {
                    gizmoSettingsGroupBox.SetEnabled(true);
                }

                SceneView.RepaintAll();
            });
        }

        private void BindSizeCenterToggle(Toggle showSizeCenterToggle)
        {
            showSizeCenterToggle.RegisterValueChangedCallback(ev =>
            {
                editorSettings.ShowSizeCenter = showSizeCenterToggle.value;
                UpdateSizeFoldout();
            });
        }

        private void SetupParentChildTransformsToggle(Toggle parentChildTransformsToggle)
        {
            parentChildTransformsToggle.value = editorSettings.ShowParentChildTransform;
            parentChildTransformsToggle.RegisterValueChangedCallback(ev =>
            {
                editorSettings.ShowParentChildTransform = ev.newValue;
                UpdateSetupParentChildFoldouts();
                SceneView.RepaintAll();
            });
        }

        private void SetupMaxChildInspector(IntegerField maxChildInspector)
        {
            maxChildInspector.value = editorSettings.MaxChildInspector;
            maxChildInspector.RegisterValueChangedCallback(ev =>
            {
                editorSettings.MaxChildInspector = ev.newValue;
                UpdateSetupParentChildFoldouts();
                SceneView.RepaintAll();
            });
        }

        private void SetupLabelHandlesToggle(Toggle labelHandles)
        {
            labelHandles.value = editorSettings.ShowSizeGizmosLabelHandle;
            labelHandles.RegisterValueChangedCallback(ev =>
            {
                editorSettings.ShowSizeGizmosLabelHandle = ev.newValue;
                SceneView.RepaintAll();
            });
        }

        private void SetupMinimumSizeForDoubleLabel(IntegerField minimumSizeForDoubleLabel)
        {
            minimumSizeForDoubleLabel.value = editorSettings.MinimumSizeForDoubleSidedLabel;
            minimumSizeForDoubleLabel.RegisterValueChangedCallback(ev =>
            {
                editorSettings.MinimumSizeForDoubleSidedLabel = ev.newValue;
                SceneView.RepaintAll();
            });
        }

        private void SetupSizeGizmoLabelBothSideToggle(GroupBox settings, Toggle sizeGizmoLabelBothSide)
        {
            sizeGizmoLabelBothSide.value = editorSettings.ShowSizeGizmoLabelOnBothSide;
            sizeGizmoLabelBothSide.RegisterValueChangedCallback(ev =>
            {
                editorSettings.ShowSizeGizmoLabelOnBothSide = ev.newValue;
                SceneView.RepaintAll();
                UpdateDoubleSidedLabelSettings(settings);
            });
        }

        private void SetupGizmoColorField(ColorField gizmoColorField)
        {
            gizmoColorField.value = editorSettings.SizeGizmoColor;
            gizmoColorField.RegisterValueChangedCallback(ev =>
            {
                editorSettings.SizeGizmoColor = ev.newValue;
                SceneView.RepaintAll();
            });
        }

        private void SetupSizeGizmoLabelSizeField(IntegerField sizeGizmoLabelSizeField)
        {
            sizeGizmoLabelSizeField.value = editorSettings.SizeGizmoLabelSize;
            sizeGizmoLabelSizeField.RegisterValueChangedCallback(ev =>
            {
                editorSettings.SizeGizmoLabelSize = ev.newValue;
                UpdateHandleLabelStyle();
                SceneView.RepaintAll();
            });
        }

        private void SetupGizmoLabel(GroupBox settings, Toggle gizmoLabel)
        {
            gizmoLabel.value = editorSettings.ShowSizeGizmoLabel;
            gizmoLabel.RegisterValueChangedCallback(ev =>
            {
                editorSettings.ShowSizeGizmoLabel = ev.newValue;
                //UpdateSizeLabelSettings(settings);
                SceneView.RepaintAll();
            });
        }

        private void SetupInspectorColor()
        {
            if (editorSettings.OverrideFoldoutColor)
            {
                root.Q<GroupBox>("RootHolder").style.backgroundColor = editorSettings.InspectorColor;

                List<GroupBox> customFoldoutGroups = root.Query<GroupBox>(className: "custom-foldout").ToList();
                foreach (GroupBox foldout in customFoldoutGroups)
                    foldout.style.backgroundColor = editorSettings.FoldoutColor;
            }
        }

        private void UpdateInspectorColor()
        {
            List<GroupBox> customFoldoutGroups = root.Query<GroupBox>(className: "custom-foldout").ToList();
            if (editorSettings.OverrideFoldoutColor)
            {
                foreach (GroupBox foldout in customFoldoutGroups)
                    foldout.style.backgroundColor = editorSettings.FoldoutColor;
            }
            else
            {
                foreach (GroupBox foldout in customFoldoutGroups)
                    foldout.style.backgroundColor = StyleKeyword.Null;
            }

            if (editorSettings.OverrideInspectorColor)
                root.Q<GroupBox>("RootHolder").style.backgroundColor = editorSettings.InspectorColor;
            else
                root.Q<GroupBox>("RootHolder").style.backgroundColor = StyleKeyword.Null;
        }

        private void TurnOffSettings(GroupBox settings, Toggle toggle)
        {
            toggle.SetValueWithoutNotify(false);
            settings.style.display = DisplayStyle.None;
        }

        private void UpdateDoubleSidedLabelSettings(GroupBox settings)
        {
            IntegerField minimumSizeForDoubleLabel = settings.Q<IntegerField>("MinimumSizeForDoubleLabel");
            if (editorSettings.ShowSizeGizmoLabelOnBothSide)
                minimumSizeForDoubleLabel.style.display = DisplayStyle.Flex;
            else
                minimumSizeForDoubleLabel.style.display = DisplayStyle.None;
        }

        #region Footer

        private readonly string assetLink = "https://assetstore.unity.com/packages/slug/276554?aid=1011ljxWe";
        private readonly string publisherLink = "https://assetstore.unity.com/publishers/45848?aid=1011ljxWe";
        private readonly string documentationLink = "https://ferdowsur.gitbook.io/better-transform/";

        private void SetupFooter(VisualElement root)
        {
            root.Q<ToolbarButton>("AssetLink").clicked += OpenAssetLink;
            root.Q<ToolbarButton>("Documentation").clicked += OpenDocumentationLink;
            root.Q<ToolbarButton>("OtherAssetsLink").clicked += OpenPublisherLink;
        }

        private void OpenAssetLink() => Application.OpenURL(assetLink);

        private void OpenDocumentationLink() => Application.OpenURL(documentationLink);

        private void OpenPublisherLink() => Application.OpenURL(publisherLink);

        #endregion Footer

        #endregion Settings

        #region Default Editor

        /// <summary>
        /// If the UXML file is missing for any reason,
        /// Instead of showing an empty inspector,
        /// This loads the default one.
        /// This shouldn't ever happen.
        /// </summary>
        private void LoadDefaultEditor(VisualElement container)
        {
            if (originalEditor != null)
                DestroyImmediate(originalEditor);

            originalEditor = Editor.CreateEditor(targets, Type.GetType("UnityEditor.TransformInspector, UnityEditor"));
            IMGUIContainer inspectorContainer = new IMGUIContainer(OnGUICallback);
            container.Add(inspectorContainer);
        }

        private void OnGUICallback()
        {
            if (target == null)
                return;
            if (originalEditor == null)
                return;

            EditorGUI.BeginChangeCheck();
            EditorGUIUtility.labelWidth = 65;
            originalEditor.OnInspectorGUI();
            EditorGUI.EndChangeCheck();
        }

        #endregion Default Editor

        #region Math

        /// <summary>
        ///
        /// </summary>
        /// <param name="vector3">Vector3 Rounded</param>
        /// <returns></returns>
        private Vector3 RoundedVector3v2(Vector3 vector3) => new Vector3(RoundedFloatv2(vector3.x), RoundedFloatv2(vector3.y), RoundedFloatv2(vector3.z));

        private float RoundedFloatv2(float f)
        {
            if (float.IsNaN(f) || float.IsInfinity(f))
                return f;

            if (Mathf.Approximately(f, Mathf.Round(f)))
                return Mathf.Round((float)f);

            float rounded = (float)Math.Round(f, editorSettings.FieldRoundingAmount);
            return rounded;
        }

        private Vector3 RoundedVector3(Vector3 vector3) => new Vector3(RoundedFloat(vector3.x), RoundedFloat(vector3.y), RoundedFloat(vector3.z));

        private float RoundedFloat(float f)
        {
            if (float.IsNaN(f) || float.IsInfinity(f))
                return f;

            if (Mathf.Approximately(f, Mathf.Round(f)))
                return Mathf.Round((float)f);

            float rounded = (float)Math.Round(f, editorSettings.FieldRoundingAmount);
            if (Mathf.Approximately(f, rounded))
                return rounded;

            return f;
        }

        private Vector3 Divide(Vector3 first, Vector3 second) => new Vector3(NanFixed(first.x / second.x), NanFixed(first.y / second.y), NanFixed(first.z / second.z));

        private Vector3 Multiply(Vector3 first, Vector3 second) => new Vector3(NanFixed(first.x * second.x), NanFixed(first.y * second.y), NanFixed(first.z * second.z));

        private float NanFixed(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 1;

            return value;
        }

        #endregion Math

        #region Animator

        #region Variables

        //Since it is not possible to get the current animator state like is it in recording mode in the current Unity version,
        //The state is retrieved from bound fields and then applied to non bound field.
        //Example: Copy state from localPosition field to worldPositionField, where localPositionField is bound and automatically updated by Animator
        private VisualElement animator_stateIndicator_position;

        private VisualElement animator_stateIndicator_rotation;
        private VisualElement animator_stateIndicator_scale;

        //The class applied to fields to indicate to user that the animator is playing a recorded state
        private string animatedFieldClass = "animatedField";

        //The class applied to fields to indicate to user that the animator is recording
        private string recordingFieldClass = "animationRecordingField";

        //The class applied by unity to fields that are being recorded
        private string beingRecordedUnitysClass = "unity-binding--animation-recorded";

        #endregion Variables

        /// <summary>
        /// Since the custom inspector uses a lot of non bou
        /// </summary>
        private void SetupAnimatorCompability()
        {
            VerifyStateIndicatorReferences();
            SetupAnimatorState();

            root.schedule.Execute(() => UpdateAnimatorState()).Every(5000).StartingIn(10000); //1000 ms = 1 s
        }

        private void SetupAnimatorState()
        {
            if (IsNotInValidAnimationMode())
            {
                return;
            }

            UpdateAnimatorState_PositionFields();
            UpdateAnimatorState_RotationFields();
            UpdateAnimatorState_ScaleFields();
        }

        private void UpdateAnimatorState()
        {
            if (editorSettings.logPerformance && editorSettings.logDetailedPerformance)
            {
                stopwatch = new Stopwatch();
                stopwatch.Start();
            }

            if (IsNotInValidAnimationMode())
            {
                if (editorSettings.logPerformance && editorSettings.logDetailedPerformance)
                {
                    LogDelay("(Running on loop) Animator State Update", stopwatch.ElapsedMilliseconds);
                    stopwatch.Stop();
                }
                return;
            }

            UpdateAnimatorState_PositionFields();
            UpdateAnimatorState_RotationFields();
            UpdateAnimatorState_ScaleFields();

            if (editorSettings.logPerformance && editorSettings.logDetailedPerformance)
            {
                LogDelay("(Running on loop) Animator State Update", stopwatch.ElapsedMilliseconds);
                stopwatch.Stop();
            }
        }

        private bool IsNotInValidAnimationMode()
        {
            if (EditorApplication.isPlaying || !AnimationMode.InAnimationMode())
                return true;

            return false;
        }

        private void VerifyStateIndicatorReferences()
        {
            if (animator_stateIndicator_position == null)
                animator_stateIndicator_position = localPositionField.Q<FloatField>().Children().ElementAt(1);

            //This is written this way because it is a property field and binding takes time, sometimes this is called before the binding is done
            if (animator_stateIndicator_rotation == null)
            {
                var t = quaternionRotationPropertyField.Q<Toggle>();
                if (t != null)
                    animator_stateIndicator_rotation = t.Children().First();
            }

            if (animator_stateIndicator_scale == null)
                animator_stateIndicator_scale = boundLocalScaleField.Q<FloatField>().Children().ElementAt(1);
        }

        private bool addedPositionAnimatorStateIndicatorClasses = false;

        private void UpdateAnimatorState_PositionFields()
        {
            if (EditorApplication.isPlaying || !AnimationMode.InAnimationMode())
            {
                if (addedPositionAnimatorStateIndicatorClasses)
                {
                    worldPositionField.RemoveFromClassList(animatedFieldClass);
                    worldPositionField.RemoveFromClassList(recordingFieldClass);
                    addedPositionAnimatorStateIndicatorClasses = false;
                }

                return;
            }

            //if (animator_stateIndicator_position == null)
            //    animator_stateIndicator_position = localPositionField.Q<FloatField>().Children().ElementAt(1);

            bool isPositionAnimated = AnimationMode.IsPropertyAnimated(target, positionProperty);
            if (isPositionAnimated)
            {
                if (animator_stateIndicator_position != null && animator_stateIndicator_position.ClassListContains(beingRecordedUnitysClass))
                {
                    worldPositionField.RemoveFromClassList(animatedFieldClass);
                    worldPositionField.AddToClassList(recordingFieldClass);
                }
                else
                {
                    worldPositionField.AddToClassList(animatedFieldClass);
                    worldPositionField.RemoveFromClassList(recordingFieldClass);
                }
                addedPositionAnimatorStateIndicatorClasses = true;
            }
            else
            {
                if (addedPositionAnimatorStateIndicatorClasses)
                {
                    worldPositionField.RemoveFromClassList(animatedFieldClass);
                    worldPositionField.RemoveFromClassList(recordingFieldClass);
                    addedPositionAnimatorStateIndicatorClasses = false;
                }
            }
        }

        private bool addedRotationAnimatorStateIndicatorClasses = false;

        private void UpdateAnimatorState_RotationFields()
        {
            if (EditorApplication.isPlaying || !AnimationMode.InAnimationMode())
            {
                if (addedRotationAnimatorStateIndicatorClasses)
                {
                    localRotationField.RemoveFromClassList(animatedFieldClass);
                    worldRotationField.RemoveFromClassList(animatedFieldClass);

                    localRotationField.RemoveFromClassList(recordingFieldClass);
                    worldRotationField.RemoveFromClassList(recordingFieldClass);

                    addedRotationAnimatorStateIndicatorClasses = false;
                }

                return;
            }

            if (animator_stateIndicator_rotation == null)
            {
                var t = quaternionRotationPropertyField.Q<Toggle>();
                if (t != null)
                    animator_stateIndicator_rotation = t.Children().First();
            }

            bool isRotationAnimated = AnimationMode.IsPropertyAnimated(target, rotationProperty);
            if (isRotationAnimated)
            {
                if (animator_stateIndicator_rotation != null && animator_stateIndicator_rotation.ClassListContains(beingRecordedUnitysClass))
                {
                    localRotationField.RemoveFromClassList(animatedFieldClass);
                    worldRotationField.RemoveFromClassList(animatedFieldClass);

                    localRotationField.AddToClassList(recordingFieldClass);
                    worldRotationField.AddToClassList(recordingFieldClass);
                }
                else
                {
                    localRotationField.AddToClassList(animatedFieldClass);
                    worldRotationField.AddToClassList(animatedFieldClass);

                    localRotationField.RemoveFromClassList(recordingFieldClass);
                    worldRotationField.RemoveFromClassList(recordingFieldClass);
                }
                addedRotationAnimatorStateIndicatorClasses = true;
            }
            else
            {
                if (addedRotationAnimatorStateIndicatorClasses)
                {
                    localRotationField.RemoveFromClassList(animatedFieldClass);
                    worldRotationField.RemoveFromClassList(animatedFieldClass);

                    localRotationField.RemoveFromClassList(recordingFieldClass);
                    worldRotationField.RemoveFromClassList(recordingFieldClass);

                    addedRotationAnimatorStateIndicatorClasses = false;
                }
            }
        }

        private bool addedScaleAnimatorStateIndicatorClasses = false;

        private void UpdateAnimatorState_ScaleFields()
        {
            if (EditorApplication.isPlaying || !AnimationMode.InAnimationMode())
            {
                if (addedScaleAnimatorStateIndicatorClasses)
                {
                    localScaleField.RemoveFromClassList(animatedFieldClass);
                    worldScaleField.RemoveFromClassList(animatedFieldClass);

                    localScaleField.RemoveFromClassList(recordingFieldClass);
                    worldScaleField.RemoveFromClassList(recordingFieldClass);

                    addedScaleAnimatorStateIndicatorClasses = false;
                }

                return;
            }

            if (animator_stateIndicator_scale == null)
                animator_stateIndicator_scale = boundLocalScaleField.Q<FloatField>().Children().ElementAt(1);

            bool isLocalScaleAnimated = AnimationMode.IsPropertyAnimated(target, scaleProperty);

            if (isLocalScaleAnimated)
            {
                if (animator_stateIndicator_scale != null && animator_stateIndicator_scale.ClassListContains(beingRecordedUnitysClass))
                {
                    localScaleField.RemoveFromClassList(animatedFieldClass);
                    worldScaleField.RemoveFromClassList(animatedFieldClass);

                    localScaleField.AddToClassList(recordingFieldClass);
                    worldScaleField.AddToClassList(recordingFieldClass);
                }
                else
                {
                    localScaleField.AddToClassList(animatedFieldClass);
                    worldScaleField.AddToClassList(animatedFieldClass);

                    localScaleField.RemoveFromClassList(recordingFieldClass);
                    worldScaleField.RemoveFromClassList(recordingFieldClass);
                }
                addedScaleAnimatorStateIndicatorClasses = true;
            }
            else
            {
                if (addedScaleAnimatorStateIndicatorClasses)
                {
                    localScaleField.RemoveFromClassList(animatedFieldClass);
                    worldScaleField.RemoveFromClassList(animatedFieldClass);

                    localScaleField.RemoveFromClassList(recordingFieldClass);
                    worldScaleField.RemoveFromClassList(recordingFieldClass);

                    addedScaleAnimatorStateIndicatorClasses = false;
                }
            }
        }

        #endregion Animator

        #region Adapt to view width

        private Label refreshLabel;

        private Label gizmoOnLabel;
        private Label gizmoOffLabel;

        private Label hierarchyLabel;
        private Label selfSizeLabel;

        private Label rendererSizeTypeLabel;
        private Label meshSizeTypeLabel;

        private GroupBox sizeToolbar;
        private GroupBox calculationTypeGroupBox;

        private GroupBox sizeLabelGroupBox;

        /// <summary>
        /// This updates the inspector based off of outside factor from this code itself
        /// </summary>
        private void SetupViewWidthAdaption()
        {
            if (editorSettings.ShowSizeFoldout || editorSettings.ShowSizeInLine)
            {
                SetupViewWidthAdaptionForSize();
            }

            root.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (editorSettings.logPerformance && editorSettings.logDetailedPerformance)
                    Debug.Log("Inspector geometry updated.");

                float width = evt.newRect.width;
                Adapt(width);
            });
        }

        private void SetupViewWidthAdaptionForSize()
        {
            topGroupBox.schedule.Execute(() =>
            {
                SetupViewWidthAdaptionForSizeMain();
                Adapt(root.contentRect.width);
            }).ExecuteLater(0);
        }

        private void SetupViewWidthAdaptionForSizeMain()
        {
            if (targets.Length == 1)
            {
                if (editorSettings.ShowSizeFoldout || editorSettings.ShowSizeInLine)
                {
                    if (refreshSizeButton == null)
                    {
                        sizeSetupDone = false;
                        SetupSize(new CustomFoldoutSetup());
                    }

                    refreshLabel = refreshSizeButton.Q<Label>("Label");
                    gizmoOnLabel = gizmoOnButton.Q<Label>("Label");
                    gizmoOffLabel = gizmoOffButton.Q<Label>("Label");
                    hierarchyLabel = hierarchySizeButton.Q<Label>("Label");
                    selfSizeLabel = selfSizeButton.Q<Label>("Label");

                    rendererSizeTypeLabel = rendererSizeButton.Q<Label>("Label");
                    meshSizeTypeLabel = filterSizeButton.Q<Label>("Label");

                    sizeToolbar = root.Q<GroupBox>("SizeToolbar");

                    sizeLabelGroupBox = sizeFoldoutField.parent.Q<GroupBox>("SizeLabelGroupBox");

                    calculationTypeGroupBox = root.Q<GroupBox>("CalculationTypeGroupBox");
                }
            }
        }

        //todo: Null reference check for everything is a bit paranoid. Change these later. But since this will run in edge case sceneries for 1 frame, one extra if statement even if unnecessary isn't that bad.
        private void Adapt(float width)
        {
            if (targets.Length != 1)
                return;
            if (editorSettings == null)
                return;

            if (editorSettings.ShowSizeFoldout || editorSettings.ShowSizeInLine)
            {
                AdaptSizeUI(width);
            }

            if (editorSettings.showSiblingIndex && siblingIndexLabel != null && transform.parent)
                siblingIndexLabel.style.display = width > 225 ? DisplayStyle.Flex : DisplayStyle.None;

            if (toolbarGroupBox != null)
                toolbarGroupBox.style.display = width > 245 ? DisplayStyle.Flex : DisplayStyle.None;

            AdaptMainInformationLabels(width);
        }

        private void AdaptMainInformationLabels(float width)
        {
            if (positionLabel == null || rotationLabel == null || scaleLabelGroupbox == null || sizeLabelGroupBox == null || localSpaceLabel == null || worldSpaceLabel == null)
                return;

            localSpaceLabel.style.display = width > 180 ? DisplayStyle.Flex : DisplayStyle.None;
            worldSpaceLabel.style.display = width > 180 ? DisplayStyle.Flex : DisplayStyle.None;
            positionLabel.style.display = width > 180 ? DisplayStyle.Flex : DisplayStyle.None;
            rotationLabel.style.display = width > 180 ? DisplayStyle.Flex : DisplayStyle.None;
            scaleLabelGroupbox.style.display = width > 180 ? DisplayStyle.Flex : DisplayStyle.None;
            sizeLabelGroupBox.style.display = width > 180 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void AdaptSizeUI(float width)
        {
            if (gizmoOnLabel == null) SetupViewWidthAdaptionForSize();

            //Hide the Refresh Button if too small
            if (refreshSizeButton != null)
                refreshSizeButton.style.display = width > 200 ? DisplayStyle.Flex : DisplayStyle.None;

            //Hide Calculation Type Button if too small
            if (calculationTypeGroupBox != null)
                calculationTypeGroupBox.style.display = width > 190 ? DisplayStyle.Flex : DisplayStyle.None;

            if (refreshLabel != null)
                refreshLabel.style.display = width > 420 ? DisplayStyle.Flex : DisplayStyle.None;

            if (editorSettings.ShowSizeFoldout)
            {
                if (gizmoOnLabel != null)
                    gizmoOnLabel.style.display = width > 385 ? DisplayStyle.Flex : DisplayStyle.None;
                if (gizmoOffLabel != null)
                    gizmoOffLabel.style.display = width > 385 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else
            {
                if (gizmoOnLabel != null)
                    gizmoOnLabel.style.display = width > 350 ? DisplayStyle.Flex : DisplayStyle.None;
                if (gizmoOffLabel != null)
                    gizmoOffLabel.style.display = width > 350 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (editorSettings.ShowSizeFoldout)
            {
                if (rendererSizeTypeLabel != null)
                    rendererSizeTypeLabel.style.display = width > 355 ? DisplayStyle.Flex : DisplayStyle.None;
                if (meshSizeTypeLabel != null)
                    meshSizeTypeLabel.style.display = width > 355 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else
            {
                if (rendererSizeTypeLabel != null)
                    rendererSizeTypeLabel.style.display = width > 315 ? DisplayStyle.Flex : DisplayStyle.None;
                if (meshSizeTypeLabel != null)
                    meshSizeTypeLabel.style.display = width > 315 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (hierarchyLabel != null)
                hierarchyLabel.style.display = width > 310 ? DisplayStyle.Flex : DisplayStyle.None;
            if (selfSizeLabel != null)
                selfSizeLabel.style.display = width > 310 ? DisplayStyle.Flex : DisplayStyle.None;

            if (sizeToolbar != null)
                sizeToolbar.style.display = width > 245 ? DisplayStyle.Flex : DisplayStyle.None;

            if (unitDropDownField != null)
            {
                if (editorSettings != null && editorSettings.ShowSizeInLine)
                {
                    if (width > 295)
                        unitDropDownField.RemoveFromClassList("unity-popup-field-shortened");
                    else
                        unitDropDownField.AddToClassList("unity-popup-field-shortened");
                }
                else
                {
                    if (width > 235)
                        unitDropDownField.RemoveFromClassList("unity-popup-field-shortened");
                    else
                        unitDropDownField.AddToClassList("unity-popup-field-shortened");
                }
            }

            //Convert Button types. The smaller ones have very little gap between them
            if (width > 275)
            {
                ConvertToNormalComboButton(refreshSizeButton);
                ConvertToNormalComboButton(gizmoOnButton);
                ConvertToNormalComboButton(gizmoOffButton);
                ConvertToNormalComboButton(rendererSizeButton);
                ConvertToNormalComboButton(filterSizeButton);
                ConvertToNormalComboButton(hierarchySizeButton);
                ConvertToNormalComboButton(selfSizeButton);
            }
            else
            {
                ConvertToShortComboButton(refreshSizeButton);
                ConvertToShortComboButton(gizmoOffButton);
                ConvertToShortComboButton(gizmoOnButton);
                ConvertToShortComboButton(rendererSizeButton);
                ConvertToShortComboButton(filterSizeButton);
                ConvertToShortComboButton(hierarchySizeButton);
                ConvertToShortComboButton(selfSizeButton);
            }
        }

        private void ConvertToNormalComboButton(VisualElement element)
        {
            if (element == null) return;

            element.AddToClassList("toolbarComboButton-normal");
            element.RemoveFromClassList("toolbarComboButton-shortened");
        }

        private void ConvertToShortComboButton(VisualElement element)
        {
            if (element == null) return;

            element.RemoveFromClassList("toolbarComboButton-normal");
            element.AddToClassList("toolbarComboButton-shortened");
        }

        #endregion Adapt to view width

        #region Gizmo

        private GUIStyle handleLabelStyle;

        private void UpdateHandleLabelStyle()
        {
            handleLabelStyle = new GUIStyle(EditorStyles.boldLabel);
            handleLabelStyle.fontStyle = FontStyle.BoldAndItalic;
            handleLabelStyle.alignment = TextAnchor.MiddleCenter;
            handleLabelStyle.fontSize = editorSettings.SizeGizmoLabelSize;
            handleLabelStyle.normal.background = Texture2D.whiteTexture;
        }

        private Color redHandleLabel = new Color(1, 0, 0, 1f);
        private Color greenHandleLabel = new Color(0, 0.4f, 0, 1f);
        private Color blueHandleLabel = new Color(0, 0, 1, 1f);

        private readonly float handlesTransparency = 0.1f;
        private readonly float handleSize = 0.15f;

        private void OnSceneGUI()
        {
            if (transform == null) return;

            if (Selection.objects.Length > 1) return;

            if (editorSettings == null) editorSettings = BetterTransformSettings.instance;
            if (editorSettings == null) return;

            if (!editorSettings.ShowSizeFoldout && !editorSettings.ShowSizeInLine)
                return;

            if (!editorSettings.ShowSizeGizmo)
            {
                if (editorSettings.ShowNotesOnGizmo && showThisNoteInSceneView)
                {
                    if (string.IsNullOrWhiteSpace(myNote) || myNote == noNoteString)
                        return;

                    if (handleLabelStyle == null)
                        UpdateHandleLabelStyle();

                    handleLabelStyle.normal.textColor = new Color(0, 0, 0, 0.75f);
                    Handles.Label(transform.position + new Vector3(0, -0.5f, 0), myNote, handleLabelStyle);
                }

                return;
            }

            if (handleLabelStyle == null)
                UpdateHandleLabelStyle();

            //Get proper bounds
            Bounds gizmoBounds = currentBound;
            gizmoBounds.center = Divide(gizmoBounds.center, transform.lossyScale);
            gizmoBounds.size = Divide(gizmoBounds.size, transform.lossyScale);
            //Get transform matrix : position rotation and scale
            if (editorSettings.CurrentWorkSpace == BetterTransformSettings.WorkSpace.World)
                Handles.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, transform.lossyScale);
            else
                Handles.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale); //New
                                                                                                              //Handles.matrix = transform.localToWorldMatrix; //Old

            Handles.color = editorSettings.SizeGizmoColor;

            Handles.DrawWireCube(gizmoBounds.center, gizmoBounds.size);

            //Set matrix to ignore scale so that handles don't become skewed
            //Disabled due to bug: This causes position to be wrong with global scale.
            //Handles.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

            DrawGizmoLabelAndHandle(gizmoBounds);

            if (editorSettings.ShowNotesOnGizmo && showThisNoteInSceneView)
            {
                if (string.IsNullOrWhiteSpace(myNote) || myNote == noNoteString)
                    return;

                handleLabelStyle.normal.textColor = new Color(0, 0, 0, 0.75f);
                Handles.Label(gizmoBounds.center + new Vector3(0, gizmoBounds.extents.y + 0.15f, 0), myNote, handleLabelStyle);
            }

        }

        private readonly float positionOffset = 0;

        private void DrawGizmoLabelAndHandle(Bounds gizmoBounds)
        {
            float multiplier = ScalesManager.instance.CurrentUnitValue();
            //float multiplier = ScalesFinder.CurrentUnitValue(editorSettings.SelectedUnit);
            int selectedUnit = ScalesManager.instance.selectedUnit;
            string[] availableUnits = ScalesManager.instance.GetAvailableUnits();
            string unit;
            if (availableUnits.Length > selectedUnit)
                unit = availableUnits[selectedUnit];
            else
                return;

            var settings = BetterTransformSettings.instance;
            int gizmoMaximumDecimalPoints = settings ? settings.GizmoMaximumDecimalPoints : 4;

            if (gizmoBounds.extents.x != 0)
            {
                handleLabelStyle.normal.textColor = redHandleLabel;
                Handles.color = new Color(1, 0, 0, handlesTransparency);

                GUIContent label;
                float size = currentBound.size.x * multiplier;
                if (size > 0 && size < 0.000001f)
                    label = new GUIContent("X: " + " Almost 0 " + unit);
                else
                    label = new GUIContent("X: " + (float)Math.Round(size, gizmoMaximumDecimalPoints) + " " + unit);

                if (editorSettings.ShowSizeGizmoLabel)
                {
                    Handles.Label(gizmoBounds.center + new Vector3(0, positionOffset, gizmoBounds.extents.z), label, handleLabelStyle);

                    if (editorSettings.ShowSizeGizmoLabelOnBothSide && WideEnoughForDoubleLabel(currentBound.size.z))
                    {
                        Handles.Label(gizmoBounds.center + new Vector3(0, positionOffset, -gizmoBounds.extents.z), label, handleLabelStyle);

                        if (editorSettings.ShowSizeGizmosLabelHandle)
                        {
                            Handles.ArrowHandleCap(2, gizmoBounds.center + new Vector3(0f, 0, -gizmoBounds.extents.z), Quaternion.Euler(0, 90, 0), handleSize, EventType.Repaint);
                            Handles.ArrowHandleCap(3, gizmoBounds.center + new Vector3(0f, 0, -gizmoBounds.extents.z), Quaternion.Euler(0, -90, 0), handleSize, EventType.Repaint);
                        }
                    }
                }

                if (editorSettings.ShowSizeGizmosLabelHandle)
                {
                    Handles.ArrowHandleCap(0, gizmoBounds.center + new Vector3(0f, 0, gizmoBounds.extents.z), Quaternion.Euler(0, 90, 0), handleSize, EventType.Repaint);
                    Handles.ArrowHandleCap(1, gizmoBounds.center + new Vector3(0f, 0, gizmoBounds.extents.z), Quaternion.Euler(0, -90, 0), handleSize, EventType.Repaint);
                }
            }

            if (gizmoBounds.extents.y != 0)
            {
                handleLabelStyle.normal.textColor = greenHandleLabel;
                Handles.color = new Color(0, 1, 0, handlesTransparency);

                GUIContent label;
                float size = currentBound.size.y * multiplier;
                if (size > 0 && size < 0.000001f)
                    label = new GUIContent("Y: " + " Almost 0 " + unit);
                else
                    label = new GUIContent("Y: " + (float)Math.Round(size, gizmoMaximumDecimalPoints) + " " + unit);

                if (editorSettings.ShowSizeGizmoLabel)
                    Handles.Label(gizmoBounds.center + new Vector3(0, gizmoBounds.extents.y, 0), label, handleLabelStyle);

                if (editorSettings.ShowSizeGizmosLabelHandle)
                {
                    Handles.ArrowHandleCap(4, gizmoBounds.center + new Vector3(0, gizmoBounds.extents.y - 0.25f, 0), Quaternion.Euler(90, 0, 0), handleSize, EventType.Repaint);
                    Handles.ArrowHandleCap(5, gizmoBounds.center + new Vector3(0, gizmoBounds.extents.y - 0.25f, 0), Quaternion.Euler(-90, 0, 0), handleSize, EventType.Repaint);
                }

                if (editorSettings.ShowSizeGizmoLabel && editorSettings.ShowSizeGizmoLabelOnBothSide && WideEnoughForDoubleLabel(currentBound.size.y))
                {
                    Handles.Label(gizmoBounds.center + new Vector3(0, -gizmoBounds.extents.y, 0), label, handleLabelStyle);
                    if (editorSettings.ShowSizeGizmosLabelHandle)
                    {
                        Handles.ArrowHandleCap(4, gizmoBounds.center + new Vector3(-0, -gizmoBounds.extents.y + 0.25f, 0), Quaternion.Euler(90, 0, 0), handleSize, EventType.Repaint);
                        Handles.ArrowHandleCap(5, gizmoBounds.center + new Vector3(0, -gizmoBounds.extents.y + 0.25f, 0), Quaternion.Euler(-90, 0, 0), handleSize, EventType.Repaint);
                    }
                }
            }

            if (gizmoBounds.extents.z != 0)
            {
                handleLabelStyle.normal.textColor = blueHandleLabel;
                Handles.color = new Color(0, 0, 1, handlesTransparency);

                GUIContent label;
                float size = currentBound.size.z * multiplier;
                if (size > 0 && size < 0.000001f)
                    label = new GUIContent("Z: " + " Almost 0 " + unit);
                else
                    label = new GUIContent("Z: " + (float)Math.Round(size, gizmoMaximumDecimalPoints) + " " + unit);

                if (editorSettings.ShowSizeGizmoLabel)
                    Handles.Label(gizmoBounds.center + new Vector3(gizmoBounds.extents.x, positionOffset, 0), label, handleLabelStyle);

                if (editorSettings.ShowSizeGizmosLabelHandle)
                {
                    Handles.ArrowHandleCap(4, gizmoBounds.center + new Vector3(gizmoBounds.extents.x, 0, 0), Quaternion.Euler(0, 0, 90), handleSize, EventType.Repaint);
                    Handles.ArrowHandleCap(5, gizmoBounds.center + new Vector3(gizmoBounds.extents.x, 0, 0), Quaternion.Euler(180, 0, 0), handleSize, EventType.Repaint);
                }

                if (editorSettings.ShowSizeGizmoLabel && editorSettings.ShowSizeGizmoLabelOnBothSide && WideEnoughForDoubleLabel(currentBound.size.x))
                {
                    Handles.Label(gizmoBounds.center + new Vector3(-gizmoBounds.extents.x, positionOffset, 0), label, handleLabelStyle);
                    if (editorSettings.ShowSizeGizmosLabelHandle)
                    {
                        Handles.ArrowHandleCap(4, gizmoBounds.center + new Vector3(-gizmoBounds.extents.x, 0, 0), Quaternion.Euler(0, 0, 90), handleSize, EventType.Repaint);
                        Handles.ArrowHandleCap(5, gizmoBounds.center + new Vector3(-gizmoBounds.extents.x, 0, 0), Quaternion.Euler(180, 0, 0), handleSize, EventType.Repaint);
                    }
                }
            }
        }

        private bool WideEnoughForDoubleLabel(float width)
        {
            if (width >= editorSettings.MinimumSizeForDoubleSidedLabel)
                return true;

            return false;
        }

        #endregion Gizmo
    }
}