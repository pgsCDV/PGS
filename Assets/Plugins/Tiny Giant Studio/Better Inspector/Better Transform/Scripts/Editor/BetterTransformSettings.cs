using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TinyGiantStudio.BetterInspector
{
    [FilePath("ProjectSettings/BetterTransformSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class BetterTransformSettings : ScriptableSingleton<BetterTransformSettings>
    {
        #region Inspector Customization

        [SerializeField] private bool _overrideInspectorColor = false;

        public bool OverrideInspectorColor
        {
            get { return _overrideInspectorColor; }
            set
            {
                _overrideInspectorColor = value;
                Save(true);
            }
        }

        [SerializeField] private Color _inspectorColor = new Color(0, 0, 1, 0.025f);

        public Color InspectorColor
        {
            get { return _inspectorColor; }
            set
            {
                _inspectorColor = value;
                Save(true);
            }
        }

        [SerializeField] private bool _overrideFoldoutColor = false;

        public bool OverrideFoldoutColor
        {
            get { return _overrideFoldoutColor; }
            set
            {
                _overrideFoldoutColor = value;
                Save(true);
            }
        }

        [SerializeField] private Color _foldoutColor = new Color(0, 1, 0, 0.025f);

        public Color FoldoutColor
        {
            get { return _foldoutColor; }
            set
            {
                _foldoutColor = value;
                Save(true);
            }
        }

        #endregion Inspector Customization

        [SerializeField] private WorkSpace _currentWorkSpace = WorkSpace.Local;

        public WorkSpace CurrentWorkSpace
        {
            get { return _currentWorkSpace; }
            set
            {
                _currentWorkSpace = value;
                Save(true);
            }
        }

        [SerializeField] private bool _showCopyPasteButtons = true;

        public bool ShowCopyPasteButtons
        {
            get { return _showCopyPasteButtons; }
            set
            {
                _showCopyPasteButtons = value;
                Save(true);
            }
        }

        [SerializeField] private bool _showAllVariableCopyPasteButtons = false;

        public bool ShowAllVariableCopyPasteButtons
        {
            get { return _showAllVariableCopyPasteButtons; }
            set
            {
                _showAllVariableCopyPasteButtons = value;
                Save(true);
            }
        }

        [SerializeField] private int _fieldRoundingAmount = 5;

        public int FieldRoundingAmount
        {
            get { return _fieldRoundingAmount; }
            set
            {
                _fieldRoundingAmount = value;
                Save(true);
            }
        }

        public bool roundPositionField = false;
        public bool roundRotationField = false;
        public bool roundScaleField = false;

        public bool animatedFoldout = true;

        public bool pingSelfButton = false;

        //[SerializeField] private bool _lockScaleAspectRatio = false;

        //public bool LockScaleAspectRatio
        //{
        //    get { return _lockScaleAspectRatio; }
        //    set
        //    {
        //        _lockScaleAspectRatio = value;
        //        Save(true);
        //    }
        //}

        #region Size

        [SerializeField] private bool _showSizeInLine = false;

        public bool ShowSizeInLine
        {
            get { return _showSizeInLine; }
            set
            {
                _showSizeInLine = value;
                Save(true);
            }
        }

        [SerializeField] private bool _showSizeFoldout = true;

        public bool ShowSizeFoldout
        {
            get { return _showSizeFoldout; }
            set
            {
                _showSizeFoldout = value;
                Save(true);
            }
        }

        [SerializeField] private bool _showSizeCenter = true;

        public bool ShowSizeCenter
        {
            get { return _showSizeCenter; }
            set
            {
                _showSizeCenter = value;
                Save(true);
            }
        }

        [SerializeField] private bool _includeChildBounds = true;

        public bool IncludeChildBounds
        {
            get { return _includeChildBounds; }
            set
            {
                _includeChildBounds = value;
                Save(true);
            }
        }

        public bool ignoreParticleAndVFXInSizeCalculation = false;

        [SerializeField] private SizeType _currentSizeType = SizeType.Renderer;

        public SizeType CurrentSizeType
        {
            get { return _currentSizeType; }
            set
            {
                _currentSizeType = value;
                Save(true);
            }
        }

        [SerializeField] private bool _lockSizeAspectRatio = false;

        public bool LockSizeAspectRatio
        {
            get { return _lockSizeAspectRatio; }
            set
            {
                _lockSizeAspectRatio = value;
                Save(true);
            }
        }

        [SerializeField] private int _maxChildCountForSizeCalculation = 30;

        public int MaxChildCountForSizeCalculation
        {
            get { return _maxChildCountForSizeCalculation; }
            set
            {
                _maxChildCountForSizeCalculation = value;
                Save(true);
            }
        }

        [SerializeField] private int _maxChildInspectors = 10;

        public int MaxChildInspector
        {
            get { return _maxChildInspectors; }
            set
            {
                _maxChildInspectors = value;
                Save(true);
            }
        }

        [SerializeField] private bool _constantSizeUpdate = false;

        public bool ConstantSizeUpdate
        {
            get { return _constantSizeUpdate; }
            set
            {
                _constantSizeUpdate = value;
                Save(true);
            }
        }

        #region Gizmos

        [SerializeField] private bool _showSizeGizmo = true;

        public bool ShowSizeGizmo
        {
            get { return _showSizeGizmo; }
            set
            {
                _showSizeGizmo = value;
                Save(true);
            }
        }

        [SerializeField] private bool _showSizeGizmoLabel = true;

        public bool ShowSizeGizmoLabel
        {
            get { return _showSizeGizmoLabel; }
            set
            {
                _showSizeGizmoLabel = value;
                Save(true);
            }
        }

        [SerializeField] private int _sizeGizmoLabelSize = 10;

        public int SizeGizmoLabelSize
        {
            get { return _sizeGizmoLabelSize; }
            set
            {
                _sizeGizmoLabelSize = value;
                Save(true);
            }
        }

        [SerializeField] private Color _sizeGizmoColor = new Color(1, 1, 1, 0.15f);

        public Color SizeGizmoColor
        {
            get { return _sizeGizmoColor; }
            set
            {
                _sizeGizmoColor = value;
                Save(true);
            }
        }

        [SerializeField] private bool _showSizeGizmoLabelOnBothSide = true;

        public bool ShowSizeGizmoLabelOnBothSide
        {
            get { return _showSizeGizmoLabelOnBothSide; }
            set
            {
                _showSizeGizmoLabelOnBothSide = value;
                Save(true);
            }
        }

        [SerializeField] private bool _showSizeGizmosLabelHandle = false;

        public bool ShowSizeGizmosLabelHandle
        {
            get { return _showSizeGizmosLabelHandle; }
            set
            {
                _showSizeGizmosLabelHandle = value;
                Save(true);
            }
        }

        [SerializeField] private int _minimumSizeForDoubleSidedLabel = 10;

        public int MinimumSizeForDoubleSidedLabel
        {
            get { return _minimumSizeForDoubleSidedLabel; }
            set
            {
                _minimumSizeForDoubleSidedLabel = value;
                Save(true);
            }
        }

        [SerializeField] private int _gizmoMaximumDecimalPoints = 4;

        public int GizmoMaximumDecimalPoints
        {
            get { return _gizmoMaximumDecimalPoints; }
            set
            {
                _gizmoMaximumDecimalPoints = value;
                Save(true);
            }
        }

        #endregion Gizmos

        #endregion Size

        #region Notes

        [SerializeField] private bool _showNotes = true;

        public bool ShowNotes
        {
            get { return _showNotes; }
            set
            {
                _showNotes = value;
                Save(true);
            }
        }

        [SerializeField] private bool _showNotesOnGizmo = true;

        public bool ShowNotesOnGizmo
        {
            get { return _showNotesOnGizmo; }
            set
            {
                _showNotesOnGizmo = value;
                Save(true);
            }
        }
        [SerializeField] private List<Note> notes = new List<Note>();

        public string GetNote(string id)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                if (notes[i].id == id)
                    return notes[i].note;
            }

            return string.Empty;
        }

        public void SetNote(string id, string note, NoteType noteType, Color color, bool showThisNoteInSceneView)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                if (notes[i].id == id)
                {
                    notes[i].note = note;
                    notes[i].noteType = noteType;
                    notes[i].color = color;
                    notes[i].showInSceneView = showThisNoteInSceneView;
                    Save(true);

                    return;
                }
            }

            notes.Add(new Note(id, note, noteType, color, showThisNoteInSceneView));
            Save(true);
        }

        public void DeleteNote(string id)
        {
            Note noteToDelete = null;
            for (int i = 0; i < notes.Count; i++)
            {
                if (notes[i].id == id)
                {
                    noteToDelete = notes[i];
                }
            }

            if (noteToDelete != null)
            {
                notes.Remove(noteToDelete);
                Save(true);
            }
        }

        public int NoteCount() => notes.Count;

        public void DebugLogAllNotes()
        {
            if (notes.Count == 0)
                Debug.Log("No notes are found");

            for (int i = 0; i < notes.Count; i++)
                Debug.Log(notes[i].note + ", by GUID: " + notes[i].id);
        }

        public void DeleteAllNotes()
        {
            Undo.RecordObject(this, "Delete notes");
            notes.Clear();
        }

        public void CleanupNotes(bool debugLog = false)
        {
            List<Note> notesToRemove = new List<Note>();
            for (int i = 0; i < notes.Count; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(notes[i].id);
                if (string.IsNullOrEmpty(path))
                {
                    if (debugLog)
                        Debug.Log(notes[i].note + ", by GUID: " + notes[i].note);
                    notesToRemove.Add(notes[i]);
                }
            }

            if (notesToRemove.Count == 0)
            {
                Debug.Log("All notes have found corresponding assets. No cleanup required.");
                return;
            }

            for (int i = 0; i < notesToRemove.Count; i++)
            {
                if (notes.Contains(notesToRemove[i])) //It should never be false. Never got error. Added because of paranoia
                    notes.Remove(notesToRemove[i]);
            }
        }

        #endregion Notes

        #region Parent Child Transform

        [SerializeField] private bool _showParentChildTransform = true;

        public bool ShowParentChildTransform
        {
            get { return _showParentChildTransform; }
            set
            {
                _showParentChildTransform = value;
                Save(true);
            }
        }

        #endregion Parent Child Transform

        public bool showSiblingIndex = false;
        public bool showAssetGUID = false;

        public bool showWhySizeIsHiddenLabel = true;

        [SerializeField] private bool _loadDefaultInspector = true;

        public bool LoadDefaultInspector
        {
            get { return _loadDefaultInspector; }
            set
            {
                _loadDefaultInspector = value;
                Save(true);
            }
        }

        public bool logPerformance = false;
        public bool logDetailedPerformance = true;

        public void ResetToMinimal()
        {
            _showSizeInLine = true;
            _showSizeFoldout = false;
            _showParentChildTransform = false;

            Reset();
        }

        public void ResetToDefault()
        {
            _showSizeInLine = false;
            _showSizeFoldout = true;
            _showParentChildTransform = true;

            Reset();
        }

        public void Reset()
        {
            _loadDefaultInspector = true;

            _overrideInspectorColor = false;
            _inspectorColor = new Color(0, 0, 1, 0.025f);
            _overrideFoldoutColor = false;
            _foldoutColor = new Color(0, 1, 0, 0.025f);

            _currentWorkSpace = WorkSpace.Local;
            _showCopyPasteButtons = true;
            _showAllVariableCopyPasteButtons = false;

            _fieldRoundingAmount = 5;
            //_lockScaleAspectRatio = false;

            _maxChildInspectors = 10;
            _includeChildBounds = true;
            _maxChildCountForSizeCalculation = 50;
            _currentSizeType = SizeType.Renderer;
            _lockSizeAspectRatio = false;

            _showSizeGizmo = true;
            _showSizeGizmoLabel = true;
            _sizeGizmoLabelSize = 10;
            _sizeGizmoColor = new Color(1, 1, 1, 0.15f); ;
            _showSizeGizmoLabelOnBothSide = true;
            _showSizeGizmosLabelHandle = false;
            _minimumSizeForDoubleSidedLabel = 10;
            _gizmoMaximumDecimalPoints = 4;
            _constantSizeUpdate = false;

            _showNotes = true;

            showSiblingIndex = false;
            showAssetGUID = false;

            logPerformance = false;
            logDetailedPerformance = false;

            Save(true);
        }

        public void Save() => Save(true);

        [Serializable]
        public enum WorkSpace
        {
            Local,
            World,
            Both
        }

        public enum SizeType
        {
            Renderer,
            Filter
        }
    }
}