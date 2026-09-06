using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Addressable 영웅 프리팹의 로드 핸들과 HeroData별 오브젝트 풀을 관리합니다.
/// </summary>
public sealed class HeroPoolService : IDisposable
{
    private sealed class PoolEntry
    {
        public AsyncOperationHandle<GameObject> LoadHandle;
        public ObjectPool<HeroActor> Pool;
    }

    private readonly Dictionary<HeroData, PoolEntry> entries = new();
    private readonly Dictionary<HeroActor, HeroData> rentedHeroes = new();
    private readonly Transform poolRoot;
    private readonly CancellationTokenSource lifetimeCancellation = new();

    private bool isDisposed;

    /// <summary>
    /// 현재 풀에서 대여 중인 영웅 수입니다.
    /// </summary>
    public int RentedCount => rentedHeroes.Count;

    /// <summary>풀 반환 또는 외부 파괴 시, 데이터 초기화 전에 외부 등록을 해제하도록 알립니다.</summary>
    public event Action<HeroActor> HeroReleasing;

    /// <summary>파괴된 Unity 객체도 CLR 참조로 대여 여부를 확인합니다.</summary>
    public bool IsRented(HeroActor hero) => !ReferenceEquals(hero, null) && rentedHeroes.ContainsKey(hero);

    /// <summary>
    /// 비활성 영웅을 정리할 선택적 부모 Transform을 지정합니다.
    /// </summary>
    public HeroPoolService(Transform poolRoot = null)
    {
        this.poolRoot = poolRoot;
    }

    /// <summary>
    /// Cysharp UniTask와 Addressables를 사용해 HeroData의 프리팹을 로드하고 풀을 예열합니다.
    /// 이미 준비된 데이터에는 아무 작업도 하지 않습니다.
    /// </summary>
    public async UniTask PrepareAsync(
        HeroData heroData,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateHeroData(heroData);
        cancellationToken.ThrowIfCancellationRequested();

        if (entries.ContainsKey(heroData))
        {
            return;
        }

        using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, lifetimeCancellation.Token);
        CancellationToken token = linkedCancellation.Token;
        AsyncOperationHandle<GameObject> loadHandle =
            Addressables.LoadAssetAsync<GameObject>(heroData.PrefabReference.RuntimeKey);
        PoolEntry pendingEntry = null;
        bool transferred = false;

        try
        {
            GameObject prefab = await loadHandle.ToUniTask(
                cancellationToken: token, cancelImmediately: true, autoReleaseWhenCanceled: false);
            token.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            // 같은 데이터를 동시에 준비한 다른 호출이 먼저 등록했으면 이 호출의 핸들만 해제합니다.
            if (entries.ContainsKey(heroData)) return;

            if (prefab == null || prefab.GetComponent<HeroActor>() == null)
            {
                throw new InvalidOperationException(
                    $"{heroData.name} prefab must contain HeroActor on its root.");
            }

            pendingEntry = CreatePoolEntry(prefab, loadHandle, heroData.PoolPrewarmCount);
            HeroActor[] prewarmed = new HeroActor[heroData.PoolPrewarmCount];
            try
            {
                // 모두 빌린 뒤 반환해야 서로 다른 인스턴스 N개가 생성됩니다.
                for (int i = 0; i < prewarmed.Length; i++) prewarmed[i] = pendingEntry.Pool.Get();
            }
            finally
            {
                foreach (HeroActor hero in prewarmed)
                    if (hero != null) pendingEntry.Pool.Release(hero);
            }
            token.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            entries.Add(heroData, pendingEntry);
            transferred = true;
        }
        finally
        {
            if (!transferred)
            {
                pendingEntry?.Pool.Clear();
                if (loadHandle.IsValid()) Addressables.Release(loadHandle);
            }
        }
    }

    /// <summary>
    /// Cysharp UniTask를 사용해 여러 HeroData 풀을 순서대로 준비합니다.
    /// </summary>
    public async UniTask PrepareAllAsync(
        IEnumerable<HeroData> heroDataCollection,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (heroDataCollection == null)
        {
            throw new ArgumentNullException(nameof(heroDataCollection));
        }

        foreach (HeroData heroData in heroDataCollection)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await PrepareAsync(heroData, cancellationToken);
        }
    }

    /// <summary>
    /// Cysharp UniTask로 Addressable 프리팹을 준비한 뒤 영웅을 풀에서 꺼냅니다.
    /// </summary>
    public async UniTask<HeroActor> GetAsync(
        HeroData heroData,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await PrepareAsync(heroData, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        PoolEntry entry = entries[heroData];
        HeroActor hero;
        do { hero = entry.Pool.Get(); } while (hero == null);

        try
        {
            hero.gameObject.SetActive(true);
            hero.OnSpawned(heroData);

            if (!rentedHeroes.TryAdd(hero, heroData))
            {
                throw new InvalidOperationException($"{hero.name} is already rented.");
            }

            hero.Destroyed += HandleDestroyed;
            return hero;
        }
        catch
        {
            if (hero.IsSpawned)
            {
                hero.OnDespawned();
            }

            entry.Pool.Release(hero);
            throw;
        }
    }

    /// <summary>
    /// 대여 중인 영웅을 런타임 초기화 후 원래 HeroData 풀에 반환합니다.
    /// </summary>
    public bool Release(HeroActor hero)
    {
        if (ReferenceEquals(hero, null) || !rentedHeroes.Remove(hero, out HeroData heroData))
        {
            return false;
        }

        hero.Destroyed -= HandleDestroyed;
        try { HeroReleasing?.Invoke(hero); }
        finally
        {
            if (hero != null)
            {
                try { hero.OnDespawned(); }
                finally { entries[heroData].Pool.Release(hero); }
            }
        }
        return true;
    }

    private void HandleDestroyed(HeroActor hero)
    {
        if (!rentedHeroes.Remove(hero)) return;
        hero.Destroyed -= HandleDestroyed;
        HeroReleasing?.Invoke(hero);
    }

    /// <summary>
    /// 모든 대여 객체와 풀을 정리하고 Addressable 로드 핸들을 해제합니다.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        lifetimeCancellation.Cancel();
        while (rentedHeroes.Count > 0)
        {
            using Dictionary<HeroActor, HeroData>.Enumerator enumerator =
                rentedHeroes.GetEnumerator();
            enumerator.MoveNext();
            try { Release(enumerator.Current.Key); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        foreach (PoolEntry entry in entries.Values)
        {
            try { entry.Pool.Clear(); }
            finally
            {
                if (entry.LoadHandle.IsValid()) Addressables.Release(entry.LoadHandle);
            }
        }

        entries.Clear();
        HeroReleasing = null;
        lifetimeCancellation.Dispose();
    }

    private PoolEntry CreatePoolEntry(
        GameObject prefab,
        AsyncOperationHandle<GameObject> loadHandle, int prewarmCount)
    {
        ObjectPool<HeroActor> pool = null;

        pool = new ObjectPool<HeroActor>(
            createFunc: () => CreateInstance(prefab),
            actionOnGet: null,
            actionOnRelease: hero =>
            {
                if (hero == null) return;
                hero.gameObject.SetActive(false);

                if (poolRoot != null)
                {
                    hero.transform.SetParent(poolRoot, false);
                }
            },
            actionOnDestroy: hero =>
            {
                if (hero != null)
                {
                    UnityEngine.Object.Destroy(hero.gameObject);
                }
            },
            collectionCheck: true,
            defaultCapacity: Math.Max(1, prewarmCount),
            maxSize: Math.Max(64, prewarmCount));

        return new PoolEntry
        {
            LoadHandle = loadHandle,
            Pool = pool
        };
    }

    private HeroActor CreateInstance(GameObject prefab)
    {
        GameObject instance = UnityEngine.Object.Instantiate(prefab, poolRoot);
        instance.SetActive(false);
        return instance.GetComponent<HeroActor>();
    }

    private static void ValidateHeroData(HeroData heroData)
    {
        if (heroData == null)
        {
            throw new ArgumentNullException(nameof(heroData));
        }

        if (!heroData.HasValidPrefabReference)
        {
            throw new InvalidOperationException(
                $"{heroData.name} has no valid Addressable prefab reference.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(HeroPoolService));
        }
    }
}
