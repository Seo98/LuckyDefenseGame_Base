using System;
using System.Collections.Generic;
using R3;

/// <summary>
/// 배치된 영웅과 사용 인구수를 추적하고 동적인 최대 인구수 보너스를 관리합니다.
/// </summary>
public sealed class HeroPopulationTracker : IDisposable
{
    // 등록 시 비용을 보관하여 Data 초기화 또는 Unity 객체 파괴 후에도 정확하게 반환합니다.
    private readonly Dictionary<HeroActor, int> activeHeroes = new();

    /// <summary>현재 등록된 영웅과 등록 당시 인구 비용입니다. 상태 검증에 사용합니다.</summary>
    public IReadOnlyDictionary<HeroActor, int> RegisteredHeroes => activeHeroes;

    /// <summary>
    /// R3의 ReactiveProperty를 사용해 현재 사용 인구수를 저장하고 변경을 발행합니다.
    /// </summary>
    private readonly ReactiveProperty<int> usedPopulation = new(0);

    /// <summary>
    /// R3의 ReactiveProperty를 사용해 현재 최대 인구수를 저장하고 변경을 발행합니다.
    /// </summary>
    private readonly ReactiveProperty<int> maximumPopulation;

    private readonly int baseMaximumPopulation;
    private int maximumPopulationBonus;
    private bool isDisposed;

    /// <summary>
    /// 현재 전장에 등록된 영웅 수입니다.
    /// </summary>
    public int ActiveHeroCount => activeHeroes.Count;

    /// <summary>
    /// 현재 사용 중인 영웅 인구수입니다.
    /// </summary>
    public int UsedPopulation => usedPopulation.Value;

    /// <summary>
    /// 현재 적용 중인 최대 영웅 인구수입니다.
    /// </summary>
    public int MaximumPopulation => maximumPopulation.Value;

    /// <summary>
    /// R3의 Observable을 사용해 사용 인구수 변경을 발행합니다.
    /// </summary>
    public Observable<int> UsedPopulationChanged => usedPopulation;

    /// <summary>
    /// R3의 Observable을 사용해 최대 인구수 변경을 발행합니다.
    /// </summary>
    public Observable<int> MaximumPopulationChanged => maximumPopulation;

    /// <summary>
    /// BattleRulesData의 기본 영웅 인구수로 추적기를 생성합니다.
    /// </summary>
    public HeroPopulationTracker(BattleRulesData battleRules)
    {
        if (battleRules == null)
        {
            throw new ArgumentNullException(nameof(battleRules));
        }

        baseMaximumPopulation = Math.Max(1, battleRules.BaseHeroPopulation);
        maximumPopulation = new ReactiveProperty<int>(baseMaximumPopulation);
    }

    /// <summary>
    /// 지정한 HeroData를 현재 인구수 내에서 추가할 수 있는지 확인합니다.
    /// </summary>
    public bool CanAdd(HeroData heroData)
    {
        ThrowIfDisposed();

        return heroData != null &&
               UsedPopulation + heroData.PopulationCost <= MaximumPopulation;
    }

    /// <summary>
    /// 스폰된 영웅을 등록하고 인구수를 반영합니다.
    /// 중복 또는 인구수 초과 시 false를 반환합니다.
    /// </summary>
    public bool Register(HeroActor hero)
    {
        ThrowIfDisposed();

        if (hero == null)
        {
            throw new ArgumentNullException(nameof(hero));
        }

        if (!hero.IsSpawned || hero.Data == null)
        {
            throw new InvalidOperationException(
                $"{hero.name} must be spawned before population registration.");
        }

        if (activeHeroes.ContainsKey(hero) || !CanAdd(hero.Data))
        {
            return false;
        }

        activeHeroes.Add(hero, hero.Data.PopulationCost);
        usedPopulation.Value += hero.Data.PopulationCost;
        return true;
    }

    /// <summary>
    /// 영웅 등록을 해제하고 해당 HeroData의 인구수를 반환합니다.
    /// </summary>
    public bool Unregister(HeroActor hero)
    {
        ThrowIfDisposed();

        if (ReferenceEquals(hero, null) || !activeHeroes.Remove(hero, out int populationCost))
        {
            return false;
        }

        usedPopulation.Value = Math.Max(0, UsedPopulation - populationCost);
        return true;
    }

    /// <summary>
    /// 전설 기물 효과 등으로 제공되는 최대 인구수 보너스 총량을 설정합니다.
    /// </summary>
    public void SetMaximumPopulationBonus(int bonus)
    {
        ThrowIfDisposed();
        maximumPopulationBonus = Math.Max(0, bonus);
        maximumPopulation.Value = baseMaximumPopulation + maximumPopulationBonus;
    }

    /// <summary>
    /// 등록 상태와 인구수 보너스를 전투 시작 상태로 초기화합니다.
    /// 실제 영웅의 풀 반환은 외부 시스템이 담당합니다.
    /// </summary>
    public void Reset()
    {
        ThrowIfDisposed();
        activeHeroes.Clear();
        maximumPopulationBonus = 0;
        usedPopulation.Value = 0;
        maximumPopulation.Value = baseMaximumPopulation;
    }

    /// <summary>
    /// R3 상태 스트림과 추적 데이터를 정리합니다.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        activeHeroes.Clear();
        usedPopulation.Dispose();
        maximumPopulation.Dispose();
        isDisposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(HeroPopulationTracker));
        }
    }
}
