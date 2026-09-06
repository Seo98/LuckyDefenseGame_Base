using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>uGUI/TMP의 게임 시작, 종료, 결과, 다시 시작 화면입니다. 전투 생명주기는 Controller가 소유합니다.</summary>
public sealed class BattleSessionView : MonoBehaviour
{
    [SerializeField] private GameObject overlay;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text description;
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private Button actionButton;
    [SerializeField] private Button endButton;
    private BattleGameController controller;

    /// <summary>같은 씬에서 반복 초기화해도 버튼/상태 구독이 중복되지 않게 연결합니다.</summary>
    public void Bind(BattleGameController value)
    {
        Unbind(); controller = value;
        controller.SessionStateChanged += Render;
        actionButton.onClick.AddListener(Activate);
        endButton.onClick.AddListener(End);
        Render();
    }
    private void Activate()
    {
        if (controller.SessionState == BattleGameController.SessionPhase.Ready) controller.StartWaves();
        else if (controller.SessionState == BattleGameController.SessionPhase.Result) controller.RequestRestart();
    }
    private void End() => controller.EndSession();
    private void Render()
    {
        if (controller == null) return;
        var state = controller.SessionState;
        bool running = state == BattleGameController.SessionPhase.Running;
        overlay.SetActive(!running);
        endButton.gameObject.SetActive(running);
        actionButton.interactable = state != BattleGameController.SessionPhase.Restarting;
        title.text = state switch
        {
            BattleGameController.SessionPhase.Ready => "순환형 디펜스",
            BattleGameController.SessionPhase.Restarting => "새 전투 준비 중",
            _ => controller.SessionResult
        };
        description.text = state == BattleGameController.SessionPhase.Ready
            ? "영웅 소환 → 필드 배치 → 합성\n5웨이브 보스를 처치하세요.\n적 100마리 누적 또는 보스 시간 초과 시 패배합니다."
            : "다시 시작하면 미네랄·기물·아이템·웨이브가 초기화됩니다.";
        actionLabel.text = state == BattleGameController.SessionPhase.Ready ? "게임 시작" : "다시 시작";
    }
    /// <summary>전투/버튼 구독을 해제합니다.</summary>
    public void Unbind()
    {
        if (controller != null) controller.SessionStateChanged -= Render;
        if (actionButton != null) actionButton.onClick.RemoveListener(Activate);
        if (endButton != null) endButton.onClick.RemoveListener(End);
        controller = null;
    }
    private void OnDestroy() => Unbind();
}
