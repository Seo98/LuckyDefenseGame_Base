using UnityEngine;

[CreateAssetMenu(menuName = "Defense/Characters/Enemy Data")]
public sealed class EnemyData : CharacterData
{
    [SerializeField, Min(0f)] private float moveSpeed = 3f;
    [SerializeField, Min(0)] private int mineralReward = 1;
    [SerializeField] private bool isBoss;

    [SerializeField] private EnemyActor prefab;
    [SerializeField, Min(1)] private int poolPrewarmCount = 20;

    public EnemyActor Prefab => prefab;
    public int PoolPrewarmCount => poolPrewarmCount;

    public float MoveSpeed => moveSpeed;
    public int MineralReward => mineralReward;
    public bool IsBoss => isBoss;
}