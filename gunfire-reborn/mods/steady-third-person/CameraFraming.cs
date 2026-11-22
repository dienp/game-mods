using UnityEngine;

namespace SteadyThirdPerson;

internal static class CameraFraming
{
    internal const float DefaultHeight = 0.8f;
    internal const int DefaultShoulderDistance = 65;
    private const float AimPitchLimit = 89f;

    internal static (Vector3 Socket, float ArmScale) PlaceShoulder(Vector3 nativeOffset, float shoulderOffset,
        int distancePercent) =>
        (new Vector3(shoulderOffset, nativeOffset.y + DefaultHeight, nativeOffset.z), distancePercent / 100f);

    internal static Ray RebaseAimRay(Ray ray, Transform source, Transform shoulder)
    {
        var rotation = shoulder.rotation * Quaternion.Inverse(source.rotation);
        return new Ray(shoulder.position + rotation * (ray.origin - source.position), rotation * ray.direction);
    }

    internal static (float Up, float Down) ExtendPitchLimits(float up, float down, float upWithRecoil)
    {
        // The native X clamp negates the upward bound after adding the saved recoil.
        float recoilOffset = upWithRecoil - up;
        return (Mathf.Max(up, AimPitchLimit - recoilOffset), Mathf.Max(down, AimPitchLimit));
    }

    internal static (Quaternion Rotation, float Fov) Evaluate(Quaternion rotation, float nativeFov,
        float referenceFov, float horizontalFov, float aspect, float rollReduction)
    {
        float HalfAngle(float degrees) => Mathf.Tan(degrees * Mathf.Deg2Rad * 0.5f);

        // Unity stores vertical FOV. Keep the native zoom ratio while setting the horizontal baseline.
        float zoom = HalfAngle(nativeFov) / HalfAngle(referenceFov);
        float fov = 2f * Mathf.Atan(zoom * HalfAngle(horizontalFov) / Mathf.Max(aspect, 0.1f)) * Mathf.Rad2Deg;

        var forward = rotation * Vector3.forward;
        if (rollReduction > 0f && Mathf.Abs(forward.y) < 0.9999f)
            rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(forward, Vector3.up), rollReduction);

        return (rotation, Mathf.Clamp(fov, 1f, 179f));
    }
}
