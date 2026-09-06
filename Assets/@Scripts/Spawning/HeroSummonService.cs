using System;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// 소환 테이블 추첨, 미네랄 소비, 인구수와 슬롯 배치를 하나의 안전한 흐름으로 처리합니다.
/// </summary>
public sealed class HeroSummonService : IDisposable
{
    /// <summary>
    /// 영웅 소환 시도 결과입니다.
    /// </summary>
    public enum SummonStatus
    {
        Success,
        InvalidTable,
        NotEnoughMinerals,
        PopulationLimit,
        NoPlacementSlot,
        StagingFull,
        Busy,
        BattleEnded
    }

    /// <summary>
    /// 소환 상태와 성공 시 생성된 영웅을 함께 반환합니다.
    /// </summary>
    public readonly struct SummonResult
    {
        /// <summary>
        /// 소환 처리 상태입니다.
        /// </summary>
        public SummonStatus Status { get; }

        /// <summary>
        /// 성공 시 배치된 영웅이며, 실패 시 null입니다.
        /// </summary>
        public HeroActor Hero { get; }

        /// <summary>
        /// 소환 성공 여부입니다.
        /// </summary>
        public bool IsSuccess => Status == SummonStatus.Success;

        internal SummonResult(SummonStatus status, HeroActor hero = null)
        {
            Status = status;
            Hero = hero;
        }
    }

    private readonly HeroSummonTableData summonTable;
    private readonly HeroPoolService poolService;
    private readonly HeroPopulationTracker populationTracker;
    private readonly HeroSummonStagingArea stagingArea;
    private readonly MineralWallet mineralWallet;
    private readonly Random random;
    private readonly int summonCost;
    private readonly HeroPlacementBoard placementBoard;
    private readonly Func<bool> canSummon;
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private bool isSummoning;
    private bool isSynthesizing;

    /// <summary>소환 또는 합성 트랜잭션이 진행 중인지 나타냅니다.</summary>
    public bool IsBusy => isSummoning || isSynthesizing;

    /// <summary>소환과 합성이 같은 풀/배치 등록을 동시에 변경하지 않도록 잠급니다.</summary>
    public bool TryBeginSynthesis()
    {
        if (IsBusy || !CanSummon()) return false;
        isSynthesizing = true;
        return true;
    }

    /// <summary>합성 잠금을 해제하고 최종 등록 일치를 검증합니다.</summary>
    public void EndSynthesis()
    {
        isSynthesizing = false;
        if (!isDisposed) ValidateState();
    }

    /// <summary>합성으로 만들어진 영웅에도 공통 사망/풀 반환 처리를 연결합니다.</summary>
    public void TrackSynthesizedHero(HeroActor hero)
    {
        hero.Died -= HandleHeroDied;
        hero.Died += HandleHeroDied;
        HeroSummoned?.Invoke(hero);
    }
    private bool battleEnded;
    private bool isDisposed;

    /// <summary>
    /// 영웅이 성공적으로 소환되어 대기열에 등록된 직후 발생합니다.
    /// </summary>
    public event Action<HeroActor> HeroSummoned;

    /// <summary>
    /// 소환에 필요한 데이터와 런타임 서비스를 연결합니다.
    /// </summary>
    public HeroSummonService(
        HeroSummonTableData summonTable,
        HeroPoolService poolService,
        HeroPopulationTracker populationTracker,
        HeroSummonStagingArea stagingArea,
        MineralWallet mineralWallet,
        BattleRulesData battleRules,
        HeroPlacementBoard placementBoard,
        Func<bool> canSummon = null,
        Random random = null)
    {
        this.summonTable = summonTable != null
            ? summonTable
            : throw new ArgumentNullException(nameof(summonTable));
        this.poolService = poolService ?? throw new ArgumentNullException(nameof(poolService));
        this.populationTracker = populationTracker ??
            throw new ArgumentNullException(nameof(populationTracker));
        this.stagingArea = stagingArea != null
            ? stagingArea
            : throw new ArgumentNullException(nameof(stagingArea));
        this.mineralWallet = mineralWallet ??
            throw new ArgumentNullException(nameof(mineralWallet));

        if (battleRules == null)
        {
            throw new ArgumentNullException(nameof(battleRules));
        }

        summonCost = Math.Max(0, battleRules.HeroSummonCost);
        this.random = random ?? new Random();
        this.placementBoard = placementBoard != null ? placementBoard :
            throw new ArgumentNullException(nameof(placementBoard));
        this.canSummon = canSummon;
        poolService.HeroReleasing += HandleHeroReleasing;
    }

    /// <summary>
    /// Cysharp UniTask를 사용해 Addressable 영웅을 로드하고 소환을 완료합니다.
    /// 실패 시 미네랄, 인구수, 슬롯 및 풀 상태를 원래대로 유지합니다.
    /// </summary>
    public async UniTask<SummonResult> SummonAsync(
        CancellationToken cancellationToken = default)
    {
        if (!CanSummon()) return new SummonResult(SummonStatus.BattleEnded);
        cancellationToken.ThrowIfCancellationRequested();
        if (IsBusy)
        {
            return new SummonResult(SummonStatus.Busy);
        }

        isSummoning = true;
        using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, lifetimeCancellation.Token);

        try
        {
            SummonResult result = await ExecuteSummonAsync(linkedCancellation.Token);
            if (result.IsSuccess) HeroSummoned?.Invoke(result.Hero);
            return result;
        }
        catch (OperationCanceledException) when (battleEnded)
        {
            return new SummonResult(SummonStatus.BattleEnded);
        }
        finally
        {
            isSummoning = false;
            if (!isDisposed) ValidateState();
        }
    }

    /// <summary>Cysharp UniTask 기반 로드 후 재검사하고 실패한 소환의 등록과 비용을 롤백합니다.</summary>
    private async UniTask<SummonResult> ExecuteSummonAsync(
        CancellationToken cancellationToken)
    {
        if (!summonTable.IsValid)
        {
            return new SummonResult(SummonStatus.InvalidTable);
        }

        if (!mineralWallet.CanSpend(summonCost))
        {
            return new SummonResult(SummonStatus.NotEnoughMinerals);
        }

        if (!stagingArea.HasSpace)
        {
            return new SummonResult(SummonStatus.StagingFull);
        }

        HeroData heroData = RollHeroData();
        if (!populationTracker.CanAdd(heroData))
        {
            return new SummonResult(SummonStatus.PopulationLimit);
        }

        HeroActor hero = await poolService.GetAsync(heroData, cancellationToken);
        bool committed = false;
        bool spent = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanSummon()) return new SummonResult(SummonStatus.BattleEnded);
            if (!stagingArea.TryStage(hero)) return new SummonResult(SummonStatus.StagingFull);
            if (!CanSummon()) return new SummonResult(SummonStatus.BattleEnded);
            if (!populationTracker.Register(hero)) return new SummonResult(SummonStatus.PopulationLimit);
            if (!CanSummon()) return new SummonResult(SummonStatus.BattleEnded);
            if (!mineralWallet.TrySpend(summonCost)) return new SummonResult(SummonStatus.NotEnoughMinerals);
            spent = true;
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanSummon()) return new SummonResult(SummonStatus.BattleEnded);
            hero.Died += HandleHeroDied;
            committed = true;
            return new SummonResult(SummonStatus.Success, hero);
        }
        finally
        {
            if (!committed)
            {
                poolService.Release(hero);
                if (spent) mineralWallet.Add(summonCost);
            }
        }
    }

    private bool CanSummon() => !battleEnded && !isDisposed && (canSummon?.Invoke() ?? true);

    /// <summary>이 전투의 소환을 영구 차단하고 진행 중인 UniTask 소환 로드를 취소합니다.</summary>
    public void EndBattle()
    {
        if (battleEnded) return;
        battleEnded = true;
        lifetimeCancellation.Cancel();
    }

    /// <summary>벤치/필드/인구 등록을 해제하고 영웅을 풀에 반환합니다. 중복 제거는 무시합니다.</summary>
    public bool ReleaseHero(HeroActor hero) => poolService.Release(hero);

    /// <summary>소유한 영웅을 한 번만 판매하고 벤치/필드/인구/풀 등록 정리 후 미네랄을 지급합니다.</summary>
    public bool TrySell(HeroActor hero, out int refund)
    {
        refund = 0;
        if (!CanSummon() || IsBusy || hero == null || !hero.IsSpawned || hero.IsBeingDestroyed ||
            hero.IsDragging || hero.IsSynthesisReserved || hero.Character.IsDead ||
            !poolService.IsRented(hero) || !populationTracker.RegisteredHeroes.ContainsKey(hero) ||
            !(stagingArea.Contains(hero) ^ placementBoard.IsPlaced(hero))) return false;
        int price = hero.Data.SellPrice;
        if (!ReleaseHero(hero)) return false;
        mineralWallet.Add(price);
        refund = price;
        return true;
    }

    private void HandleHeroDied(HeroActor hero) => ReleaseHero(hero);

    private void HandleHeroReleasing(HeroActor hero)
    {
        hero.Died -= HandleHeroDied;
        try
        {
            if (stagingArea != null) stagingArea.Release(hero);
        }
        finally
        {
            try
            {
                if (placementBoard != null)
                {
                    placementBoard.UnregisterExternalOccupant(hero);
                    placementBoard.Release(hero, out _);
                }
            }
            finally { populationTracker.Unregister(hero); }
        }
        if (!isSummoning && !isDisposed) ValidateState();
    }

    /// <summary>개발 빌드에서 소환/제거 후 풀·벤치·필드·인구 등록의 일치를 검사합니다.</summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public void ValidateState()
    {
        if (IsBusy) return;
        if (placementBoard == null || stagingArea == null) return;
        int cost = 0;
        bool valid = poolService.RentedCount == populationTracker.ActiveHeroCount &&
                     stagingArea.Count + placementBoard.OccupiedCount == populationTracker.ActiveHeroCount;
        foreach (var pair in populationTracker.RegisteredHeroes)
        {
            HeroActor hero = pair.Key;
            cost += pair.Value;
            valid &= hero != null && hero.IsSpawned && poolService.IsRented(hero) &&
                     (stagingArea.Contains(hero) ^ placementBoard.IsPlaced(hero));
        }
        if (!valid || cost != populationTracker.UsedPopulation)
            UnityEngine.Debug.LogError("Hero registrations disagree: pool / bench / field / population.");
    }

    /// <summary>소환을 종료하고 등록된 영웅을 반환한 뒤 이벤트와 취소 토큰을 해제합니다.</summary>
    public void Dispose()
    {
        if (isDisposed) return;
        EndBattle();
        isDisposed = true;
        while (populationTracker.ActiveHeroCount > 0)
        {
            using var enumerator = populationTracker.RegisteredHeroes.GetEnumerator();
            enumerator.MoveNext();
            HeroActor hero = enumerator.Current.Key;
            if (!ReleaseHero(hero)) HandleHeroReleasing(hero);
        }
        poolService.HeroReleasing -= HandleHeroReleasing;
        HeroSummoned = null;
        lifetimeCancellation.Dispose();
    }

    private HeroData RollHeroData()
    {
        HeroSummonTableData.TierEntry tier = RollTier();
        double totalWeight = 0d;

        for (int i = 0; i < tier.CandidateCount; i++)
        {
            totalWeight += tier.GetCandidate(i).Weight;
        }

        double roll = random.NextDouble() * totalWeight;
        for (int i = 0; i < tier.CandidateCount; i++)
        {
            HeroSummonTableData.Candidate candidate = tier.GetCandidate(i);
            roll -= candidate.Weight;

            if (roll <= 0d)
            {
                return candidate.HeroData;
            }
        }

        return tier.GetCandidate(tier.CandidateCount - 1).HeroData;
    }

    private HeroSummonTableData.TierEntry RollTier()
    {
        double totalWeight = 0d;

        for (int i = 0; i < summonTable.TierCount; i++)
        {
            totalWeight += summonTable.GetTier(i).Weight;
        }

        double roll = random.NextDouble() * totalWeight;
        for (int i = 0; i < summonTable.TierCount; i++)
        {
            HeroSummonTableData.TierEntry tier = summonTable.GetTier(i);
            roll -= tier.Weight;

            if (roll <= 0d)
            {
                return tier;
            }
        }

        return summonTable.GetTier(summonTable.TierCount - 1);
    }
}
