//This script exists to not cause issue with users who are updating from older version. This will be removed later. //Time: 16 June 2025

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TinyGiantStudio.BetterInspector
{
    /// <summary>
    /// This is responsible for finding the correct ScriptableObject
    /// </summary>
    public static class ScalesFinder
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns>The scales ScriptableObject file. This can be null</returns>
        public static ScalesManager MyScales()
        {
            return ScalesManager.instance;
        }

    }
}
#endif