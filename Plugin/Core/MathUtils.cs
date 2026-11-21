using UnityEngine;

namespace MapsExtended.Core
{
    public static class MathUtils
    {
        public enum RotationAxis
        {
            X,
            Y,
            Z
        }

        public static Vector2 GetRotatedVector2(Vector2 vector, float degreeRotation)
        {
            var radians = degreeRotation * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2(
                vector.x * cos - vector.y * sin,
                vector.x * sin + vector.y * cos);
        }

        public static Vector2 GetRotatedRectangle(Vector2 rectangle, float degreeRotation)
        {
            var radians = degreeRotation * Mathf.Deg2Rad;
            var absCos = Mathf.Abs(Mathf.Cos(radians));
            var absSin = Mathf.Abs(Mathf.Sin(radians));
            return new Vector2(
                rectangle.x * absCos + rectangle.y * absSin,
                rectangle.x * absSin + rectangle.y * absCos);
        }

        public static Vector2 GetMidpoint(Vector2 minBound, Vector2 maxBound)
        {
            return (minBound + maxBound) * 0.5f;
        }

        public static Vector3 ConvertToMapPosition(Vector3 unityPosition)
        {
            return new Vector3(unityPosition.x, unityPosition.z, unityPosition.y);
        }

        public static Vector3 ConvertToMapPosition(Transform transform)
        {
            if (transform == null)
            {
                return Vector3.zero;
            }

            return ConvertToMapPosition(transform.position);
        }

        public static float ConvertToMapRotation(Transform transform, RotationAxis axis = RotationAxis.Y)
        {
            if (transform == null)
            {
                return 0f;
            }

            var euler = transform.rotation.eulerAngles;
            switch (axis)
            {
                case RotationAxis.X:
                    return -euler.x;
                case RotationAxis.Z:
                    return -euler.z;
                default:
                    return -euler.y;
            }
        }

        public static bool ApproxEquals(float first, float second)
        {
            return Mathf.Abs(first - second) < float.Epsilon;
        }
    }
}
