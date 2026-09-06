using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>전투에서 사용할 합성 레시피와 아이템 추첨 후보입니다.</summary>
[CreateAssetMenu(fileName = "HeroRecipeCatalog", menuName = "Defense/Synthesis/Catalog")]
public sealed class HeroRecipeCatalog : ScriptableObject
{
    [SerializeField] private HeroRecipeData[] recipes = Array.Empty<HeroRecipeData>();
    [SerializeField] private SynthesisItemData[] summonItems = Array.Empty<SynthesisItemData>();
    [SerializeField, Min(0)] private int itemSummonCost = 200;
    /// <summary>활성 레시피입니다.</summary>
    public IReadOnlyList<HeroRecipeData> Recipes => recipes;
    /// <summary>동일 확률로 추첨하는 프로토타입 아이템 목록입니다.</summary>
    public IReadOnlyList<SynthesisItemData> SummonItems => summonItems;
    /// <summary>아이템 소환 미네랄 가격입니다.</summary>
    public int ItemSummonCost => itemSummonCost;
}
