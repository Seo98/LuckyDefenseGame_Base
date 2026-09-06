using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using R3;

/// <summary>선택 상태와 합성 데이터를 uGUI View에 투영합니다. 프레임별 탐색 없이 변경 이벤트로 갱신합니다.</summary>
public sealed class HeroSynthesisPresenter : IDisposable
{
    private readonly HeroSynthesisView view;
    private readonly HeroSelection selection;
    private readonly HeroRecipeCatalog catalog;
    private readonly HeroRoster roster;
    private readonly HeroSynthesisService synthesis;
    private readonly SynthesisItemInventory items;
    private readonly MineralWallet wallet;
    private readonly HeroPopulationTracker population;
    private readonly HeroSummonStagingArea bench;
    private readonly HeroPlacementBoard board;
    private readonly HeroSummonService summon;
    private readonly Func<bool> canOperate;
    private readonly List<HeroRecipeData> choices = new();
    private readonly Dictionary<HeroData, int> heroNeeds = new();
    private readonly Dictionary<SynthesisItemData, int> itemNeeds = new();
    /// <summary>R3 Observable 구독을 Presenter 수명에 맞춰 정리합니다.</summary>
    private readonly CompositeDisposable subscriptions = new();
    private int recipeIndex;
    private bool disposed;

    /// <summary>수동 조립된 모델/서비스/View를 연결합니다.</summary>
    public HeroSynthesisPresenter(HeroSynthesisView view, HeroSelection selection, HeroRecipeCatalog catalog,
        HeroRoster roster, HeroSynthesisService synthesis, SynthesisItemInventory items,
        MineralWallet wallet, HeroPopulationTracker population, HeroSummonStagingArea bench,
        HeroPlacementBoard board, HeroSummonService summon, Func<bool> canOperate)
    {
        this.view = view; this.selection = selection; this.catalog = catalog; this.roster = roster;
        this.synthesis = synthesis; this.items = items; this.wallet = wallet; this.population = population;
        this.bench = bench; this.board = board; this.summon = summon; this.canOperate = canOperate;
        selection.Changed += Selected;
        view.OpenRequested += Open;
        view.RecipeSelected += Choose;
        view.SynthesizeRequested += Synthesize;
        view.ItemSummonRequested += BuyItem;
        view.SellRequested += Sell;
        items.Changed += Refresh;
        bench.StagingChanged += Refresh;
        board.PlacementChanged += Refresh;
        summon.HeroSummoned += HeroAdded;
        synthesis.BusyChanged += BusyChanged;
        subscriptions.Add(wallet.BalanceChanged.Subscribe(_ => Refresh()));
        subscriptions.Add(population.MaximumPopulationChanged.Subscribe(_ => Refresh()));
        Selected(selection.Selected);
    }

    private void Selected(HeroActor hero)
    {
        choices.Clear(); recipeIndex = 0;
        if (hero != null)
            foreach (var recipe in catalog.Recipes)
                if (recipe != null && recipe.IsValid && recipe.Uses(hero.Data)) choices.Add(recipe);
        view.ShowHero(hero != null ? hero.Data : null, choices.Count > 0);
        if (hero != null && choices.Count == 0) view.ShowMessage("이 기물은 등록된 진화가 없습니다.");
        Refresh();
    }
    private void Open() { Refresh(); view.OpenRecipes(); }
    private void Choose(int index) { if (!synthesis.IsBusy) { recipeIndex = index; Refresh(); } }
    private void HeroAdded(HeroActor _) => Refresh();
    private void BusyChanged(bool _) => Refresh();
    private void Synthesize() => ExecuteAsync().Forget();

    /// <summary>Cysharp UniTask 합성을 관찰하고 결과 영웅으로 선택을 넘깁니다.</summary>
    private async UniTask ExecuteAsync()
    {
        if (recipeIndex < 0 || recipeIndex >= choices.Count || synthesis.IsBusy) return;
        view.ShowMessage("재료 예약 · 결과 준비 중...");
        HeroSynthesisService.Result result = await synthesis.SynthesizeAsync(choices[recipeIndex], selection.Selected);
        if (disposed || view == null) return;
        if (result.Success) selection.Select(result.Hero);
        view.ShowMessage(result.Message);
        Refresh();
    }

    private void BuyItem()
    {
        if (!canOperate() || summon.IsBusy || catalog.SummonItems.Count == 0) return;
        var item = catalog.SummonItems[UnityEngine.Random.Range(0, catalog.SummonItems.Count)];
        if (item == null || !wallet.TrySpend(catalog.ItemSummonCost)) { view.ShowMessage("미네랄이 부족합니다."); return; }
        items.Add(item, 1);
        view.ShowMessage($"{item.DisplayName} ×1 획득");
    }

    private void Sell()
    {
        if (disposed) return;
        string name = selection.Selected != null ? selection.Selected.Data.DisplayName : "영웅";
        bool sold = summon.TrySell(selection.Selected, out int refund);
        view.ShowMessage(sold ? $"{name} 판매 · +{refund} 미네랄" : "드래그/합성 중이거나 판매할 수 없는 영웅입니다.");
        Refresh();
    }

    /// <summary>전투 종료 등 외부 상태 변경에도 명시적으로 갱신할 수 있습니다.</summary>
    public void Refresh()
    {
        if (disposed || view == null) return;
        HeroActor selected = selection.Selected;
        view.ShowSell(selected != null ? selected.Data : null, canOperate() && !summon.IsBusy &&
            selected != null && !selected.IsDragging && !selected.IsSynthesisReserved);
        var itemText = new StringBuilder("진화 아이템  ");
        foreach (var item in catalog.SummonItems)
            if (item != null) itemText.Append($"{item.DisplayName} ×{items.Count(item)}  ");
        view.ShowItems(itemText.ToString(), catalog.ItemSummonCost,
            canOperate() && !summon.IsBusy && catalog.SummonItems.Count > 0 && wallet.CanSpend(catalog.ItemSummonCost));
        view.ShowRecipes(choices, recipeIndex);
        if (recipeIndex < 0 || recipeIndex >= choices.Count)
        { view.ShowRecipeDetails("선택 가능한 합성이 없습니다.", null, false); return; }
        var recipe = choices[recipeIndex];
        bool ready = HeroSynthesisService.GetRequirements(recipe, heroNeeds, itemNeeds);
        var text = new StringBuilder("<b>필요 재료 · 보유 / 필요</b>\n\n");
        int materialPopulation = 0;
        foreach (var pair in heroNeeds)
        {
            int have = roster.CountAvailable(pair.Key);
            AppendRequirement(text, pair.Key.DisplayName, have, pair.Value);
            ready &= have >= pair.Value;
            materialPopulation += pair.Key.PopulationCost * pair.Value;
        }
        foreach (var pair in itemNeeds)
        {
            int have = items.Available(pair.Key);
            AppendRequirement(text, pair.Key.DisplayName, have, pair.Value);
            ready &= have >= pair.Value;
        }
        int resultPopulation = population.UsedPopulation - materialPopulation + recipe.Result.PopulationCost;
        bool enoughPopulation = resultPopulation <= population.MaximumPopulation;
        text.Append($"\n재료 인구 {materialPopulation} → 결과 {recipe.Result.PopulationCost}");
        text.Append(ready ? $"\n합성 후 인구  {Math.Max(0, resultPopulation)} / {population.MaximumPopulation}" : "\n재료 확보 후 인구수 재검사");
        if (!enoughPopulation) text.Append("\n<color=#FF7474>인구수 한도 초과</color>");
        if (synthesis.IsBusy) text.Append("\n<color=#E7C779>합성 처리 중</color>");
        view.ShowRecipeDetails(text.ToString(), recipe.Result, ready && enoughPopulation &&
            canOperate() && !summon.IsBusy && selection.Selected != null && !selection.Selected.IsDragging);
    }

    private static void AppendRequirement(StringBuilder text, string name, int have, int need)
    {
        int missing = Math.Max(0, need - have);
        text.Append($"{name}  {have}/{need}\n");
        text.Append(missing > 0 ? $"<color=#FF7474>×{missing} 부족</color>\n\n" : "<color=#73E6A0>충족</color>\n\n");
    }

    /// <summary>R3 및 모든 UI/게임 이벤트 구독을 정리합니다.</summary>
    public void Dispose()
    {
        disposed = true;
        subscriptions.Dispose();
        selection.Changed -= Selected;
        view.OpenRequested -= Open; view.RecipeSelected -= Choose;
        view.SynthesizeRequested -= Synthesize; view.ItemSummonRequested -= BuyItem;
        view.SellRequested -= Sell;
        items.Changed -= Refresh; bench.StagingChanged -= Refresh; board.PlacementChanged -= Refresh;
        summon.HeroSummoned -= HeroAdded; synthesis.BusyChanged -= BusyChanged;
    }
}
