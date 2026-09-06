using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>정확한 영웅 데이터/아이템과 수량으로 합성 조건을 정의합니다. 태생이 다른 기물은 다른 SO입니다.</summary>
[CreateAssetMenu(fileName = "HeroRecipe", menuName = "Defense/Synthesis/Recipe")]
public sealed class HeroRecipeData : ScriptableObject
{
    [Serializable]
    public struct HeroIngredient
    {
        [SerializeField] private HeroData hero;
        [SerializeField, Min(1)] private int count;
        /// <summary>재료 영웅의 정확한 데이터 참조입니다.</summary>
        public HeroData Hero => hero;
        /// <summary>필요한 재료 수입니다.</summary>
        public int Count => count;
    }

    [Serializable]
    public struct ItemIngredient
    {
        [SerializeField] private SynthesisItemData item;
        [SerializeField, Min(1)] private int count;
        /// <summary>필요한 진화 트리거입니다.</summary>
        public SynthesisItemData Item => item;
        /// <summary>필요한 아이템 수입니다.</summary>
        public int Count => count;
    }

    [SerializeField] private HeroData result;
    [SerializeField] private HeroIngredient[] heroes = Array.Empty<HeroIngredient>();
    [SerializeField] private ItemIngredient[] items = Array.Empty<ItemIngredient>();
    /// <summary>합성 결과 영웅입니다.</summary>
    public HeroData Result => result;
    /// <summary>영웅 재료 목록입니다. 중복 항목의 수량도 합산됩니다.</summary>
    public IReadOnlyList<HeroIngredient> Heroes => heroes;
    /// <summary>아이템 재료 목록입니다.</summary>
    public IReadOnlyList<ItemIngredient> Items => items;

    /// <summary>잘못된 수량, 일반 기물의 진화, 빈 결과를 거부합니다.</summary>
    public bool IsValid
    {
        get
        {
            if (result == null || !result.HasValidPrefabReference || heroes == null || heroes.Length == 0 || items == null) return false;
            foreach (var entry in heroes)
                if (entry.Hero == null || entry.Count <= 0 || entry.Hero.BirthGrade == HeroGrade.Normal) return false;
            foreach (var entry in items) if (entry.Item == null || entry.Count <= 0) return false;
            return true;
        }
    }

    /// <summary>선택한 영웅을 재료로 사용하는 레시피인지 확인합니다.</summary>
    public bool Uses(HeroData hero)
    {
        if (heroes == null) return false;
        foreach (var entry in heroes) if (entry.Hero == hero) return true;
        return false;
    }
}
