using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 하나의 Spawn 영역을 여러 벤치 칸으로 나눠 소환된 영웅을 모두 표시합니다.
/// 대기 중인 영웅은 비배치 상태이며 필드에 드롭된 이후에만 공격할 수 있습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class HeroSummonStagingArea : MonoBehaviour
{
    [SerializeField] private HeroPlacementBoard placementBoard;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Collider spawnSurface;
    [SerializeField, Min(1)] private int maxQueueCount = 8;

    private readonly Dictionary<HeroActor, int> slotByHero = new(8);
    private readonly Dictionary<HeroActor, Pose> reservedPoses = new(8);
    private HeroActor[] occupants = Array.Empty<HeroActor>();

    /// <summary>
    /// 소환 벤치의 최대 기물 수입니다.
    /// </summary>
    public int Capacity => maxQueueCount;

    /// <summary>
    /// 드래그 중 예약된 칸을 포함한 벤치 기물 수입니다.
    /// </summary>
    public int Count => slotByHero.Count;

    /// <summary>
    /// 현재 빈 벤치 칸이 있는지 나타냅니다.
    /// </summary>
    public bool HasSpace => Count < Capacity;

    /// <summary>
    /// 벤치 점유 상태가 변경될 때 발생합니다.
    /// </summary>
    public event Action StagingChanged;

    private void Awake()
    {
        EnsureSlots();
    }

    /// <summary>
    /// 왼쪽부터 첫 번째 빈 칸에 소환 영웅을 표시합니다.
    /// </summary>
    public bool TryStage(HeroActor hero)
    {
        EnsureSlots();

        if (hero == null ||
            !hero.IsSpawned ||
            !HasSpace ||
            slotByHero.ContainsKey(hero) ||
            spawnPoint == null)
        {
            return false;
        }

        for (int i = 0; i < occupants.Length; i++)
        {
            if (occupants[i] == null && TryPlaceInSlot(hero, i))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 벤치에 있는 영웅의 드래그를 시작하고 원래 칸 번호를 반환합니다.
    /// </summary>
    public bool TryBeginMove(HeroActor hero, out int slotIndex)
    {
        if (hero == null ||
            !slotByHero.TryGetValue(hero, out slotIndex) ||
            !hero.BeginDrag())
        {
            slotIndex = -1;
            return false;
        }

        // 소환이 원래 칸을 차지하지 못하도록 점유 등록은 유지합니다.
        reservedPoses[hero] = new Pose(hero.transform.position, hero.transform.rotation);
        placementBoard?.UnregisterExternalOccupant(hero);
        hero.transform.SetParent(null, true);
        StagingChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 드롭 실패 시 영웅을 원래 벤치 칸에 복구합니다.
    /// </summary>
    public bool TryRestore(HeroActor hero, int slotIndex)
    {
        if (hero == null || !hero.IsSpawned ||
            !slotByHero.TryGetValue(hero, out int reservedSlot) || reservedSlot != slotIndex ||
            !reservedPoses.Remove(hero, out Pose pose)) return false;
        HeroPlacementUtility.ApplyPose(hero, transform, pose.position, pose.rotation);
        hero.CompletePlacement(false);
        placementBoard?.RegisterExternalOccupant(hero);
        StagingChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 벤치에서 영웅을 제거합니다. 소환 실패 롤백과 풀 반환에 사용합니다.
    /// </summary>
    public bool Release(HeroActor hero)
    {
        if (ReferenceEquals(hero, null) || !slotByHero.Remove(hero, out int slotIndex))
        {
            return false;
        }

        occupants[slotIndex] = null;
        reservedPoses.Remove(hero);
        placementBoard?.UnregisterExternalOccupant(hero);
        if (hero != null && !hero.IsBeingDestroyed && !hero.IsPlaced) hero.transform.SetParent(null, true);
        StagingChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 지정한 영웅이 현재 소환 벤치에 있는지 확인합니다.
    /// </summary>
    public bool Contains(HeroActor hero)
    {
        return !ReferenceEquals(hero, null) && slotByHero.ContainsKey(hero);
    }

    /// <summary>합성 결과를 대표 재료의 원래 벤치 칸에 놓습니다.</summary>
    public bool TryStageAt(HeroActor hero, int slotIndex) => TryPlaceInSlot(hero, slotIndex);

    /// <summary>영웅이 차지한 벤치 칸 번호를 조회합니다.</summary>
    public bool TryGetSlot(HeroActor hero, out int slotIndex) => slotByHero.TryGetValue(hero, out slotIndex);

    private bool TryPlaceInSlot(HeroActor hero, int slotIndex)
    {
        EnsureSlots();

        if (hero == null ||
            !hero.IsSpawned ||
            (uint)slotIndex >= (uint)occupants.Length ||
            (!ReferenceEquals(occupants[slotIndex], null) && occupants[slotIndex] != hero) ||
            (slotByHero.TryGetValue(hero, out int existingSlot) && existingSlot != slotIndex) ||
            spawnPoint == null)
        {
            return false;
        }

        if (!HeroPlacementUtility.TryResolveOnSurface(hero, GetSlotSurfacePosition(slotIndex),
                spawnPoint.rotation, out Vector3 position, out _)) return false;
        occupants[slotIndex] = hero;
        slotByHero[hero] = slotIndex;
        hero.gameObject.SetActive(true);
        HeroPlacementUtility.ApplyPose(hero, transform, position, spawnPoint.rotation);
        hero.CompletePlacement(false);
        placementBoard?.RegisterExternalOccupant(hero);
        StagingChanged?.Invoke();
        return true;
    }

    private Vector3 GetSlotSurfacePosition(int slotIndex)
    {
        float normalizedPosition = (slotIndex + 0.5f) / Capacity;

        if (spawnSurface is BoxCollider boxCollider)
        {
            Vector3 localPosition = boxCollider.center;
            localPosition.x += (normalizedPosition - 0.5f) * boxCollider.size.x;
            localPosition.y += boxCollider.size.y * 0.5f;
            return boxCollider.transform.TransformPoint(localPosition);
        }

        if (HeroPlacementUtility.TryGetWorldBounds(spawnSurface, out Bounds bounds))
        {
            return new Vector3(
                Mathf.Lerp(bounds.min.x, bounds.max.x, normalizedPosition),
                bounds.max.y,
                bounds.center.z);
        }

        float centeredSlot = slotIndex - (Capacity - 1) * 0.5f;
        return spawnPoint.position + spawnPoint.right * centeredSlot;
    }

    private void EnsureSlots()
    {
        if (occupants.Length != Capacity)
        {
            occupants = new HeroActor[Capacity];
            slotByHero.Clear();
            reservedPoses.Clear();
        }
    }

    private void OnValidate()
    {
        maxQueueCount = Mathf.Max(1, maxQueueCount);
    }
}
