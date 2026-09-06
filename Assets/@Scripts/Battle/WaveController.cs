using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

/// <summary>
/// WaveSequenceData를 순서대로 실행하고 스폰, 웨이브 전환 및 패배를 판정합니다.
/// Unity 컴포넌트와 DI 구성에 의존하지 않는 런타임 제어 객체입니다.
/// </summary>
public sealed class WaveController : IDisposable
{
    /// <summary>
    /// 전체 웨이브 실행 상태입니다.
    /// </summary>
    public enum RunState
    {
        Idle,
        Running,
        Completed,
        Defeated,
        Stopped
    }

    /// <summary>
    /// 전투가 패배로 끝난 원인입니다.
    /// </summary>
    public enum DefeatReason
    {
        None,
        EnemyLimitReached,
        BossTimeExpired
    }

    private readonly WaveSequenceData sequenceData;
    private readonly EnemySpawner enemySpawner;
    private readonly EnemyPopulationTracker populationTracker;

    /// <summary>
    /// R3의 ReactiveProperty를 사용해 전체 웨이브 실행 상태를 저장하고 변경을 발행합니다.
    /// </summary>
    private readonly ReactiveProperty<RunState> runState = new(RunState.Idle);

    /// <summary>
    /// R3의 ReactiveProperty를 사용해 현재 웨이브 인덱스를 저장하고 변경을 발행합니다.
    /// </summary>
    private readonly ReactiveProperty<int> currentWaveIndex = new(-1);

    /// <summary>
    /// R3의 ReactiveProperty를 사용해 현재 웨이브의 남은 시간을 저장하고 변경을 발행합니다.
    /// </summary>
    private readonly ReactiveProperty<float> remainingTime = new(0f);

    private CancellationTokenSource runCancellation;

    /// <summary>
    /// R3의 LimitReached Observable 구독 수명을 관리합니다.
    /// </summary>
    private IDisposable populationLimitSubscription;
    private EnemyData currentBossTarget;
    private DefeatReason currentDefeatReason;
    private bool isCurrentBossDefeated;
    private bool isDisposed;

    /// <summary>
    /// 현재 전체 실행 상태입니다.
    /// </summary>
    public RunState State => runState.Value;

    /// <summary>
    /// 현재 실행 중인 웨이브의 0부터 시작하는 인덱스입니다.
    /// 실행 전에는 -1입니다.
    /// </summary>
    public int CurrentWaveIndex => currentWaveIndex.Value;

    /// <summary>
    /// 현재 웨이브의 남은 시간(초)입니다.
    /// </summary>
    public float RemainingTime => remainingTime.Value;

    /// <summary>
    /// 현재 패배 원인입니다. 패배 전에는 None입니다.
    /// </summary>
    public DefeatReason CurrentDefeatReason => currentDefeatReason;

    /// <summary>
    /// R3의 Observable을 사용해 전체 실행 상태가 바뀔 때 값을 발행합니다.
    /// </summary>
    public Observable<RunState> StateChanged => runState;

    /// <summary>
    /// R3의 Observable을 사용해 현재 웨이브 인덱스가 바뀔 때 값을 발행합니다.
    /// </summary>
    public Observable<int> CurrentWaveIndexChanged => currentWaveIndex;

    /// <summary>
    /// R3의 Observable을 사용해 현재 웨이브의 남은 시간이 바뀔 때 값을 발행합니다.
    /// </summary>
    public Observable<float> RemainingTimeChanged => remainingTime;

    /// <summary>
    /// 새 웨이브가 시작된 직후 발생합니다.
    /// </summary>
    public event Action<int, WaveData> WaveStarted;

    /// <summary>
    /// 웨이브의 성공 조건을 만족한 직후 발생합니다.
    /// </summary>
    public event Action<int, WaveData> WaveCompleted;

    /// <summary>
    /// 전체 시퀀스가 정상적으로 끝났을 때 발생합니다.
    /// </summary>
    public event Action SequenceCompleted;

    /// <summary>
    /// 패배 조건이 확정된 직후 발생합니다.
    /// </summary>
    public event Action<DefeatReason> Defeated;

    /// <summary>
    /// 웨이브 실행에 필요한 데이터와 런타임 서비스를 연결합니다.
    /// </summary>
    public WaveController(
        WaveSequenceData sequenceData,
        EnemySpawner enemySpawner,
        EnemyPopulationTracker populationTracker)
    {
        this.sequenceData = sequenceData != null
            ? sequenceData
            : throw new ArgumentNullException(nameof(sequenceData));
        this.enemySpawner = enemySpawner ??
            throw new ArgumentNullException(nameof(enemySpawner));
        this.populationTracker = populationTracker ??
            throw new ArgumentNullException(nameof(populationTracker));
    }

    /// <summary>
    /// 등록된 웨이브를 처음부터 순서대로 실행합니다.
    /// 외부 취소 또는 Stop 호출 시 예외를 외부로 전파하지 않고 Stopped 상태가 됩니다.
    /// Cysharp UniTask를 사용해 Unity PlayerLoop에서 웨이브를 비동기로 실행합니다.
    /// </summary>
    public async UniTask RunAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (State == RunState.Running)
        {
            throw new InvalidOperationException("WaveController is already running.");
        }

        if (!sequenceData.IsValid)
        {
            throw new InvalidOperationException($"{sequenceData.name} has invalid wave settings.");
        }

        ResetRuntimeState();
        runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        populationLimitSubscription = populationTracker.LimitReached.Subscribe(
            HandlePopulationLimitReached);
        enemySpawner.EnemyRemoved += HandleEnemyRemoved;
        runState.Value = RunState.Running;

        try
        {
            for (int i = 0; i < sequenceData.WaveCount; i++)
            {
                await RunWaveAsync(i, sequenceData.GetWave(i), runCancellation.Token);

                if (currentDefeatReason != DefeatReason.None)
                {
                    FinishAsDefeated();
                    return;
                }
            }

            remainingTime.Value = 0f;
            runState.Value = RunState.Completed;
            SequenceCompleted?.Invoke();
        }
        catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
        {
            if (!isDisposed)
            {
                remainingTime.Value = 0f;
                runState.Value = RunState.Stopped;
            }
        }
        catch
        {
            if (!isDisposed)
            {
                remainingTime.Value = 0f;
                runState.Value = RunState.Stopped;
            }

            throw;
        }
        finally
        {
            UnsubscribeRuntimeEvents();
            runCancellation?.Dispose();
            runCancellation = null;
            currentBossTarget = null;
        }
    }

    /// <summary>
    /// 실행 중인 웨이브 시퀀스를 중지합니다.
    /// 현재 활성 Enemy의 제거 여부는 외부 게임 규칙에서 결정합니다.
    /// </summary>
    public void Stop()
    {
        if (isDisposed || State != RunState.Running)
        {
            return;
        }

        runCancellation?.Cancel();
    }

    /// <summary>
    /// 실행을 중지하고 이벤트 및 R3 상태 스트림을 정리합니다.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        runCancellation?.Cancel();
        UnsubscribeRuntimeEvents();

        WaveStarted = null;
        WaveCompleted = null;
        SequenceCompleted = null;
        Defeated = null;

        runState.Dispose();
        currentWaveIndex.Dispose();
        remainingTime.Dispose();
    }

    /// <summary>
    /// Cysharp UniTask를 사용해 한 웨이브의 스폰과 시간 기반 종료 조건을 실행합니다.
    /// </summary>
    private async UniTask RunWaveAsync(
        int waveIndex,
        WaveData waveData,
        CancellationToken cancellationToken)
    {
        currentWaveIndex.Value = waveIndex;
        remainingTime.Value = waveData.Duration;
        currentBossTarget = waveData.Rule == WaveData.CompletionRule.DefeatBossBeforeTimeExpires
            ? waveData.BossTarget
            : null;
        isCurrentBossDefeated = false;

        WaveStarted?.Invoke(waveIndex, waveData);

        using CancellationTokenSource waveCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        UniTask[] spawnTasks = StartSpawnGroups(waveData, waveCancellation.Token);

        try
        {
            float elapsedTime = 0f;

            while (elapsedTime < waveData.Duration)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (currentDefeatReason != DefeatReason.None)
                {
                    return;
                }

                if (currentBossTarget != null && isCurrentBossDefeated)
                {
                    WaveCompleted?.Invoke(waveIndex, waveData);
                    return;
                }

                // UniTask.Yield로 Unity Update PlayerLoop까지 비동기 대기합니다.
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                elapsedTime += Time.deltaTime;
                remainingTime.Value = Mathf.Max(0f, waveData.Duration - elapsedTime);
            }

            if (currentBossTarget != null && isCurrentBossDefeated)
            {
                WaveCompleted?.Invoke(waveIndex, waveData);
                return;
            }

            if (waveData.Rule == WaveData.CompletionRule.DefeatBossBeforeTimeExpires)
            {
                currentDefeatReason = DefeatReason.BossTimeExpired;
                return;
            }

            WaveCompleted?.Invoke(waveIndex, waveData);
        }
        finally
        {
            waveCancellation.Cancel();
            await AwaitSpawnTasksAsync(spawnTasks, waveCancellation.Token);
            currentBossTarget = null;
        }
    }

    /// <summary>
    /// Cysharp UniTask 배열로 모든 스폰 그룹의 비동기 작업을 동시에 시작합니다.
    /// </summary>
    private UniTask[] StartSpawnGroups(WaveData waveData, CancellationToken cancellationToken)
    {
        int groupCount = waveData.SpawnGroupCount;
        UniTask[] tasks = new UniTask[groupCount];

        for (int i = 0; i < groupCount; i++)
        {
            tasks[i] = SpawnGroupAsync(waveData.GetSpawnGroup(i), cancellationToken);
        }

        return tasks;
    }

    /// <summary>
    /// Cysharp UniTask를 사용해 그룹 시작 지연과 Enemy별 스폰 간격을 처리합니다.
    /// </summary>
    private async UniTask SpawnGroupAsync(
        EnemySpawnGroup spawnGroup,
        CancellationToken cancellationToken)
    {
        if (spawnGroup.StartDelay > 0f)
        {
            // UniTask.Delay로 Time.timeScale의 영향을 받는 시작 지연을 처리합니다.
            await UniTask.Delay(
                TimeSpan.FromSeconds(spawnGroup.StartDelay),
                cancellationToken: cancellationToken);
        }

        for (int i = 0; i < spawnGroup.SpawnCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            enemySpawner.Spawn(spawnGroup.EnemyData, spawnGroup.SpawnWaypointIndex);

            if (i + 1 < spawnGroup.SpawnCount && spawnGroup.SpawnInterval > 0f)
            {
                // UniTask.Delay로 같은 그룹 내 Enemy 사이의 스폰 간격을 처리합니다.
                await UniTask.Delay(
                    TimeSpan.FromSeconds(spawnGroup.SpawnInterval),
                    cancellationToken: cancellationToken);
            }
        }
    }

    /// <summary>
    /// Cysharp UniTask.WhenAll을 사용해 병렬 스폰 작업을 모두 관찰하고 정상 취소를 처리합니다.
    /// </summary>
    private static async UniTask AwaitSpawnTasksAsync(
        UniTask[] spawnTasks,
        CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.WhenAll(spawnTasks);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 웨이브 종료로 남은 예약 스폰을 정상 취소한 경우입니다.
        }
    }

    private void HandleEnemyRemoved(EnemyData enemyData, EnemyRemovalReason reason)
    {
        if (reason == EnemyRemovalReason.Killed && enemyData == currentBossTarget)
        {
            isCurrentBossDefeated = true;
        }
    }

    private void HandlePopulationLimitReached(int activeEnemyCount)
    {
        if (State == RunState.Running)
        {
            currentDefeatReason = DefeatReason.EnemyLimitReached;
        }
    }

    private void FinishAsDefeated()
    {
        remainingTime.Value = 0f;
        enemySpawner.ReleaseAll(EnemyRemovalReason.GameEnded);
        runState.Value = RunState.Defeated;
        Defeated?.Invoke(currentDefeatReason);
    }

    private void ResetRuntimeState()
    {
        currentWaveIndex.Value = -1;
        remainingTime.Value = 0f;
        currentDefeatReason = DefeatReason.None;
        currentBossTarget = null;
        isCurrentBossDefeated = false;
    }

    private void UnsubscribeRuntimeEvents()
    {
        enemySpawner.EnemyRemoved -= HandleEnemyRemoved;
        populationLimitSubscription?.Dispose();
        populationLimitSubscription = null;
    }

    private void ThrowIfDisposed()
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(WaveController));
        }
    }
}
