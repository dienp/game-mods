using UnityEngine;

namespace SteadyThirdPerson;

internal static class CameraFraming
{
    internal const float DefaultHeight = 0.8f;
    internal const int DefaultShoulderDistance = 65;

    internal static Vector3 CenterOffset(Vector3 offset) =>
        new(0f, offset.y + DefaultHeight, offset.z);

    // An already-running Debug build can still reload this three-argument entry point.
    internal static (Vector3 Socket, float ArmScale) CloseShoulder(Vector3 socket, float weight, float shoulderOffset) =>
        PlaceShoulder(socket, weight, shoulderOffset, DefaultShoulderDistance);

    internal static (Vector3 Socket, float ArmScale) PlaceShoulder(Vector3 socket, float weight,
        float shoulderOffset, int distancePercent)
    {
        socket.x += weight * shoulderOffset;
        return (socket, Mathf.Lerp(1f, distancePercent / 100f, weight));
    }

    internal static Ray RebaseAimRay(Ray ray, Transform source, Transform shoulder)
    {
        var rotation = shoulder.rotation * Quaternion.Inverse(source.rotation);
        return new Ray(shoulder.position + rotation * (ray.origin - source.position), rotation * ray.direction);
    }

    internal static (Quaternion Rotation, float Fov) Evaluate(Quaternion rotation, float nativeFov,
        float referenceFov, float horizontalFov, float aspect, float rollReduction)
    {
        float HalfAngle(float degrees) => Mathf.Tan(degrees * Mathf.Deg2Rad * 0.5f);

        // Unity stores vertical FOV. Keep the native zoom ratio while setting the horizontal baseline.
        float zoom = HalfAngle(nativeFov) / HalfAngle(referenceFov);
        float fov = 2f * Mathf.Atan(zoom * HalfAngle(horizontalFov) / Mathf.Max(aspect, 0.1f)) * Mathf.Rad2Deg;

        var forward = rotation * Vector3.forward;
        if (rollReduction > 0f && Mathf.Abs(forward.y) < 0.999f)
            rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(forward, Vector3.up), rollReduction);

        return (rotation, Mathf.Clamp(fov, 1f, 179f));
    }
}
