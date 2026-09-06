/// <summary>
/// 영웅 기물의 기본 등급과 합성 결과 단계를 나타냅니다.
/// 소환 가능 여부와 소환 확률은 이 열거형이 아닌 별도 소환 테이블에서 결정합니다.
/// </summary>
public enum HeroGrade
{
    /// <summary>
    /// 일반 등급입니다.
    /// </summary>
    Normal,

    /// <summary>
    /// 정예 등급입니다.
    /// </summary>
    Elite,

    /// <summary>
    /// 희귀 등급입니다.
    /// </summary>
    Rare,

    /// <summary>
    /// 희귀+ 등급입니다.
    /// </summary>
    RarePlus,

    /// <summary>
    /// 영웅 등급입니다.
    /// </summary>
    Hero,

    /// <summary>
    /// 영웅+ 등급입니다.
    /// </summary>
    HeroPlus,

    /// <summary>
    /// 전설 등급입니다.
    /// </summary>
    Legendary,

    /// <summary>
    /// 전설+ 등급입니다.
    /// </summary>
    LegendaryPlus,

    /// <summary>
    /// 소환으로 등장하지 않고 합성으로만 획득하는 초월 등급입니다.
    /// </summary>
    Transcendent,

    /// <summary>
    /// 소환으로 등장하지 않고 합성으로만 획득하는 초월+ 등급입니다.
    /// </summary>
    TranscendentPlus
}
