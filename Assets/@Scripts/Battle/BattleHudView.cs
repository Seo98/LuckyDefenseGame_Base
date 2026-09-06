using System;
using R3;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// uGUI와 TextMeshPro로 전투 상태를 표시하고 소환 입력을 전달합니다.
/// 게임 규칙은 포함하지 않으며 BattleGameController와 런타임 모델만 구독합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleHudView : MonoBehaviour
{
    [Header("uGUI")]
    [SerializeField] private Button summonButton;
    [SerializeField] private GameObject bossPanel;
    [SerializeField] private Image bossHealthFill;

    [Header("TextMeshPro")]
    [SerializeField] private TMP_Text mineralLabel;
    [SerializeField] private TMP_Text populationLabel;
    [SerializeField] private TMP_Text enemyLabel;
    [SerializeField] private TMP_Text waveLabel;
    [SerializeField] private TMP_Text timerLabel;
    [SerializeField] private TMP_Text bossLabel;
    [SerializeField] private TMP_Text resultLabel;

    /// <summary>
    /// R3 Observable 구독 수명을 UI 바인딩 단위로 관리합니다.
    /// </summary>
    private CompositeDisposable subscriptions;

    private BattleGameController controller;
    private int displayedSeconds = -1;

    /// <summary>
    /// Inspector의 uGUI/TMP 참조를 확인하고 R3 상태 스트림과 전투 이벤트를 연결합니다.
    /// </summary>
    public void Bind(BattleGameController battleController)
    {
        if (battleController == null)
        {
            throw new ArgumentNullException(nameof(battleController));
        }

        Unbind();
        controller = battleController;
        ValidateReferences();
        displayedSeconds = -1;

        subscriptions = new CompositeDisposable();
        subscriptions.Add(controller.MineralWallet.BalanceChanged.Subscribe(UpdateMinerals));
        subscriptions.Add(controller.HeroPopulation.UsedPopulationChanged.Subscribe(_ => UpdatePopulation()));
        subscriptions.Add(controller.HeroPopulation.MaximumPopulationChanged.Subscribe(_ => UpdatePopulation()));
        subscriptions.Add(controller.EnemyPopulation.ActiveCountChanged.Subscribe(UpdateEnemyCount));
        subscriptions.Add(controller.Waves.CurrentWaveIndexChanged.Subscribe(UpdateWaveIndex));
        subscriptions.Add(controller.Waves.RemainingTimeChanged.Subscribe(UpdateTimer));
        subscriptions.Add(controller.Waves.StateChanged.Subscribe(UpdateRunState));

        summonButton.onClick.AddListener(HandleSummonClicked);
        controller.SummonCompleted += HandleSummonCompleted;
        controller.SessionStateChanged += HandleSessionStateChanged;
        controller.BossHealthChanged += UpdateBossHealth;
        controller.Waves.WaveStarted += HandleWaveStarted;
        controller.Waves.Defeated += HandleDefeated;
        controller.Waves.SequenceCompleted += HandleSequenceCompleted;

        UpdatePopulation();
        UpdateBossHealth(0f, 0f, false);
    }

    /// <summary>
    /// 등록한 UI 입력, 게임 이벤트 및 R3 구독을 모두 해제합니다.
    /// </summary>
    public void Unbind()
    {
        if (summonButton != null)
        {
            summonButton.onClick.RemoveListener(HandleSummonClicked);
        }

        if (controller != null)
        {
            controller.SummonCompleted -= HandleSummonCompleted;
            controller.SessionStateChanged -= HandleSessionStateChanged;
            controller.BossHealthChanged -= UpdateBossHealth;
            controller.Waves.WaveStarted -= HandleWaveStarted;
            controller.Waves.Defeated -= HandleDefeated;
            controller.Waves.SequenceCompleted -= HandleSequenceCompleted;
        }

        subscriptions?.Dispose();
        subscriptions = null;
        controller = null;
    }

    private void ValidateReferences()
    {
        if (summonButton == null || bossPanel == null || bossHealthFill == null ||
            mineralLabel == null || populationLabel == null || enemyLabel == null ||
            waveLabel == null || timerLabel == null || bossLabel == null || resultLabel == null)
        {
            throw new InvalidOperationException("BattleHudView: uGUI/TMP references must be assigned.");
        }
    }

    private void HandleSummonClicked()
    {
        controller.RequestHeroSummon();
    }

    private void HandleSummonCompleted(HeroSummonService.SummonResult result)
    {
        resultLabel.text = result.Status switch
        {
            HeroSummonService.SummonStatus.Success =>
                $"소환: {result.Hero.Data.DisplayName} ({result.Hero.Data.Grade})",
            HeroSummonService.SummonStatus.NotEnoughMinerals => "미네랄이 부족합니다.",
            HeroSummonService.SummonStatus.PopulationLimit => "영웅 인구수가 가득 찼습니다.",
            HeroSummonService.SummonStatus.NoPlacementSlot => "배치할 빈 슬롯이 없습니다.",
            HeroSummonService.SummonStatus.StagingFull => "소환 대기열 8칸이 가득 찼습니다.",
            HeroSummonService.SummonStatus.Busy => "소환 처리 중입니다.",
            HeroSummonService.SummonStatus.BattleEnded => "전투가 종료되어 소환할 수 없습니다.",
            _ => "소환 데이터를 확인해 주세요."
        };
    }

    private void UpdateMinerals(int value)
    {
        mineralLabel.text = $"미네랄  {value}";
    }

    private void UpdatePopulation()
    {
        populationLabel.text =
            $"인구수  {controller.HeroPopulation.UsedPopulation} / " +
            controller.HeroPopulation.MaximumPopulation;
    }

    private void UpdateEnemyCount(int value)
    {
        enemyLabel.text = $"적  {value} / {controller.EnemyPopulation.MaximumActiveCount}";
    }

    private void UpdateWaveIndex(int index)
    {
        if (index < 0)
        {
            waveLabel.text = "웨이브 대기";
        }
    }

    private void HandleWaveStarted(int index, WaveData wave)
    {
        waveLabel.text = $"Wave {index + 1}  {wave.DisplayName}";
        resultLabel.text = string.Empty;
    }

    private void UpdateTimer(float seconds)
    {
        int wholeSeconds = Mathf.CeilToInt(seconds);
        if (displayedSeconds == wholeSeconds)
        {
            return;
        }

        displayedSeconds = wholeSeconds;
        timerLabel.SetText("남은 시간  {0}초", wholeSeconds);
    }

    private void UpdateBossHealth(float current, float maximum, bool visible)
    {
        bossPanel.SetActive(visible);
        bossHealthFill.fillAmount = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        bossLabel.text = visible
            ? $"BOSS  {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}"
            : string.Empty;
    }

    private void UpdateRunState(WaveController.RunState state)
    {
        summonButton.interactable = controller.CanOperate;
    }

    private void HandleSessionStateChanged() => summonButton.interactable = controller.CanOperate;

    private void HandleDefeated(WaveController.DefeatReason reason)
    {
        resultLabel.text = reason == WaveController.DefeatReason.EnemyLimitReached
            ? "패배: 적이 100마리 이상 쌓였습니다."
            : "패배: 제한 시간 내에 보스를 처치하지 못했습니다.";
    }

    private void HandleSequenceCompleted()
    {
        resultLabel.text = "모든 웨이브 완료";
    }

    private void OnDestroy()
    {
        Unbind();
    }

}
