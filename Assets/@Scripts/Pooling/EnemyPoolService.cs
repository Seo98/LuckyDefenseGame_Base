using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// EnemyData별 EnemyActor 풀을 생성하고 재사용 생명주기를 관리합니다.
/// </summary>
public sealed class EnemyPoolService : IDisposable
{
    private const int DefaultMaxSizePerPool = 128;

    private readonly Dictionary<EnemyData, ObjectPool<EnemyActor>> pools = new();
    private readonly Dictionary<EnemyActor, ObjectPool<EnemyActor>> ownerPools = new();
    private readonly HashSet<EnemyActor> rentedEnemies = new();

    private readonly Transform poolRoot;
    private readonly bool collectionCheck;
    private readonly int maxSizePerPool;

    private bool isDisposed;

    /// <summary>
    /// 현재 풀에서 대여 중인 전체 Enemy 수입니다.
    /// </summary>
    public int RentedCount => rentedEnemies.Count;

    /// <summary>파괴된 Unity 객체를 포함해 현재 대여 등록 여부를 확인합니다.</summary>
    public bool IsRented(EnemyActor enemy) => !ReferenceEquals(enemy, null) && rentedEnemies.Contains(enemy);

    /// <summary>
    /// Enemy 풀 서비스를 생성합니다.
    /// </summary>
    /// <param name="poolRoot">생성된 Enemy를 정리할 부모 Transform입니다.</param>
    /// <param name="collectionCheck">중복 반환 검사를 사용할지 여부입니다.</param>
    /// <param name="maxSizePerPool">Enemy 종류 하나가 보관할 최대 풀 크기입니다.</param>
    public EnemyPoolService(
        Transform poolRoot = null,
        bool collectionCheck = true,
        int maxSizePerPool = DefaultMaxSizePerPool)
    {
        this.poolRoot = poolRoot;
        this.collectionCheck = collectionCheck;
        this.maxSizePerPool = Mathf.Max(1, maxSizePerPool);
    }

    /// <summary>
    /// EnemyData에 설정된 수만큼 인스턴스를 미리 생성합니다.
    /// 이미 생성된 풀은 부족한 수량만 추가합니다.
    /// </summary>
    public void Prewarm(EnemyData enemyData)
    {
        ThrowIfDisposed();
        ValidateEnemyData(enemyData);

        ObjectPool<EnemyActor> pool = GetOrCreatePool(enemyData);
        int requiredCount = Mathf.Max(0, enemyData.PoolPrewarmCount - pool.CountAll);

        if (requiredCount == 0)
        {
            return;
        }

        EnemyActor[] temporaryEnemies = new EnemyActor[requiredCount];

        for (int i = 0; i < requiredCount; i++)
        {
            temporaryEnemies[i] = pool.Get();
        }

        for (int i = 0; i < temporaryEnemies.Length; i++)
        {
            pool.Release(temporaryEnemies[i]);
        }
    }

    /// <summary>
    /// 여러 EnemyData의 풀을 미리 생성합니다.
    /// </summary>
    public void Prewarm(IEnumerable<EnemyData> enemyDataCollection)
    {
        ThrowIfDisposed();

        if (enemyDataCollection == null)
        {
            throw new ArgumentNullException(nameof(enemyDataCollection));
        }

        foreach (EnemyData enemyData in enemyDataCollection)
        {
            Prewarm(enemyData);
        }
    }

    /// <summary>
    /// 지정한 EnemyData에 해당하는 Enemy를 풀에서 대여합니다.
    /// 반환된 Enemy는 아직 전투 초기화되지 않았으므로 OnSpawned를 호출해야 합니다.
    /// </summary>
    public EnemyActor Get(EnemyData enemyData)
    {
        ThrowIfDisposed();
        ValidateEnemyData(enemyData);

        ObjectPool<EnemyActor> pool = GetOrCreatePool(enemyData);
        EnemyActor enemy;
        do { enemy = pool.Get(); } while (enemy == null);

        if (!rentedEnemies.Add(enemy))
        {
            throw new InvalidOperationException($"{enemy.name} is already rented.");
        }

        return enemy;
    }

    /// <summary>
    /// 대여한 Enemy를 원래 풀로 반환합니다.
    /// </summary>
    public void Release(EnemyActor enemy)
    {
        ThrowIfDisposed();

        if (ReferenceEquals(enemy, null))
        {
            throw new ArgumentNullException(nameof(enemy));
        }

        if (!ownerPools.TryGetValue(enemy, out ObjectPool<EnemyActor> pool))
        {
            throw new InvalidOperationException($"{enemy.name} was not created by this pool service.");
        }

        if (!rentedEnemies.Remove(enemy))
        {
            throw new InvalidOperationException($"{enemy.name} is not currently rented.");
        }

        if (enemy == null)
        {
            ownerPools.Remove(enemy);
            return;
        }

        pool.Release(enemy);
    }

    /// <summary>
    /// 현재 대여 중인 모든 Enemy를 각자의 풀로 반환합니다.
    /// </summary>
    public void ReleaseAll()
    {
        ThrowIfDisposed();

        while (rentedEnemies.Count > 0)
        {
            using HashSet<EnemyActor>.Enumerator enumerator = rentedEnemies.GetEnumerator();
            enumerator.MoveNext();
            Release(enumerator.Current);
        }
    }

    /// <summary>
    /// 모든 Enemy를 반환하고 생성된 풀 인스턴스를 제거합니다.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        ReleaseAll();

        foreach (ObjectPool<EnemyActor> pool in pools.Values)
        {
            pool.Clear();
        }

        pools.Clear();
        ownerPools.Clear();
        isDisposed = true;
    }

    private ObjectPool<EnemyActor> GetOrCreatePool(EnemyData enemyData)
    {
        if (pools.TryGetValue(enemyData, out ObjectPool<EnemyActor> existingPool))
        {
            return existingPool;
        }

        ObjectPool<EnemyActor> pool = null;
        int initialCapacity = Mathf.Max(1, enemyData.PoolPrewarmCount);
        int maximumSize = Mathf.Max(maxSizePerPool, initialCapacity);

        pool = new ObjectPool<EnemyActor>(
            createFunc: () => CreateEnemy(enemyData, pool),
            actionOnGet: OnGetEnemy,
            actionOnRelease: OnReleaseEnemy,
            actionOnDestroy: OnDestroyEnemy,
            collectionCheck: collectionCheck,
            defaultCapacity: initialCapacity,
            maxSize: maximumSize);

        pools.Add(enemyData, pool);
        return pool;
    }

    private EnemyActor CreateEnemy(
        EnemyData enemyData,
        ObjectPool<EnemyActor> ownerPool)
    {
        EnemyActor enemy = UnityEngine.Object.Instantiate(enemyData.Prefab, poolRoot);
        enemy.gameObject.SetActive(false);
        ownerPools.Add(enemy, ownerPool);
        return enemy;
    }

    private static void OnGetEnemy(EnemyActor enemy)
    {
        if (enemy != null) enemy.gameObject.SetActive(true);
    }

    private static void OnReleaseEnemy(EnemyActor enemy)
    {
        enemy.OnDespawned();
        enemy.gameObject.SetActive(false);
    }

    private void OnDestroyEnemy(EnemyActor enemy)
    {
        if (enemy == null)
        {
            return;
        }

        rentedEnemies.Remove(enemy);
        ownerPools.Remove(enemy);
        UnityEngine.Object.Destroy(enemy.gameObject);
    }

    private static void ValidateEnemyData(EnemyData enemyData)
    {
        if (enemyData == null)
        {
            throw new ArgumentNullException(nameof(enemyData));
        }

        if (enemyData.Prefab == null)
        {
            throw new InvalidOperationException($"{enemyData.name} has no EnemyActor prefab.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(EnemyPoolService));
        }
    }
}
