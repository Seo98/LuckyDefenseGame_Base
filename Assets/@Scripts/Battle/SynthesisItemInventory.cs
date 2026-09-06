using System;
using System.Collections.Generic;

/// <summary>아이템 보유량과 합성 예약량을 분리합니다. 예약 해제는 소비 없이 원상복구됩니다.</summary>
public sealed class SynthesisItemInventory
{
    private readonly Dictionary<SynthesisItemData, int> owned = new();
    private readonly Dictionary<SynthesisItemData, int> reserved = new();
    /// <summary>보유량 또는 예약량 변경 이벤트입니다.</summary>
    public event Action Changed;
    /// <summary>현재 보유량입니다.</summary>
    public int Count(SynthesisItemData item) => owned.TryGetValue(item, out int value) ? value : 0;
    /// <summary>다른 작업이 사용할 수 있는 미예약 수량입니다.</summary>
    public int Available(SynthesisItemData item) => Count(item) - (reserved.TryGetValue(item, out int value) ? value : 0);
    /// <summary>드롭이나 아이템 소환 결과를 지급합니다.</summary>
    public void Add(SynthesisItemData item, int count)
    {
        if (item == null || count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        owned[item] = checked(Count(item) + count);
        Changed?.Invoke();
    }
    /// <summary>모든 요구량이 충족될 때만 원자적으로 예약합니다.</summary>
    public bool TryReserve(IReadOnlyDictionary<SynthesisItemData, int> requirements)
    {
        foreach (var pair in requirements) if (pair.Key == null || pair.Value <= 0 || Available(pair.Key) < pair.Value) return false;
        foreach (var pair in requirements)
            reserved[pair.Key] = (reserved.TryGetValue(pair.Key, out int value) ? value : 0) + pair.Value;
        Changed?.Invoke();
        return true;
    }
    /// <summary>예약 수량을 소비하거나 취소합니다. 성공한 예약과 한 번씩 짝지어 호출합니다.</summary>
    public void FinishReservation(IReadOnlyDictionary<SynthesisItemData, int> requirements, bool consume)
    {
        foreach (var pair in requirements)
        {
            reserved[pair.Key] -= pair.Value;
            if (consume) owned[pair.Key] -= pair.Value;
        }
        Changed?.Invoke();
    }
}
