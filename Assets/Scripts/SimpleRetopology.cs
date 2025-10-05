using UnityEngine;
using System.Collections.Generic;

public static class SimpleRetopology {
    public static void RunRetopology(GameObject go) {
        MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) {
            Debug.LogError("MeshFilter or mesh not found");
            return;
        }

        Mesh mesh = mf.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        Dictionary<Vector3, int> vertexMap = new Dictionary<Vector3, int>();
        List<Vector3> newVertices = new List<Vector3>();
        List<int> newTriangles = new List<int>();

        for (int i = 0; i < triangles.Length; i++) {
            Vector3 v = vertices[triangles[i]];

            if (!vertexMap.TryGetValue(v, out int index)) {
                index = newVertices.Count;
                newVertices.Add(v);
                vertexMap.Add(v, index);
            }

            newTriangles.Add(index);
        }

        Mesh newMesh = new Mesh();
        newMesh.vertices = newVertices.ToArray();
        newMesh.triangles = newTriangles.ToArray();
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();

        mf.sharedMesh = newMesh;

        MeshCollider mc = go.GetComponentInChildren<MeshCollider>();
        if (mc != null)
            mc.sharedMesh = newMesh;

        //Debug.Log($"Retopology complete. Vertices reduced from {vertices.Length} to {newVertices.Count}");
    }
}
