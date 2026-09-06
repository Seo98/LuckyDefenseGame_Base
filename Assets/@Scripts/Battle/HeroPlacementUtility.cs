using UnityEngine;

/// <summary>
/// 벤치와 필드가 공유하는 영웅 바닥 정렬 및 위치 적용입니다.
/// 물리 캐시 대신 Collider 형상과 현재 Transform으로 계산하여 같은 프레임의 이동도 반영합니다.
/// </summary>
public static class HeroPlacementUtility
{
    /// <summary>영웅 Collider의 최하단을 표면 Y에 맞춘 루트 위치와 월드 Bounds를 계산합니다.</summary>
    public static bool TryResolveOnSurface(HeroActor hero, Vector3 surfacePosition,
        Quaternion rotation, out Vector3 position, out Bounds bounds)
    {
        position = surfacePosition;
        if (!TryGetBoundsAt(hero, position, rotation, out bounds)) return false;
        float lift = surfacePosition.y - bounds.min.y;
        position.y += lift;
        bounds.center += Vector3.up * lift;
        return true;
    }

    /// <summary>기물을 움직이지 않고 지정한 위치/회전의 Collider Bounds를 계산합니다.</summary>
    public static bool TryGetBoundsAt(HeroActor hero, Vector3 position, Quaternion rotation, out Bounds bounds)
    {
        bounds = default;
        if (hero == null) return false;
        Collider collider = hero.GetComponentInChildren<Collider>();
        if (collider == null) return false;
        // 루트의 월드 스케일과 자식 Collider 오프셋은 유지하고 위치/회전만 교체합니다.
        Matrix4x4 delta = Matrix4x4.TRS(position, rotation, Vector3.one) *
                          Matrix4x4.TRS(hero.transform.position, hero.transform.rotation, Vector3.one).inverse;
        return TryGetColliderBounds(collider, delta * collider.transform.localToWorldMatrix, out bounds);
    }

    /// <summary>지원하는 Collider의 현재 월드 Bounds를 물리 동기화 없이 계산합니다.</summary>
    public static bool TryGetWorldBounds(Collider collider, out Bounds bounds)
    {
        bounds = default;
        return collider != null && TryGetColliderBounds(collider, collider.transform.localToWorldMatrix, out bounds);
    }

    /// <summary>계산 또는 저장한 위치를 적용합니다. 점유 등록과 공격 가능 상태는 호출자가 관리합니다.</summary>
    public static void ApplyPose(HeroActor hero, Transform parent, Vector3 position, Quaternion rotation)
    {
        hero.transform.SetParent(parent, true);
        hero.transform.SetPositionAndRotation(position, rotation);
    }

    private static bool TryGetColliderBounds(Collider collider, Matrix4x4 matrix, out Bounds bounds)
    {
        Vector3 scale = new(matrix.MultiplyVector(Vector3.right).magnitude,
            matrix.MultiplyVector(Vector3.up).magnitude, matrix.MultiplyVector(Vector3.forward).magnitude);
        if (collider is SphereCollider sphere)
        {
            float radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            bounds = new Bounds(matrix.MultiplyPoint3x4(sphere.center), Vector3.one * (2f * radius));
            return true;
        }
        if (collider is CapsuleCollider capsule)
        {
            int axis = capsule.direction;
            Vector3 direction = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
            float radius = capsule.radius * Mathf.Max(scale[(axis + 1) % 3], scale[(axis + 2) % 3]);
            float halfSegment = Mathf.Max(0f, capsule.height * scale[axis] * 0.5f - radius);
            Vector3 extent = Abs(matrix.MultiplyVector(direction).normalized) * halfSegment + Vector3.one * radius;
            bounds = new Bounds(matrix.MultiplyPoint3x4(capsule.center), extent * 2f);
            return true;
        }

        Bounds localBounds;
        if (collider is BoxCollider box) localBounds = new Bounds(box.center, box.size);
        else if (collider is MeshCollider mesh && mesh.sharedMesh != null) localBounds = mesh.sharedMesh.bounds;
        else { bounds = default; return false; }

        // Box는 정확한 AABB, Mesh는 로컬 Bounds를 감싸는 보수적인 AABB입니다.
        Vector3 extents = localBounds.extents;
        Vector3 worldExtents = Abs(matrix.MultiplyVector(Vector3.right)) * extents.x +
                               Abs(matrix.MultiplyVector(Vector3.up)) * extents.y +
                               Abs(matrix.MultiplyVector(Vector3.forward)) * extents.z;
        bounds = new Bounds(matrix.MultiplyPoint3x4(localBounds.center), worldExtents * 2f);
        return true;
    }

    private static Vector3 Abs(Vector3 value) => new(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
}
