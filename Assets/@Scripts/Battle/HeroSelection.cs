using System;

/// <summary>선택 상태와 풀 반환/파괴에 따른 자동 해제를 관리합니다. 입력/UI 구현과 독립적입니다.</summary>
public sealed class HeroSelection : IDisposable
{
    /// <summary>현재 선택된 살아 있는 영웅입니다.</summary>
    public HeroActor Selected { get; private set; }
    /// <summary>선택 변경 이벤트입니다.</summary>
    public event Action<HeroActor> Changed;
    /// <summary>선택 또는 빈 공간 클릭에 따른 해제를 적용합니다.</summary>
    public void Select(HeroActor hero)
    {
        if (hero != null && !hero.IsSpawned) hero = null;
        if (ReferenceEquals(Selected, hero)) return;
        if (!ReferenceEquals(Selected, null))
        {
            Selected.Despawned -= HandleRemoved;
            Selected.Destroyed -= HandleRemoved;
            if (Selected != null && !Selected.IsBeingDestroyed) Selected.GetComponent<HeroSelectionVisual>()?.SetSelected(false);
        }
        Selected = hero;
        if (hero != null)
        {
            hero.Despawned += HandleRemoved;
            hero.Destroyed += HandleRemoved;
            hero.GetComponent<HeroSelectionVisual>()?.SetSelected(true);
        }
        Changed?.Invoke(hero);
    }
    private void HandleRemoved(HeroActor _) => Select(null);
    /// <summary>선택과 이벤트 구독을 정리합니다.</summary>
    public void Dispose() { Select(null); Changed = null; }
}
