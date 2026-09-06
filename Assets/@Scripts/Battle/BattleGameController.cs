using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 프로토타입 전투에 필요한 런타임 서비스를 조립하고 생명주기를 관리합니다.
/// VContainer 없이 동작하며, 각 생성 코드는 추후 LifetimeScope 등록으로 옮길 수 있습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleGameController : MonoBehaviour
{
    /// <summary>한 판의 준비 → 진행 → 결과 → 재초기화 상태입니다.</summary>
    public enum SessionPhase { Ready, Running, Result, Restarting }
    /// <summary>현재 게임 세션 상태입니다.</summary>
    public SessionPhase SessionState { get; private set; } = SessionPhase.Ready;
    /// <summary>승리/패배/사용자 종료 문구입니다.</summary>
    public string SessionResult { get; private set; } = "전투 종료";
    /// <summary>시작/종료 UI와 조작 게이트 갱신 이벤트입니다.</summary>
    public event Action SessionStateChanged;
    /// <summary>전투 진행 중에만 영웅 조작을 허용합니다.</summary>
    public bool CanOperate => CanOperateHeroes();
    [Header("Data")]
    [SerializeField] private BattleRulesData battleRules;
    [SerializeField] private HeroSummonTableData heroSummonTable;
    [SerializeField] private WaveSequenceData waveSequence;
    [SerializeField] private HeroRecipeCatalog heroRecipes;

    [Header("Scene References")]
    [SerializeField] private LoopPath loopPath;
    [SerializeField] private HeroPlacementBoard placementBoard;
    [SerializeField] private HeroSummonStagingArea heroStagingArea;
    [SerializeField] private BattleHudView hudView;
    [SerializeField] private HeroSynthesisView synthesisView;
    [SerializeField] private BattleSessionView sessionView;
    [SerializeField] private Transform enemyPoolRoot;
    [SerializeField] private Transform heroPoolRoot;

    [Header("Prototype")]
    [SerializeField] private bool autoStartWaves;

    private CancellationTokenSource lifetimeCancellation;
    private EnemyPoolService enemyPool;
    private HeroPoolService heroPool;
    private EnemyPopulationTracker enemyPopulation;
    private HeroPopulationTracker heroPopulation;
    private MineralWallet mineralWallet;
    private EnemySpawner enemySpawner;
    private EnemyRewardService enemyReward;
    private HeroSummonService heroSummon;
    private WaveController waveController;
    private EnemyActor currentBoss;
    private EnemyData currentBossData;
    private bool initialized;
    private HeroDragController heroDrag;
    private HeroSelection heroSelection;
    private HeroSynthesisService heroSynthesis;
    private HeroSynthesisPresenter synthesisPresenter;
    private SynthesisItemInventory synthesisItems;
    /// <summary>Cysharp UniTaskCompletionSource의 완료 신호를 재시작 전에 기다립니다.</summary>
    private UniTask waveRunTask = UniTask.CompletedTask;
    private bool destroying;

    /// <summary>벤치/필드의 선택 영웅 상태입니다.</summary>
    public HeroSelection Selection => heroSelection;
    /// <summary>합성 요청의 서비스 진입점입니다.</summary>
    public HeroSynthesisService Synthesis => heroSynthesis;
    /// <summary>아이템 드롭 등 외부 시스템에서 지급할 진화 아이템 보관함입니다.</summary>
    public SynthesisItemInventory SynthesisItems => synthesisItems;

    /// <summary>
    /// 영웅 소환 요청이 처리된 직후 결과를 전달합니다.
    /// </summary>
    public event Action<HeroSummonService.SummonResult> SummonCompleted;

    /// <summary>
    /// 보스 체력 또는 표시 상태가 변경될 때 현재 체력, 최대 체력, 표시 여부를 전달합니다.
    /// </summary>
    public event Action<float, float, bool> BossHealthChanged;

    /// <summary>
    /// 현재 조립된 미네랄 지갑입니다.
    /// </summary>
    public MineralWallet MineralWallet => mineralWallet;

    /// <summary>
    /// 현재 조립된 영웅 인구 추적기입니다.
    /// </summary>
    public HeroPopulationTracker HeroPopulation => heroPopulation;

    /// <summary>
    /// 현재 조립된 적 인구 추적기입니다.
    /// </summary>
    public EnemyPopulationTracker EnemyPopulation => enemyPopulation;

    /// <summary>
    /// 현재 조립된 웨이브 컨트롤러입니다.
    /// </summary>
    public WaveController Waves => waveController;

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        if (autoStartWaves && initialized)
        {
            StartWaves();
        }
    }

    /// <summary>
    /// 직렬화된 데이터와 씬 참조를 사용해 모든 전투 서비스를 수동 조립합니다.
    /// </summary>
    public void Initialize()
    {
        if (initialized)
        {
            return;
        }

        ValidateConfiguration();
        lifetimeCancellation = new CancellationTokenSource();

        mineralWallet = new MineralWallet(battleRules);
        heroPopulation = new HeroPopulationTracker(battleRules);
        enemyPopulation = new EnemyPopulationTracker(battleRules);
        heroPool = new HeroPoolService(heroPoolRoot);
        enemyPool = new EnemyPoolService(enemyPoolRoot);
        enemySpawner = new EnemySpawner(enemyPool, enemyPopulation, loopPath);
        enemyReward = new EnemyRewardService(enemySpawner, mineralWallet);
        heroSummon = new HeroSummonService(
            heroSummonTable,
            heroPool,
            heroPopulation,
            heroStagingArea,
            mineralWallet,
            battleRules,
            placementBoard,
            CanOperateHeroes);
        waveController = new WaveController(waveSequence, enemySpawner, enemyPopulation);
        heroDrag = placementBoard.GetComponent<HeroDragController>();
        if (heroDrag == null) heroDrag = GetComponent<HeroDragController>();
        if (heroDrag == null) heroDrag = FindFirstObjectByType<HeroDragController>();
        waveController.Defeated += HandleBattleDefeated;
        waveController.SequenceCompleted += HandleBattleEnded;

        PrewarmEnemyPools();
        enemySpawner.EnemySpawned += HandleEnemySpawned;
        enemySpawner.EnemyRemoved += HandleEnemyRemoved;
        hudView?.Bind(this);
        initialized = true;
        SessionState = SessionPhase.Ready;
        waveRunTask = UniTask.CompletedTask;
        heroSelection = new HeroSelection();
        if (heroDrag != null) heroDrag.SelectionRequested += heroSelection.Select;
        if (heroRecipes != null && synthesisView != null)
        {
            var roster = new HeroRoster(heroPopulation);
            synthesisItems = new SynthesisItemInventory();
            heroSynthesis = new HeroSynthesisService(heroPool, heroSummon, heroPopulation,
                heroStagingArea, placementBoard, roster, synthesisItems, CanOperateHeroes);
            heroSynthesis.BusyChanged += HandleSynthesisBusy;
            synthesisPresenter = new HeroSynthesisPresenter(synthesisView, heroSelection, heroRecipes,
                roster, heroSynthesis, synthesisItems, mineralWallet, heroPopulation,
                heroStagingArea, placementBoard, heroSummon, CanOperateHeroes);
        }
        if (sessionView == null && synthesisView != null) sessionView = synthesisView.GetComponent<BattleSessionView>();
        sessionView?.Bind(this);
        if (heroDrag != null) heroDrag.SetInteractionEnabled(false);
        SessionStateChanged?.Invoke();
    }

    private bool CanOperateHeroes() => initialized && !destroying && SessionState == SessionPhase.Running && waveController.State != WaveController.RunState.Defeated &&
                                      waveController.State != WaveController.RunState.Completed;

    private void HandleSynthesisBusy(bool busy)
    {
        if (heroDrag != null) heroDrag.SetInteractionEnabled(!busy && CanOperateHeroes());
    }

    /// <summary>
    /// 등록된 웨이브 시퀀스를 실행합니다.
    /// Cysharp UniTask 실행은 내부에서 관찰하며 오류를 Unity Console에 기록합니다.
    /// </summary>
    public void StartWaves()
    {
        if (!initialized || SessionState != SessionPhase.Ready || waveController.State != WaveController.RunState.Idle)
        {
            return;
        }

        SessionState = SessionPhase.Running;
        if (heroDrag != null) heroDrag.SetInteractionEnabled(true);
        SessionStateChanged?.Invoke();
        synthesisPresenter?.Refresh();
        var completion = new UniTaskCompletionSource();
        waveRunTask = completion.Task;
        RunWavesAsync(waveController, lifetimeCancellation.Token, completion).Forget();
    }

    /// <summary>현재 판을 종료합니다. 실행 중 로드/웨이브는 취소하고 결과 화면으로 전환합니다.</summary>
    public void EndSession()
    {
        if (!initialized || SessionState != SessionPhase.Running) return;
        waveController.Stop();
        FinishSession("전투 종료");
    }

    /// <summary>Cysharp UniTask로 이전 판의 비동기 작업 종료 후 새 판을 시작합니다.</summary>
    public void RequestRestart() => RestartAsync().SuppressCancellationThrow().Forget(Debug.LogException);

    /// <summary>씬을 재로드하지 않고 런타임 모델/풀/구독을 정리 후 다시 조립합니다. UniTask 사용.</summary>
    public async UniTask RestartAsync()
    {
        if (destroying || !initialized || SessionState != SessionPhase.Result) return;
        SessionState = SessionPhase.Restarting;
        SessionStateChanged?.Invoke();
        await waveRunTask;
        await UniTask.WaitUntil(() => !heroSummon.IsBusy && !(heroSynthesis?.IsBusy ?? false),
            cancellationToken: destroyCancellationToken);
        if (destroying) return;
        DisposeServices();
        // 이전 풀의 지연 Destroy가 새 전투와 같은 프레임에 섞이지 않게 한 프레임 분리합니다.
        await UniTask.Yield(PlayerLoopTiming.Update, destroyCancellationToken);
        Initialize();
        StartWaves();
    }

    /// <summary>
    /// UI 등에서 호출하는 영웅 소환 진입점입니다.
    /// Cysharp UniTask 기반 Addressables 로드를 내부에서 완료합니다.
    /// </summary>
    public void RequestHeroSummon()
    {
        if (!initialized)
        {
            return;
        }

        SummonHeroAsync(lifetimeCancellation.Token).Forget();
    }

    /// <summary>
    /// 영웅 한 기물을 소환하고 결과를 반환합니다.
    /// Cysharp UniTask를 사용해 Addressables 비동기 로드를 기다립니다.
    /// </summary>
    public async UniTask<HeroSummonService.SummonResult> SummonHeroAsync(
        CancellationToken cancellationToken = default)
    {
        if (!initialized)
        {
            throw new InvalidOperationException("BattleGameController is not initialized.");
        }

        HeroSummonService.SummonResult result =
            await heroSummon.SummonAsync(cancellationToken);
        SummonCompleted?.Invoke(result);
        return result;
    }

    /// <summary>Cysharp UniTask로 웨이브를 실행하고 UniTaskCompletionSource에 정상/취소/오류 종료 완료를 알립니다.</summary>
    private async UniTask RunWavesAsync(WaveController run, CancellationToken cancellationToken, UniTaskCompletionSource completion)
    {
        try
        {
            await run.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 씬 종료에 따른 정상 취소입니다.
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (!destroying && initialized) FinishSession("전투 오류 · 다시 시작할 수 있습니다");
        }
        finally { completion.TrySetResult(); }
    }

    private void HandleBattleDefeated(WaveController.DefeatReason reason) => FinishSession(
        reason == WaveController.DefeatReason.EnemyLimitReached ? "패배 · 적 100마리 누적" : "패배 · 보스 제한 시간 초과");

    private void HandleBattleEnded() => FinishSession("승리 · 5웨이브 클리어");

    private void FinishSession(string result)
    {
        if (!initialized || SessionState != SessionPhase.Running) return;
        SessionState = SessionPhase.Result;
        SessionResult = result;
        heroSummon.EndBattle();
        heroSynthesis?.EndBattle();
        if (heroDrag != null) heroDrag.SetInteractionEnabled(false);
        heroSelection?.Select(null);
        foreach (var pair in heroPopulation.RegisteredHeroes)
            if (pair.Key != null) pair.Key.SetBattleActive(false);
        enemySpawner.ReleaseAll(EnemyRemovalReason.GameEnded);
        synthesisPresenter?.Refresh();
        SessionStateChanged?.Invoke();
    }

    private void PrewarmEnemyPools()
    {
        HashSet<EnemyData> uniqueEnemies = new();

        for (int waveIndex = 0; waveIndex < waveSequence.WaveCount; waveIndex++)
        {
            WaveData wave = waveSequence.GetWave(waveIndex);
            for (int groupIndex = 0; groupIndex < wave.SpawnGroupCount; groupIndex++)
            {
                uniqueEnemies.Add(wave.GetSpawnGroup(groupIndex).EnemyData);
            }
        }

        enemyPool.Prewarm(uniqueEnemies);
    }

    private void HandleEnemySpawned(EnemyActor enemy)
    {
        if (enemy == null || enemy.Data == null || !enemy.Data.IsBoss)
        {
            return;
        }

        ClearCurrentBoss();
        currentBoss = enemy;
        currentBossData = enemy.Data;
        currentBoss.Character.HealthChanged += HandleBossHealthChanged;
        PublishBossHealth(true);
    }

    private void HandleEnemyRemoved(EnemyData enemyData, EnemyRemovalReason reason)
    {
        if (currentBoss != null && currentBossData == enemyData)
        {
            ClearCurrentBoss();
        }
    }

    private void HandleBossHealthChanged(Character character)
    {
        PublishBossHealth(true);
    }

    private void PublishBossHealth(bool visible)
    {
        if (!visible || currentBoss == null || currentBoss.Data == null)
        {
            BossHealthChanged?.Invoke(0f, 0f, false);
            return;
        }

        BossHealthChanged?.Invoke(
            currentBoss.Character.CurrentHealth,
            currentBoss.Data.MaxHealth,
            true);
    }

    private void ClearCurrentBoss()
    {
        if (currentBoss != null)
        {
            currentBoss.Character.HealthChanged -= HandleBossHealthChanged;
            currentBoss = null;
            currentBossData = null;
        }

        PublishBossHealth(false);
    }

    private void ValidateConfiguration()
    {
        if (battleRules == null || heroSummonTable == null || waveSequence == null)
        {
            throw new InvalidOperationException(
                "Battle rules, summon table and wave sequence must be assigned.");
        }

        if (!heroSummonTable.IsValid || !waveSequence.IsValid)
        {
            throw new InvalidOperationException("Summon table or wave sequence is invalid.");
        }

        if (loopPath == null ||
            loopPath.WaypointCount < 2 ||
            placementBoard == null ||
            heroStagingArea == null ||
            heroStagingArea.Capacity != 8)
        {
            throw new InvalidOperationException(
                "LoopPath, HeroPlacementBoard and an 8-capacity HeroSummonStagingArea are required.");
        }
    }

    private void OnDestroy()
    {
        destroying = true;
        sessionView?.Unbind();
        DisposeServices();
        SummonCompleted = null;
        BossHealthChanged = null;
        SessionStateChanged = null;
    }

    private void DisposeServices()
    {
        if (!initialized)
        {
            lifetimeCancellation?.Dispose();
            return;
        }

        initialized = false;
        heroSummon.EndBattle();
        heroSynthesis?.EndBattle();
        if (heroDrag != null) heroDrag.SetInteractionEnabled(false);
        lifetimeCancellation.Cancel();
        ClearCurrentBoss();
        hudView?.Unbind();
        synthesisPresenter?.Dispose();
        if (heroSynthesis != null) heroSynthesis.BusyChanged -= HandleSynthesisBusy;
        heroSynthesis?.Dispose();
        if (heroDrag != null && heroSelection != null) heroDrag.SelectionRequested -= heroSelection.Select;
        heroSelection?.Dispose();
        enemySpawner.EnemySpawned -= HandleEnemySpawned;
        enemySpawner.EnemyRemoved -= HandleEnemyRemoved;

        waveController.Defeated -= HandleBattleDefeated;
        waveController.SequenceCompleted -= HandleBattleEnded;
        waveController.Dispose();
        enemyReward.Dispose();
        enemySpawner.Dispose();
        heroSummon.Dispose();
        heroPool.Dispose();
        enemyPool.Dispose();
        heroPopulation.Dispose();
        enemyPopulation.Dispose();
        mineralWallet.Dispose();
        lifetimeCancellation.Dispose();

        lifetimeCancellation = null;
        initialized = false;
    }
}
