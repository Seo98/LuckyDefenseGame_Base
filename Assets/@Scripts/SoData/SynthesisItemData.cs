using UnityEngine;

/// <summary>장착 능력치가 아닌 진화 트리거로 사용하는 아이템 정의입니다.</summary>
[CreateAssetMenu(fileName = "SynthesisItem", menuName = "Defense/Synthesis/Item")]
public sealed class SynthesisItemData : ScriptableObject
{
    [SerializeField] private string displayName;
    /// <summary>UI에 표시할 아이템 이름입니다.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
}
