using System;
using UnityEngine;

/// <summary>
/// 한 스테이지에서 실행할 WaveData의 순서를 정의하는 게임 데이터입니다.
/// 실제 웨이브 실행과 상태 관리는 WaveController가 담당합니다.
/// </summary>
[CreateAssetMenu(
    fileName = "WaveSequenceData",
    menuName = "Defense/Waves/Wave Sequence Data")]
public sealed class WaveSequenceData : ScriptableObject
{
    [SerializeField]
    private WaveData[] waves = Array.Empty<WaveData>();

    /// <summary>
    /// 등록된 전체 웨이브 수입니다.
    /// </summary>
    public int WaveCount => waves?.Length ?? 0;

    /// <summary>
    /// 지정한 순번의 웨이브 데이터를 반환합니다.
    /// </summary>
    public WaveData GetWave(int index)
    {
        if (waves == null)
        {
            throw new InvalidOperationException($"{name} has no wave collection.");
        }

        if ((uint)index >= (uint)waves.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return waves[index];
    }

    /// <summary>
    /// 모든 웨이브가 런타임에서 실행 가능한 설정인지 확인합니다.
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (waves == null || waves.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < waves.Length; i++)
            {
                if (waves[i] == null || !waves[i].IsValid)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
