using System;
using UnityEngine;

/// <summary>
/// 웨이브에서 같은 종류의 Enemy를 연속으로 스폰하기 위한 설정 묶음입니다.
/// WaveData 내부에 직렬화해 일반 몬스터와 보스를 동일한 흐름으로 구성할 수 있습니다.
/// </summary>
[Serializable]
public sealed class EnemySpawnGroup
{
    [SerializeField]
    private EnemyData enemyData;

    [SerializeField, Min(1)]
    private int spawnCount = 1;

    [SerializeField, Min(0f)]
    private float startDelay;

    [SerializeField, Min(0f)]
    private float spawnInterval = 1f;

    [SerializeField, Min(0)]
    private int spawnWaypointIndex;

    /// <summary>
    /// 이 그룹에서 생성할 Enemy 정의입니다.
    /// </summary>
    public EnemyData EnemyData => enemyData;

    /// <summary>
    /// 이 그룹에서 생성할 총 Enemy 수입니다.
    /// </summary>
    public int SpawnCount => spawnCount;

    /// <summary>
    /// 그룹 실행 후 첫 Enemy를 생성하기 전까지 기다릴 시간(초)입니다.
    /// </summary>
    public float StartDelay => startDelay;

    /// <summary>
    /// 같은 그룹 내 Enemy 사이의 생성 간격(초)입니다.
    /// </summary>
    public float SpawnInterval => spawnInterval;

    /// <summary>
    /// Enemy가 출발할 LoopPath 웨이포인트 인덱스입니다.
    /// </summary>
    public int SpawnWaypointIndex => spawnWaypointIndex;

    /// <summary>
    /// 런타임에서 실행할 수 있는 최소 설정을 갖췄는지 반환합니다.
    /// </summary>
    public bool IsValid => enemyData != null && spawnCount > 0;
}
