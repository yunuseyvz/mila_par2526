using UnityEngine;
using System.Collections.Generic;

namespace LanguageTutor.Learning
{
    /// <summary>
    /// Simple object highlighter that spawns a cube at object positions.
    /// </summary>
    public class ObjectHighlighter : MonoBehaviour
    {
        [Header("Highlight Settings")]
        [SerializeField] private Color highlightColor = Color.yellow;
        [SerializeField] private float highlightDuration = 3f;
        [SerializeField] private float cubeScale = 0.2f;

        private Dictionary<GameObject, float> _activeHighlights = new Dictionary<GameObject, float>();

        private void Update()
        {
            var expiredHighlights = new List<GameObject>();

            foreach (var kvp in _activeHighlights)
            {
                if (kvp.Key == null)
                {
                    expiredHighlights.Add(kvp.Key);
                    continue;
                }

                float remainingTime = kvp.Value - Time.deltaTime;
                _activeHighlights[kvp.Key] = remainingTime;
                
                if (remainingTime <= 0)
                {
                    expiredHighlights.Add(kvp.Key);
                }
            }

            foreach (var highlight in expiredHighlights)
            {
                RemoveHighlight(highlight);
            }
        }

        /// <summary>
        /// Highlight an object at a specific world position.
        /// </summary>
        public void HighlightObject(Vector3 position, string objectLabel)
        {
            GameObject cube = new GameObject("HighlightCube");
            cube.transform.position = position;
            cube.transform.localScale = Vector3.one * cubeScale;

            MeshRenderer renderer = cube.AddComponent<MeshRenderer>();
            MeshFilter filter = cube.AddComponent<MeshFilter>();
            filter.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            Material mat = new Material(Shader.Find("Standard"));
            mat.color = highlightColor;
            renderer.material = mat;

            _activeHighlights[cube] = highlightDuration;
            Debug.Log($"[ObjectHighlighter] Highlighted '{objectLabel}' at {position.ToString("F2")}");
        }

        /// <summary>
        /// Remove a specific highlight.
        /// </summary>
        private void RemoveHighlight(GameObject highlightObject)
        {
            if (highlightObject != null)
            {
                _activeHighlights.Remove(highlightObject);
                Destroy(highlightObject);
            }
        }

        /// <summary>
        /// Clear all active highlights.
        /// </summary>
        public void ClearAllHighlights()
        {
            foreach (var kvp in new Dictionary<GameObject, float>(_activeHighlights))
            {
                RemoveHighlight(kvp.Key);
            }
        }
    }
}
