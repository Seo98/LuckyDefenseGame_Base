using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지정된 사각 배치 영역 안에서 영웅의 자유 배치와 최소 간격을 관리합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class HeroPlacementBoard : MonoBehaviour
{
    [SerializeField] private BoxCollider placementArea;
    [Tooltip("실제로 보이는 바닥의 Collider입니다. 비워두면 배치 영역 Collider의 윗면을 사용합니다.")]
    [SerializeField] private Collider placementSurface;

    [SerializeField, Min(0f)]
    private float placementClearance = 0.1f;

    [SerializeField, Min(0f)]
    private float edgePadding = 0.5f;

    [SerializeField, Min(1)]
    private int automaticPlacementAttempts = 128;

    private readonly Dictionary<HeroActor, Vector3> placedPositions = new();
    private readonly HashSet<HeroActor> externalOccupants = new();

    /// <summary>
    /// 현재 영역에 배치된 영웅 수입니다.
    /// </summary>
    public int OccupiedCount => placedPositions.Count;

    /// <summary>
    /// 배치 영역 또는 영웅 위치가 변경될 때 발생합니다.
    /// </summary>
    public event Action PlacementChanged;

    private void Awake()
    {
        placementArea ??= GetComponent<BoxCollider>();
    }

    /// <summary>
    /// 배치 영역 안의 임의 후보를 탐색해 영웅을 자동 배치합니다.
    /// 기존 소환 서비스와의 호환을 위해 성공한 순번을 함께 반환합니다.
    /// </summary>
    public bool TryPlaceInFirstEmpty(HeroActor hero, out int placementIndex)
    {
        if (!TryFindAutomaticPosition(hero, out Vector3 position) ||
            !TryPlaceAt(hero, position))
        {
            placementIndex = -1;
            return false;
        }

        placementIndex = OccupiedCount - 1;
        return true;
    }

    /// <summary>
    /// 지정한 월드 위치에서 영웅 Collider가 영역 내부이고 다른 영웅 Collider와
    /// 설정된 여백을 확보하는지 검사합니다.
    /// Y 좌표가 배치 평면에 맞춰진 최종 위치도 반환합니다.
    /// </summary>
    public bool CanPlaceAt(
        HeroActor hero,
        Vector3 worldPosition,
        out Vector3 resolvedPosition)
    {
        Vector3 surfacePosition = ResolveToPlacementPlane(worldPosition);
        resolvedPosition = surfacePosition;

        if (hero == null || !hero.IsSpawned ||
            !HeroPlacementUtility.TryResolveOnSurface(hero, surfacePosition, Quaternion.identity,
                out resolvedPosition, out Bounds candidateBounds) ||
            !IsInsidePlacementArea(candidateBounds))
        {
            return false;
        }

        foreach (HeroActor placedHero in placedPositions.Keys)
        {
            if (OverlapsHero(candidateBounds, hero, placedHero))
            {
                return false;
            }
        }

        foreach (HeroActor externalHero in externalOccupants)
        {
            if (OverlapsHero(candidateBounds, hero, externalHero))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 유효한 월드 위치에 영웅을 자유 배치합니다.
    /// </summary>
    public bool TryPlaceAt(HeroActor hero, Vector3 worldPosition)
    {
        if (hero == null || (placedPositions.ContainsKey(hero) && !hero.IsDragging) ||
            !CanPlaceAt(hero, worldPosition, out Vector3 resolvedPosition))
        {
            return false;
        }

        placedPositions[hero] = resolvedPosition;
        externalOccupants.Remove(hero);
        HeroPlacementUtility.ApplyPose(hero, transform, resolvedPosition, Quaternion.identity);
        hero.CompletePlacement(true);
        PlacementChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 영웅의 배치 상태를 해제하고 기존 월드 위치를 반환합니다.
    /// </summary>
    public bool Release(HeroActor hero, out Vector3 previousPosition)
    {
        if (ReferenceEquals(hero, null) || !placedPositions.Remove(hero, out previousPosition))
        {
            previousPosition = default;
            return false;
        }

        if (hero != null && !hero.IsBeingDestroyed)
        {
            hero.transform.SetParent(null, true);
            hero.CompletePlacement(false);
        }
        PlacementChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 배치된 영웅을 이동 상태로 전환하고 드래그 실패 시 복구할 위치를 반환합니다.
    /// </summary>
    public bool TryBeginMove(HeroActor hero, out Vector3 previousPosition)
    {
        if (hero == null ||
            !placedPositions.TryGetValue(hero, out previousPosition) ||
            !hero.BeginDrag())
        {
            previousPosition = default;
            return false;
        }

        // 이동 중에도 원래 위치를 예약하여 취소 시 반드시 복귀할 수 있게 합니다.
        hero.transform.SetParent(null, true);
        PlacementChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 지정한 영웅이 현재 이 Board에 배치되어 있는지 확인합니다.
    /// </summary>
    public bool IsPlaced(HeroActor hero)
    {
        return !ReferenceEquals(hero, null) && placedPositions.ContainsKey(hero);
    }

    /// <summary>드래그 후보를 확정하지 않고 예약된 원래 필드 위치로 복귀합니다.</summary>
    public bool TryRestore(HeroActor hero)
    {
        if (hero == null || !hero.IsSpawned || !placedPositions.TryGetValue(hero, out Vector3 position))
            return false;
        HeroPlacementUtility.ApplyPose(hero, transform, position, Quaternion.identity);
        hero.CompletePlacement(true);
        PlacementChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 소환 대기열처럼 전장 밖에서 관리되는 영웅을 Collider 겹침 검사 대상으로 등록합니다.
    /// </summary>
    public void RegisterExternalOccupant(HeroActor hero)
    {
        if (hero != null)
        {
            externalOccupants.Add(hero);
        }
    }

    /// <summary>
    /// 외부 영웅이 드래그를 시작하거나 제거될 때 겹침 검사 대상에서 해제합니다.
    /// </summary>
    public void UnregisterExternalOccupant(HeroActor hero)
    {
        if (!ReferenceEquals(hero, null))
        {
            externalOccupants.Remove(hero);
        }
    }

    private bool TryFindAutomaticPosition(HeroActor hero, out Vector3 position)
    {
        if (placementArea == null)
        {
            position = default;
            return false;
        }

        Vector3 center = placementArea.center;
        Vector3 halfSize = placementArea.size * 0.5f;
        float minX = center.x - halfSize.x + edgePadding;
        float maxX = center.x + halfSize.x - edgePadding;
        float minZ = center.z - halfSize.z + edgePadding;
        float maxZ = center.z + halfSize.z - edgePadding;

        if (minX > maxX || minZ > maxZ)
        {
            position = default;
            return false;
        }

        for (int i = 0; i < automaticPlacementAttempts; i++)
        {
            Vector3 localCandidate = new(
                UnityEngine.Random.Range(minX, maxX),
                center.y,
                UnityEngine.Random.Range(minZ, maxZ));
            Vector3 worldCandidate = placementArea.transform.TransformPoint(localCandidate);

            if (CanPlaceAt(hero, worldCandidate, out position))
            {
                return true;
            }
        }

        position = default;
        return false;
    }

    private Vector3 ResolveToPlacementPlane(Vector3 worldPosition)
    {
        if (HeroPlacementUtility.TryGetWorldBounds(placementSurface, out Bounds surfaceBounds))
        {
            worldPosition.y = surfaceBounds.max.y;
            return worldPosition;
        }
        if (placementArea == null)
        {
            return worldPosition;
        }

        Vector3 localPosition = placementArea.transform.InverseTransformPoint(worldPosition);
        localPosition.y = placementArea.center.y + placementArea.size.y * 0.5f;
        return placementArea.transform.TransformPoint(localPosition);
    }

    private bool OverlapsHero(
        Bounds candidateBounds,
        HeroActor candidateHero,
        HeroActor otherHero)
    {
        if (otherHero == null || otherHero == candidateHero || !otherHero.IsSpawned)
        {
            return false;
        }

        Vector3 otherPosition = otherHero.transform.position;
        Quaternion otherRotation = otherHero.transform.rotation;
        if (otherHero.IsDragging && placedPositions.TryGetValue(otherHero, out Vector3 reservedPosition))
        {
            otherPosition = reservedPosition;
            otherRotation = Quaternion.identity;
        }
        if (!HeroPlacementUtility.TryGetBoundsAt(otherHero, otherPosition, otherRotation, out Bounds otherBounds))
            return false;
        return candidateBounds.min.x < otherBounds.max.x + placementClearance &&
               candidateBounds.max.x > otherBounds.min.x - placementClearance &&
               candidateBounds.min.z < otherBounds.max.z + placementClearance &&
               candidateBounds.max.z > otherBounds.min.z - placementClearance;
    }

    private bool IsInsidePlacementArea(Bounds candidateBounds)
    {
        if (placementArea == null)
        {
            return false;
        }

        Vector3 center = placementArea.center;
        Vector3 halfSize = placementArea.size * 0.5f;
        Vector3 localMin = placementArea.transform.InverseTransformPoint(
            new Vector3(candidateBounds.min.x, candidateBounds.center.y, candidateBounds.min.z));
        Vector3 localMax = placementArea.transform.InverseTransformPoint(
            new Vector3(candidateBounds.max.x, candidateBounds.center.y, candidateBounds.max.z));

        return localMin.x >= center.x - halfSize.x + edgePadding &&
               localMax.x <= center.x + halfSize.x - edgePadding &&
               localMin.z >= center.z - halfSize.z + edgePadding &&
               localMax.z <= center.z + halfSize.z - edgePadding;
    }

    private void OnValidate()
    {
        placementArea ??= GetComponent<BoxCollider>();
        placementClearance = Mathf.Max(0f, placementClearance);
        edgePadding = Mathf.Max(0f, edgePadding);
        automaticPlacementAttempts = Mathf.Max(1, automaticPlacementAttempts);

        if (placementArea != null)
        {
            placementArea.isTrigger = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (placementArea == null)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.35f);
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = placementArea.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(placementArea.center, placementArea.size);
        Gizmos.matrix = previousMatrix;
    }
}
