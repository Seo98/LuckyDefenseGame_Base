/// <summary>
/// 영웅 소환 시 먼저 추첨하는 확률 구간입니다.
/// 실제 HeroGrade와 분리되어 + 등급을 같은 구간 후보로 포함할 수 있습니다.
/// </summary>
public enum HeroSummonTier
{
    /// <summary>
    /// 일반 등급 후보 구간입니다.
    /// </summary>
    Normal,

    /// <summary>
    /// 정예 등급 후보 구간입니다.
    /// </summary>
    Elite,

    /// <summary>
    /// 희귀 및 희귀+ 후보 구간입니다.
    /// </summary>
    Rare,

    /// <summary>
    /// 영웅 및 영웅+ 후보 구간입니다.
    /// </summary>
    Hero,

    /// <summary>
    /// 전설 및 전설+ 후보 구간입니다.
    /// </summary>
    Legendary
}
