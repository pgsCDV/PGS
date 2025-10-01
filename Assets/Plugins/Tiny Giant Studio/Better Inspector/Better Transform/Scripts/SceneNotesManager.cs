// Ignore Spelling: Deserialize

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TinyGiantStudio.BetterInspector
{
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    public class SceneNotesManager : MonoBehaviour
    {
        public static SceneNotesManager Instance;

        public Dictionary<Transform, Note> notes = new Dictionary<Transform, Note>();

        [SerializeField] private List<Transform> keys = new List<Transform>();
        [SerializeField] private List<Note> values = new List<Note>();

        [ExecuteInEditMode]
        void Awake()
        {
            Instance = this;

            gameObject.tag = "EditorOnly";

            PrepareTheDictionary();
        }

        private void OnEnable()
        {
            Instance = this;

            bool hideInEditor = true;

            if (hideInEditor)
            {
                gameObject.hideFlags = HideFlags.HideInHierarchy;
                EditorApplication.RepaintHierarchyWindow();
            }
            else
                gameObject.hideFlags = HideFlags.None;

            if (notes.Count == 0 && keys.Count != 0)
                PrepareTheDictionary();
        }

        void OnDisable()
        {
            Instance = null;
        }
        void OnDestroy()
        {
            Instance = null;
        }


        public Note MyNote(Transform target)
        {
            notes.TryGetValue(target, out var note);
            return note;
        }

        public string GetNote(Transform target)
        {
            notes.TryGetValue(target, out var note);

            if (note == null) return null;

            return note.note;
        }
        public NoteType GetNoteType(Transform target)
        {
            notes.TryGetValue(target, out var note);

            if (note == null) return NoteType.tooltip;

            return note.noteType;
        }
        public Color GetNoteColor(Transform target)
        {
            notes.TryGetValue(target, out var note);

            if (note == null) return new Color(0.4f, 0.4f, 0.5f);

            return note.color;
        }

        public void SetNote(Transform target, string note, NoteType noteType, Color noteColor, bool showThisNoteInSceneView)
        {
            if (notes.ContainsKey(target))
            {
                if (string.IsNullOrEmpty(note))
                {
                    notes.Remove(target);
                }
                else
                {
                    notes[target].note = note;
                    notes[target].noteType = noteType;
                    notes[target].color = noteColor;
                    notes[target].showInSceneView = showThisNoteInSceneView;
                }
            }
            else
                notes.Add(target, new Note(note, noteType, noteColor, showThisNoteInSceneView));

            UpdatePresistentValues();
            EditorUtility.SetDirty(this);
        }

        public void DeleteNote(Transform target)
        {
            if (notes.ContainsKey(target))
            {
                notes.Remove(target);

                UpdatePresistentValues();
                EditorUtility.SetDirty(this);
            }
        }

        void UpdatePresistentValues()
        {
            keys.Clear();
            values.Clear();
            foreach (var pair in notes)
            {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }
        public void PrepareTheDictionary()
        {
            notes.Clear();

            if (keys.Count != values.Count)
                return;
            //throw new Exception("There are different numbers of keys and values to deserialize!");

            for (int i = 0; i < values.Count; i++)
            {
                if (!notes.ContainsKey(keys[i]))
                    notes.Add(keys[i], values[i]);
            }
        }
    }

    [System.Serializable]
    public class Note
    {
        //Serializefield should be unnecessary here but weirdly doesn't work in some cases. Need to recheck later
        [SerializeField] public string id; //used by prefabs to identify who this belongs to
        [SerializeField] public string note;
        [SerializeField] public NoteType noteType;
        [SerializeField] public Color color = new Color(0.4f, 0.4f, 0.5f);
        [SerializeField] public bool showInSceneView = true;


        public Note(string newNote, NoteType newNoteType, Color noteColor, bool showInSceneView)
        {
            this.note = newNote;
            this.noteType = newNoteType;
            this.color = noteColor;
            this.showInSceneView = showInSceneView;
        }

        public Note(string id, string newNote, NoteType newNoteType, Color noteColor, bool showInSceneView)
        {
            this.id = id;
            this.note = newNote;
            this.noteType = newNoteType;
            this.color = noteColor;
            this.showInSceneView = showInSceneView;
        }
    }

    [System.Serializable]
    public enum NoteType
    {
        tooltip,
        fullWidthBottom,
        fullWidthTop
    }
}
#endif