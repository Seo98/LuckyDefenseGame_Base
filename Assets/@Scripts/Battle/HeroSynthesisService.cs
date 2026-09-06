using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>재료 예약 → Addressables 결과 로드 → 가역적인 등록 교체 → 재료 소비를 담당합니다.</summary>
public sealed class HeroSynthesisService : IDisposable
{
    private readonly HeroPoolService pool;
    private readonly HeroSummonService summon;
    private readonly HeroPopulationTracker population;
    private readonly HeroSummonStagingArea bench;
    private readonly HeroPlacementBoard board;
    private readonly HeroRoster roster;
    private readonly SynthesisItemInventory inventory;
    private readonly Func<bool> canOperate;
    private readonly CancellationTokenSource lifetime = new();
    private bool ended;

    private readonly struct Origin
    {
        public readonly HeroActor Hero;
        public readonly HeroData Data;
        public readonly Vector3 Position;
        public readonly int Slot;
        public Origin(HeroActor hero, int slot)
        { Hero = hero; Data = hero.Data; Position = hero.transform.position; Slot = slot; }
    }

    /// <summary>합성 완료 결과입니다. 실패 시 원인을 UI에 표시합니다.</summary>
    public readonly struct Result
    {
        public readonly HeroActor Hero;
        public readonly string Message;
        public bool Success => Hero != null;
        public Result(HeroActor hero, string message) { Hero = hero; Message = message; }
    }

    /// <summary>합성 진행 중에는 결과 선택/다른 조작을 잠급니다.</summary>
    public bool IsBusy { get; private set; }
    /// <summary>합성 잠금 변경입니다. 드래그 입력도 이 이벤트로 중단합니다.</summary>
    public event Action<bool> BusyChanged;

    /// <summary>Unity 컴포넌트와 기존 런타임 서비스를 수동 주입합니다.</summary>
    public HeroSynthesisService(HeroPoolService pool, HeroSummonService summon,
        HeroPopulationTracker population, HeroSummonStagingArea bench, HeroPlacementBoard board,
        HeroRoster roster, SynthesisItemInventory inventory, Func<bool> canOperate)
    {
        this.pool = pool; this.summon = summon; this.population = population;
        this.bench = bench; this.board = board; this.roster = roster;
        this.inventory = inventory; this.canOperate = canOperate;
    }

    /// <summary>UI와 실행에서 같은 합산 규칙을 사용합니다. 잘못된 SO는 거부합니다.</summary>
    public static bool GetRequirements(HeroRecipeData recipe, Dictionary<HeroData, int> heroes,
        Dictionary<SynthesisItemData, int> items)
    {
        heroes.Clear(); items.Clear();
        if (recipe == null || !recipe.IsValid) return false;
        try
        {
            foreach (var entry in recipe.Heroes)
                heroes[entry.Hero] = checked((heroes.TryGetValue(entry.Hero, out int count) ? count : 0) + entry.Count);
            foreach (var entry in recipe.Items)
                items[entry.Item] = checked((items.TryGetValue(entry.Item, out int count) ? count : 0) + entry.Count);
        }
        catch (OverflowException) { return false; }
        return true;
    }

    /// <summary>Cysharp UniTask로 결과 프리팹을 로드합니다. 실패/취소는 재료와 아이템을 보존합니다.</summary>
    public async UniTask<Result> SynthesizeAsync(HeroRecipeData recipe, HeroActor selected,
        CancellationToken cancellationToken = default)
    {
        if (ended || !(canOperate?.Invoke() ?? true)) return new Result(null, "전투가 종료되었습니다.");
        if (IsBusy || summon.IsBusy) return new Result(null, "다른 소환/합성 처리 중입니다.");
        var heroNeeds = new Dictionary<HeroData, int>();
        var itemNeeds = new Dictionary<SynthesisItemData, int>();
        if (!GetRequirements(recipe, heroNeeds, itemNeeds) || selected == null ||
            !recipe.Uses(selected.Data) || !roster.IsAvailable(selected, selected.Data))
            return new Result(null, "선택 영웅 또는 레시피가 유효하지 않습니다.");

        var materials = new List<HeroActor>();
        foreach (var pair in heroNeeds)
            if (!roster.Collect(pair.Key, pair.Value, selected, materials)) return new Result(null, "영웅 재료가 부족합니다.");
        foreach (var pair in itemNeeds)
            if (inventory.Available(pair.Key) < pair.Value) return new Result(null, "아이템 재료가 부족합니다.");
        // 선택한 영웅을 대표 재료로 유지합니다.
        materials.Remove(selected); materials.Insert(0, selected);
        var origins = new List<Origin>(materials.Count);
        int removedPopulation = 0;
        foreach (var hero in materials)
        {
            int slot = bench.TryGetSlot(hero, out int index) ? index : -1;
            if (slot < 0 && !board.IsPlaced(hero)) return new Result(null, "재료 배치 상태가 유효하지 않습니다.");
            origins.Add(new Origin(hero, slot));
            removedPopulation += population.RegisteredHeroes[hero];
        }
        if (population.UsedPopulation - removedPopulation + recipe.Result.PopulationCost > population.MaximumPopulation)
            return new Result(null, "합성 결과의 인구수가 한도를 초과합니다.");
        if (!summon.TryBeginSynthesis()) return new Result(null, "지금은 합성할 수 없습니다.");

        IsBusy = true;
        bool itemsReserved = false, detached = false, committed = false;
        HeroActor result = null;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            foreach (var hero in materials) hero.SetSynthesisReserved(true);
            itemsReserved = inventory.TryReserve(itemNeeds);
            if (!itemsReserved) return new Result(null, "아이템 재료가 부족합니다.");
            BusyChanged?.Invoke(true);
            result = await pool.GetAsync(recipe.Result, linked.Token);
            CheckCancellation(linked.Token);
            foreach (var origin in origins)
                if (origin.Hero == null || !origin.Hero.IsSpawned || origin.Hero.Data != origin.Data ||
                    !origin.Hero.IsSynthesisReserved || !population.RegisteredHeroes.ContainsKey(origin.Hero) ||
                    (origin.Slot >= 0 ? !bench.Contains(origin.Hero) : !board.IsPlaced(origin.Hero)))
                    return new Result(null, "합성 도중 재료가 제거되어 취소했습니다.");

            // 여기부터 소비 완료까지 await가 없습니다. 실패하면 살아 있는 재료를 원래 칸으로 복구합니다.
            detached = true;
            foreach (var origin in origins)
            {
                bench.Release(origin.Hero);
                board.Release(origin.Hero, out _);
                population.Unregister(origin.Hero);
            }
            Origin destination = origins[0];
            foreach (var origin in origins) if (origin.Slot < 0) { destination = origin; break; }
            bool placed = destination.Slot >= 0
                ? bench.TryStageAt(result, destination.Slot)
                : board.TryPlaceAt(result, destination.Position);
            if (!placed && destination.Slot < 0) placed = bench.TryStage(result);
            if (!placed) return new Result(null, "결과를 놓을 공간이 없습니다. 재료를 복구했습니다.");
            if (!population.Register(result)) return new Result(null, "인구수 한도 초과로 재료를 복구했습니다.");
            CheckCancellation(linked.Token);
            // 배치/인구 등록이 끝났으므로 이제 재료를 소비합니다.
            committed = true;
            inventory.FinishReservation(itemNeeds, true);
            itemsReserved = false;
            foreach (var hero in materials) summon.ReleaseHero(hero);
            summon.TrackSynthesizedHero(result);
            return new Result(result, $"{recipe.Result.DisplayName} 합성 완료");
        }
        catch (OperationCanceledException) { return new Result(null, "합성이 취소되었습니다. 재료는 소비하지 않았습니다."); }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return committed ? new Result(result, "합성 완료") : new Result(null, "결과 로드/배치 실패. 재료를 보존했습니다.");
        }
        finally
        {
            if (!committed)
            {
                if (result != null) summon.ReleaseHero(result);
                if (detached)
                    foreach (var origin in origins)
                    {
                        if (origin.Hero == null || !origin.Hero.IsSpawned || !pool.IsRented(origin.Hero)) continue;
                        bool restored = origin.Slot >= 0 ? bench.TryStageAt(origin.Hero, origin.Slot) : board.TryPlaceAt(origin.Hero, origin.Position);
                        if (!restored || !population.Register(origin.Hero)) Debug.LogError("합성 재료 복구 실패: " + origin.Hero.name);
                    }
            }
            if (itemsReserved) inventory.FinishReservation(itemNeeds, false);
            foreach (var hero in materials) if (hero != null && hero.IsSpawned) hero.SetSynthesisReserved(false);
            IsBusy = false;
            summon.EndSynthesis();
            BusyChanged?.Invoke(false);
        }
    }

    private void CheckCancellation(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (ended || !(canOperate?.Invoke() ?? true)) throw new OperationCanceledException();
    }
    /// <summary>전투 종료 시 진행 중인 UniTask 로드를 취소하고 신규 합성을 차단합니다.</summary>
    public void EndBattle() { if (ended) return; ended = true; lifetime.Cancel(); }
    /// <summary>합성을 취소하고 수명 토큰을 해제합니다.</summary>
    public void Dispose() { EndBattle(); BusyChanged = null; lifetime.Dispose(); }
}
