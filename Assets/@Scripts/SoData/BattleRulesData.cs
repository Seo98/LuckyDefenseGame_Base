using UnityEngine;

[CreateAssetMenu(fileName = "BattleRulesData", menuName = "Scriptable Objects/BattleRulesData")]
public sealed class BattleRulesData : ScriptableObject
{
    [SerializeField, Min(1)]
    private int maxActiveEnemyCount = 100;

    [SerializeField, Min(1)]
    private int baseHeroPopulation = 30;

    [SerializeField, Min(0)]
    private int startingMinerals = 100;

    [SerializeField, Min(0)]
    private int heroSummonCost = 10;

    /// <summary>
    /// 동시에 존재할 수 있는 최대 Enemy 수입니다.
    /// </summary>
    public int MaxActiveEnemyCount => maxActiveEnemyCount;

    /// <summary>
    /// 전투 시작 시 적용할 기본 영웅 인구수 한도입니다.
    /// </summary>
    public int BaseHeroPopulation => baseHeroPopulation;

    /// <summary>
    /// 전투 시작 시 지급할 미네랄입니다.
    /// </summary>
    public int StartingMinerals => startingMinerals;

    /// <summary>
    /// 영웅을 한 번 소환할 때 소비하는 미네랄입니다.
    /// </summary>
    public int HeroSummonCost => heroSummonCost;
}
