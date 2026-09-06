using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy 풀, 활성 수 추적기와 LoopPath를 연결해 Enemy의 전장 생명주기를 관리합니다.
/// </summary>
public sealed class EnemySpawner : IDisposable
{
    private readonly EnemyPoolService poolService;
    private readonly EnemyPopulationTracker populationTracker;
    private readonly LoopPath loopPath;
    private readonly HashSet<EnemyActor> spawnedEnemies = new();

    private bool isDisposed;
    private bool isReleasingAll;

    /// <summary>
    /// 현재 이 Spawner가 전장에 내보낸 Enemy 수입니다.
    /// </summary>
    public int SpawnedCount => spawnedEnemies.Count;

    /// <summary>
    /// Enemy가 정상적으로 전장에 등록된 직후 발생합니다.
    /// </summary>
    public event Action<EnemyActor> EnemySpawned;

    /// <summary>
    /// Enemy가 전장에서 제거되고 풀로 반환된 직후 발생합니다.
    /// EnemyData와 제거 사유를 사용해 보상 등의 후처리를 할 수 있습니다.
    /// </summary>
    public event Action<EnemyData, EnemyRemovalReason> EnemyRemoved;

    /// <summary>
    /// Enemy 스폰에 필요한 런타임 서비스를 연결합니다.
    /// </summary>
    public EnemySpawner(
        EnemyPoolService poolService,
        EnemyPopulationTracker populationTracker,
        LoopPath loopPath)
    {
        this.poolService = poolService ?? throw new ArgumentNullException(nameof(poolService));
        this.populationTracker = populationTracker ??
            throw new ArgumentNullException(nameof(populationTracker));
        this.loopPath = loopPath != null
            ? loopPath
            : throw new ArgumentNullException(nameof(loopPath));
    }

    /// <summary>
    /// 지정한 EnemyData의 Enemy를 경로 위에 스폰합니다.
    /// 인구 제한 처리 중 즉시 게임이 종료되면 null을 반환할 수 있습니다.
    /// </summary>
    public EnemyActor Spawn(EnemyData enemyData, int waypointIndex = 0)
    {
        ThrowIfDisposed();
        if (isReleasingAll) throw new InvalidOperationException("Cannot spawn while releasing enemies.");

        if (enemyData == null)
        {
            throw new ArgumentNullException(nameof(enemyData));
        }

        if (!loopPath.TryGetWaypoint(waypointIndex, out Transform spawnPoint))
        {
            throw new InvalidOperationException("LoopPath has no valid spawn waypoint.");
        }

        EnemyActor enemy = poolService.Get(enemyData);

        try
        {
            enemy.transform.SetPositionAndRotation(
                spawnPoint.position,
                spawnPoint.rotation);

            enemy.RemovalRequested -= HandleRemovalRequested;
            enemy.RemovalRequested += HandleRemovalRequested;
            enemy.OnSpawned(enemyData, loopPath, waypointIndex);

            if (!spawnedEnemies.Add(enemy))
            {
                throw new InvalidOperationException($"{enemy.name} is already managed by this spawner.");
            }

            if (!populationTracker.Register(enemy))
            {
                throw new InvalidOperationException($"{enemy.name} is already registered as active.");
            }

            // LimitReached 구독자가 동기적으로 게임을 종료했을 수 있습니다.
            if (enemy == null || !enemy.IsSpawned)
            {
                return null;
            }

            ValidateState();
            EnemySpawned?.Invoke(enemy);
            return enemy;
        }
        catch
        {
            CleanupFailedSpawn(enemy);
            throw;
        }
    }

    /// <summary>
    /// 이 Spawner가 관리하는 모든 Enemy를 지정한 사유로 풀에 반환합니다.
    /// </summary>
    public void ReleaseAll(EnemyRemovalReason reason = EnemyRemovalReason.GameEnded)
    {
        ThrowIfDisposed();

        if (isReleasingAll) return;
        isReleasingAll = true;
        try
        {
            while (spawnedEnemies.Count > 0)
            {
                using HashSet<EnemyActor>.Enumerator enumerator = spawnedEnemies.GetEnumerator();
                enumerator.MoveNext();
                EnemyActor enemy = enumerator.Current;
                // 제거 이벤트가 없거나 이미 파괴됐더라도 Spawner가 직접 등록을 정리합니다.
                try { HandleRemovalRequested(enemy, reason); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
        finally { isReleasingAll = false; }
    }

    /// <summary>
    /// 관리 중인 Enemy를 정리하고 외부 이벤트 참조를 해제합니다.
    /// PoolService와 PopulationTracker의 소유권은 외부 조립 지점에 있습니다.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        ReleaseAll();
        EnemySpawned = null;
        EnemyRemoved = null;
        isDisposed = true;
    }

    private void HandleRemovalRequested(
        EnemyActor enemy,
        EnemyRemovalReason reason)
    {
        if (ReferenceEquals(enemy, null) || !spawnedEnemies.Remove(enemy))
        {
            return;
        }

        EnemyData enemyData = enemy != null ? enemy.Data : null;

        enemy.RemovalRequested -= HandleRemovalRequested;
        try { populationTracker.Unregister(enemy); }
        finally { if (poolService.IsRented(enemy)) poolService.Release(enemy); }

        ValidateState();
        EnemyRemoved?.Invoke(enemyData, reason);
    }

    private void CleanupFailedSpawn(EnemyActor enemy)
    {
        if (ReferenceEquals(enemy, null))
        {
            return;
        }

        enemy.RemovalRequested -= HandleRemovalRequested;
        spawnedEnemies.Remove(enemy);
        populationTracker.Unregister(enemy);
        if (poolService.IsRented(enemy)) poolService.Release(enemy);
    }

    private void ThrowIfDisposed()
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(EnemySpawner));
        }
    }

    /// <summary>개발 빌드에서 Spawner·인구수·풀의 활성 등록 수를 검사합니다.</summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public void ValidateState()
    {
        if (SpawnedCount != populationTracker.ActiveCount || SpawnedCount != poolService.RentedCount)
            Debug.LogError("Enemy registrations disagree: spawner / population / pool.");
    }
}
