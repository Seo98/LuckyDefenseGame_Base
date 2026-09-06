/// <summary>
/// Enemy가 전장에서 제거되는 원인을 나타냅니다.
/// 제거 원인에 따라 보상 지급 여부와 후처리를 구분할 수 있습니다.
/// </summary>
public enum EnemyRemovalReason
{
    /// <summary>체력이 모두 소진되어 처치되었습니다.</summary>
    Killed,

    /// <summary>게임 종료 또는 전투 정리로 제거되었습니다.</summary>
    GameEnded
}
