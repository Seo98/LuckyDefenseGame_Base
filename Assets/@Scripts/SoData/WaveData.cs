using System;
using UnityEngine;

/// <summary>
/// 한 웨이브의 스폰 구성과 종료 조건을 정의하는 불변 게임 데이터입니다.
/// </summary>
[CreateAssetMenu(fileName = "WaveData", menuName = "Defense/Waves/Wave Data")]
public sealed class WaveData : ScriptableObject
{
    /// <summary>
    /// 웨이브 시간이 끝났을 때 적용할 진행 규칙입니다.
    /// </summary>
    public enum CompletionRule
    {
        /// <summary>
        /// 남은 Enemy 수와 관계없이 제한시간이 끝나면 다음 웨이브로 진행합니다.
        /// </summary>
        AdvanceWhenTimeExpires,

        /// <summary>
        /// 제한시간 안에 목표 보스를 처치해야 하며, 실패하면 게임에서 패배합니다.
        /// </summary>
        DefeatBossBeforeTimeExpires
    }

    [SerializeField]
    private string displayName;

    [SerializeField, Min(0.1f)]
    private float duration = 30f;

    [SerializeField]
    private CompletionRule completionRule = CompletionRule.AdvanceWhenTimeExpires;

    [SerializeField]
    private EnemyData bossTarget;

    [SerializeField]
    private EnemySpawnGroup[] spawnGroups = Array.Empty<EnemySpawnGroup>();

    /// <summary>
    /// UI 등에 표시할 웨이브 이름입니다.
    /// </summary>
    public string DisplayName => displayName;

    /// <summary>
    /// 일반 웨이브에서는 다음 웨이브까지의 시간이며,
    /// 보스 웨이브에서는 처치 제한시간입니다.
    /// </summary>
    public float Duration => duration;

    /// <summary>
    /// 이 웨이브의 진행 또는 패배 판정 규칙입니다.
    /// </summary>
    public CompletionRule Rule => completionRule;

    /// <summary>
    /// 보스 웨이브에서 처치를 추적할 Enemy 정의입니다.
    /// </summary>
    public EnemyData BossTarget => bossTarget;

    /// <summary>
    /// 이 웨이브에 등록된 스폰 그룹 수입니다.
    /// </summary>
    public int SpawnGroupCount => spawnGroups?.Length ?? 0;

    /// <summary>
    /// 지정한 순서의 스폰 그룹을 반환합니다.
    /// </summary>
    public EnemySpawnGroup GetSpawnGroup(int index)
    {
        if (spawnGroups == null)
        {
            throw new InvalidOperationException($"{name} has no spawn group collection.");
        }

        if ((uint)index >= (uint)spawnGroups.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return spawnGroups[index];
    }

    /// <summary>
    /// 런타임에서 실행할 수 있는 필수 설정을 갖췄는지 확인합니다.
    /// 보스 웨이브는 IsBoss가 설정된 목표 EnemyData가 필요합니다.
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (duration <= 0f || spawnGroups == null || spawnGroups.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < spawnGroups.Length; i++)
            {
                if (spawnGroups[i] == null || !spawnGroups[i].IsValid)
                {
                    return false;
                }
            }

            return completionRule != CompletionRule.DefeatBossBeforeTimeExpires ||
                   bossTarget != null && bossTarget.IsBoss;
        }
    }
}
