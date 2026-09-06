using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "HeroData", menuName = "Defense/Characters/Hero Data")]
public sealed class HeroData : CharacterData
{
    [SerializeField]
    private HeroGrade grade = HeroGrade.Normal;

    [SerializeField] private bool hasBirthGradeOverride;
    [SerializeField] private HeroGrade birthGrade = HeroGrade.Normal;

    /// <summary>진화해도 유지되는 태생 등급입니다. 기존 데이터는 현재 등급을 사용합니다.</summary>
    public HeroGrade BirthGrade => hasBirthGradeOverride ? birthGrade : grade;

    /// <summary>
    /// Unity Addressables의 AssetReferenceGameObject를 사용해 영웅 프리팹을 GUID로 참조합니다.
    /// </summary>
    [SerializeField]
    private AssetReferenceGameObject prefabReference;

    [SerializeField, Min(0)]
    private int poolPrewarmCount = 3;

    [SerializeField, Min(0f)]
    private float attackDamage = 10f;

    [SerializeField, Min(0.01f)]
    private float attackInterval = 1f;

    [SerializeField]
    private Vector2 attackRange = new(5f, 3f);

    [SerializeField, Min(0)]
    private int populationCost = 1;

    [SerializeField, Min(0)] private int sellPrice = 5;
    /// <summary>판매 시 지급할 미네랄입니다. 등급/태생별 밸런스는 SO에서 설정합니다.</summary>
    public int SellPrice => Mathf.Max(0, sellPrice);

    /// <summary>
    /// 영웅 기물의 기본 등급 또는 합성 결과 등급입니다.
    /// </summary>
    public HeroGrade Grade => grade;

    /// <summary>
    /// Addressables를 통해 비동기로 로드할 영웅 GameObject 프리팹 참조입니다.
    /// </summary>
    public AssetReferenceGameObject PrefabReference => prefabReference;

    /// <summary>
    /// 영웅 풀을 만들 때 미리 생성할 권장 개수입니다.
    /// </summary>
    public int PoolPrewarmCount => poolPrewarmCount;

    /// <summary>
    /// Addressable 프리팹 참조에 유효한 런타임 키가 설정되어 있는지 확인합니다.
    /// </summary>
    public bool HasValidPrefabReference =>
        prefabReference != null && prefabReference.RuntimeKeyIsValid();

    /// <summary>
    /// 한 번의 기본 공격으로 가하는 피해량입니다.
    /// </summary>
    public float AttackDamage => attackDamage;

    /// <summary>
    /// 기본 공격 사이의 시간(초)입니다.
    /// </summary>
    public float AttackInterval => attackInterval;

    /// <summary>
    /// 타원형 공격 범위의 X축 및 Z축 반지름입니다.
    /// </summary>
    public Vector2 AttackRange => attackRange;

    /// <summary>
    /// 전장에 배치할 때 차지하는 인구수입니다.
    /// </summary>
    public int PopulationCost => populationCost;

    private void OnValidate()
    {
        attackRange.x = Mathf.Max(0f, attackRange.x);
        attackRange.y = Mathf.Max(0f, attackRange.y);
    }
}
