using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>uGUI/TMP 표시와 버튼 이벤트만 담당합니다. 레시피 판단과 소비는 Presenter/Service에 있습니다.</summary>
public sealed class HeroSynthesisView : MonoBehaviour
{
    [SerializeField] private GameObject selectionPanel;
    [SerializeField] private GameObject synthesisPanel;
    [SerializeField] private TMP_Text heroInfo;
    [SerializeField] private TMP_Text materials;
    [SerializeField] private TMP_Text resultPreview;
    [SerializeField] private TMP_Text message;
    [SerializeField] private TMP_Text itemSummary;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button synthesizeButton;
    [SerializeField] private Button itemSummonButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private RectTransform recipeContent;
    [SerializeField] private Button recipeButtonTemplate;
    private readonly List<Button> recipeButtons = new();

    /// <summary>합성 목록 열기 요청입니다.</summary>
    public event Action OpenRequested;
    /// <summary>합성 실행 요청입니다.</summary>
    public event Action SynthesizeRequested;
    /// <summary>아이템 소환 요청입니다.</summary>
    public event Action ItemSummonRequested;
    /// <summary>선택 영웅 판매 요청입니다.</summary>
    public event Action SellRequested;
    /// <summary>결과 후보 선택 요청입니다.</summary>
    public event Action<int> RecipeSelected;

    private void Awake()
    {
        openButton.onClick.AddListener(Open);
        closeButton.onClick.AddListener(Close);
        synthesizeButton.onClick.AddListener(Synthesize);
        itemSummonButton.onClick.AddListener(SummonItem);
        if (sellButton != null) sellButton.onClick.AddListener(Sell);
        selectionPanel.SetActive(false);
        synthesisPanel.SetActive(false);
    }
    private void Open() => OpenRequested?.Invoke();
    private void Close() => synthesisPanel.SetActive(false);
    private void Synthesize() => SynthesizeRequested?.Invoke();
    private void SummonItem() => ItemSummonRequested?.Invoke();
    private void Sell() => SellRequested?.Invoke();

    /// <summary>현재 영웅의 판매가와 판매 허용 여부를 표시합니다.</summary>
    public void ShowSell(HeroData data, bool canSell)
    {
        if (sellButton == null) return;
        sellButton.interactable = canSell && data != null;
        sellButton.GetComponentInChildren<TMP_Text>().text = data != null ? $"판매 · +{data.SellPrice} 미네랄" : "판매";
    }

    /// <summary>선택 영웅의 정보와 합성 진입 가능 여부를 표시합니다.</summary>
    public void ShowHero(HeroData data, bool hasRecipes)
    {
        selectionPanel.SetActive(data != null);
        heroInfo.text = HeroPresentation.Describe(data);
        openButton.interactable = hasRecipes;
        if (data == null) synthesisPanel.SetActive(false);
    }
    /// <summary>합성 목록을 표시합니다.</summary>
    public void OpenRecipes() => synthesisPanel.SetActive(true);
    /// <summary>레시피 버튼을 재사용하여 데이터 수만큼 표시합니다.</summary>
    public void ShowRecipes(IReadOnlyList<HeroRecipeData> recipes, int selectedIndex)
    {
        while (recipeButtons.Count < recipes.Count)
        {
            int index = recipeButtons.Count;
            Button button = Instantiate(recipeButtonTemplate, recipeContent);
            button.onClick.AddListener(() => RecipeSelected?.Invoke(index));
            recipeButtons.Add(button);
        }
        for (int i = 0; i < recipeButtons.Count; i++)
        {
            Button button = recipeButtons[i];
            button.gameObject.SetActive(i < recipes.Count);
            if (i >= recipes.Count) continue;
            var data = recipes[i].Result;
            button.GetComponentInChildren<TMP_Text>().text =
                $"{(i == selectedIndex ? "▶ " : "")}{data.DisplayName}\n<size=80%>{HeroPresentation.GradeName(data.Grade)} · 인구 {data.PopulationCost}</size>";
        }
    }
    /// <summary>부족 재료와 결과 미리보기, 실행 버튼을 갱신합니다.</summary>
    public void ShowRecipeDetails(string ingredients, HeroData result, bool canSynthesize)
    {
        materials.text = ingredients;
        resultPreview.text = HeroPresentation.Describe(result);
        synthesizeButton.interactable = canSynthesize;
    }
    /// <summary>처리 결과/취소 이유를 표시합니다.</summary>
    public void ShowMessage(string value) => message.text = value;
    /// <summary>아이템 보유량과 소환 가능 여부를 표시합니다.</summary>
    public void ShowItems(string value, int cost, bool canBuy)
    {
        itemSummary.text = value;
        itemSummonButton.GetComponentInChildren<TMP_Text>().text = $"아이템 소환 · {cost}";
        itemSummonButton.interactable = canBuy;
    }
}
