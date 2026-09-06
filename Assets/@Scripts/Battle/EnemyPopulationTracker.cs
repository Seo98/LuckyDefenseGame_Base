using System;
using System.Collections.Generic;
using R3;

/// <summary>
/// 전장에 활성화된 Enemy를 추적하고 패배 인구 제한 도달을 알립니다.
/// </summary>
public sealed class EnemyPopulationTracker : IDisposable
{
    private readonly HashSet<EnemyActor> activeEnemies = new();

    /// <summary>
    /// R3의 ReactiveProperty를 사용해 현재 활성 Enemy 수를 저장하고 변경을 발행합니다.
    /// </summary>
    private readonly ReactiveProperty<int> activeCount = new(0);

    /// <summary>
    /// R3의 Subject를 사용해 Enemy 수 제한에 도달한 순간을 발행합니다.
    /// </summary>
    private readonly Subject<int> limitReached = new();

    private readonly int maximumActiveCount;

    private bool hasReachedLimit;
    private bool isDisposed;

    /// <summary>
    /// 현재 활성 Enemy 수입니다.
    /// </summary>
    public int ActiveCount => activeEnemies.Count;

    /// <summary>
    /// 패배 조건으로 사용하는 최대 활성 Enemy 수입니다.
    /// </summary>
    public int MaximumActiveCount => maximumActiveCount;

    /// <summary>
    /// 현재 활성 Enemy 수가 변경될 때 값을 발행합니다.
    /// R3의 Observable을 사용하며, UI는 이를 구독해 현재 수를 표시할 수 있습니다.
    /// </summary>
    public Observable<int> ActiveCountChanged => activeCount;

    /// <summary>
    /// 활성 Enemy 수가 제한에 도달했을 때 도달한 수를 한 번 발행합니다.
    /// R3의 Observable을 사용하며, Reset 이후에는 다시 발행할 수 있습니다.
    /// </summary>
    public Observable<int> LimitReached => limitReached;

    /// <summary>
    /// 전투 규칙을 사용해 Enemy 인구 추적기를 생성합니다.
    /// </summary>
    public EnemyPopulationTracker(BattleRulesData battleRules)
    {
        if (battleRules == null)
        {
            throw new ArgumentNullException(nameof(battleRules));
        }

        maximumActiveCount = Math.Max(1, battleRules.MaxActiveEnemyCount);
    }

    /// <summary>
    /// 활성 Enemy를 등록합니다.
    /// 이미 등록된 Enemy라면 false를 반환합니다.
    /// </summary>
    public bool Register(EnemyActor enemy)
    {
        ThrowIfDisposed();

        if (enemy == null)
        {
            throw new ArgumentNullException(nameof(enemy));
        }

        if (!enemy.IsSpawned)
        {
            throw new InvalidOperationException($"{enemy.name} must be spawned before registration.");
        }

        if (!activeEnemies.Add(enemy))
        {
            return false;
        }

        PublishActiveCount();

        if (!hasReachedLimit && ActiveCount >= maximumActiveCount)
        {
            hasReachedLimit = true;
            limitReached.OnNext(ActiveCount);
        }

        return true;
    }

    /// <summary>
    /// 활성 Enemy 등록을 해제합니다.
    /// 등록되지 않은 Enemy라면 false를 반환합니다.
    /// </summary>
    public bool Unregister(EnemyActor enemy)
    {
        ThrowIfDisposed();

        if (ReferenceEquals(enemy, null) || !activeEnemies.Remove(enemy))
        {
            return false;
        }

        PublishActiveCount();
        return true;
    }

    /// <summary>
    /// 등록 상태와 제한 도달 상태를 새로운 전투 시작 상태로 초기화합니다.
    /// Enemy의 실제 풀 반환은 EnemyPoolService가 담당합니다.
    /// </summary>
    public void Reset()
    {
        ThrowIfDisposed();

        activeEnemies.Clear();
        hasReachedLimit = false;
        PublishActiveCount();
    }

    /// <summary>
    /// R3 스트림을 종료하고 추적 상태를 정리합니다.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        activeEnemies.Clear();
        activeCount.Dispose();
        limitReached.Dispose();
        isDisposed = true;
    }

    private void PublishActiveCount()
    {
        activeCount.Value = ActiveCount;
    }

    private void ThrowIfDisposed()
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(EnemyPopulationTracker));
        }
    }
}
