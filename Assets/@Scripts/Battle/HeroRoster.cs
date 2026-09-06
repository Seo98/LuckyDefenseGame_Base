using System.Collections.Generic;

/// <summary>벤치와 필드에 등록된 영웅을 통합 조회합니다. 태생/진화 경로는 정확한 HeroData 참조로 구분합니다.</summary>
public sealed class HeroRoster
{
    private readonly HeroPopulationTracker population;
    /// <summary>기존 인구 등록을 단일 보유 목록으로 사용합니다.</summary>
    public HeroRoster(HeroPopulationTracker population) => this.population = population;
    /// <summary>드래그/합성 예약되지 않은 동일 데이터 영웅 수입니다.</summary>
    public int CountAvailable(HeroData data)
    {
        int count = 0;
        foreach (var pair in population.RegisteredHeroes)
            if (IsAvailable(pair.Key, data)) count++;
        return count;
    }
    /// <summary>선택 영웅을 우선 포함한 뒤, 부족한 수량을 벤치/필드 통합 목록에서 수집합니다.</summary>
    public bool Collect(HeroData data, int count, HeroActor selected, List<HeroActor> output)
    {
        if (IsAvailable(selected, data)) { output.Add(selected); count--; }
        foreach (var pair in population.RegisteredHeroes)
        {
            if (count <= 0) break;
            if (pair.Key != selected && IsAvailable(pair.Key, data)) { output.Add(pair.Key); count--; }
        }
        return count == 0;
    }
    /// <summary>실제로 보유하며 사용 가능한 재료인지 검사합니다.</summary>
    public bool IsAvailable(HeroActor hero, HeroData data) => hero != null && hero.Data == data &&
        hero.IsSpawned && !hero.IsDragging && !hero.IsSynthesisReserved && !hero.Character.IsDead &&
        population.RegisteredHeroes.ContainsKey(hero);
}
