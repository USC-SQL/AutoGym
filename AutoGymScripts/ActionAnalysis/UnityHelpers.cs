using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace UnityActionAnalysis
{
    public static class UnityHelpers
    {
        private const float AABB_SCALE = 0.1f;

        // Approximates the clickable bounds of a game object with a collider
        public static bool ComputeObjectMouseBounds(GameObject gameObject, out Vector2 pixelMin, out Vector2 pixelMax)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                pixelMin = new Vector2();
                pixelMax = new Vector2();
                return false;
            }

            Bounds bounds;
            if (gameObject.TryGetComponent(out Collider collider))
            {
                bounds = collider.bounds;
            } else if (gameObject.TryGetComponent(out Collider2D collider2d))
            {
                bounds = collider2d.bounds;
            } else
            {
                pixelMin = new Vector2();
                pixelMax = new Vector2();
                return false;
            }

            Vector3 scaledMin = bounds.center - bounds.extents * AABB_SCALE;
            Vector3 scaledMax = bounds.center + bounds.extents * AABB_SCALE;

            List<Vector3> aabbPoints = new List<Vector3> {
                new Vector3(scaledMin.x, scaledMin.y, scaledMin.z),
                new Vector3(scaledMax.x, scaledMin.y, scaledMin.z),
                new Vector3(scaledMin.x, scaledMax.y, scaledMin.z),
                new Vector3(scaledMax.x, scaledMax.y, scaledMin.z),
                new Vector3(scaledMin.x, scaledMin.y, scaledMax.z),
                new Vector3(scaledMax.x, scaledMin.y, scaledMax.z),
                new Vector3(scaledMin.x, scaledMax.y, scaledMax.z),
                new Vector3(scaledMax.x, scaledMax.y, scaledMax.z)
            };

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;

            foreach (Vector3 pt in aabbPoints)
            {
                Vector3 screenPt = cam.WorldToScreenPoint(pt);
                screenPt.x = screenPt.x / cam.pixelWidth * Screen.width;
                screenPt.y = screenPt.y / cam.pixelHeight * Screen.height;
                minX = Math.Min(minX, screenPt.x);
                maxX = Math.Max(maxX, screenPt.x);
                minY = Math.Min(minY, screenPt.y);
                maxY = Math.Max(maxY, screenPt.y);
            }

            pixelMin = new Vector2(minX, minY);
            pixelMax = new Vector2(maxX, maxY);
            return true;
        }
    }
}
