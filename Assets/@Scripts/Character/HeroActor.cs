using System;
using UnityEngine;

/// <summary>
/// 전장에 배치된 영웅 기물의 데이터와 Character 생명주기를 연결합니다.
/// 공격, 타겟 탐색, 합성 및 풀 관리는 별도 시스템이 담당합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Character))]
public sealed class HeroActor : MonoBehaviour
{
    [SerializeField]
    private Character character;

    private HeroData data;

    /// <summary>
    /// 현재 이 기물에 적용된 영웅 정의입니다.
    /// 풀에서 대기 중일 때는 null입니다.
    /// </summary>
    public HeroData Data => data;

    /// <summary>
    /// 체력과 공통 캐릭터 상태를 관리하는 컴포넌트입니다.
    /// </summary>
    public Character Character => character;

    /// <summary>
    /// 현재 풀에서 꺼내져 전장에 배치된 상태인지 나타냅니다.
    /// </summary>
    public bool IsSpawned { get; private set; }

    /// <summary>
    /// 현재 유효한 전장 슬롯에 배치되어 있는지 나타냅니다.
    /// </summary>
    public bool IsPlaced { get; private set; }

    /// <summary>
    /// 플레이어가 현재 이 영웅을 드래그하고 있는지 나타냅니다.
    /// </summary>
    public bool IsDragging { get; private set; }

    /// <summary>합성 재료로 예약된 동안 이동과 공격을 금지합니다.</summary>
    public bool IsSynthesisReserved { get; private set; }
    /// <summary>OnDestroy 진입 이후에는 Transform/배치 상태를 변경하지 않습니다.</summary>
    public bool IsBeingDestroyed { get; private set; }
    /// <summary>전투 종료 후 남은 영웅의 공격을 차단합니다.</summary>
    public bool IsBattleActive { get; private set; }

    /// <summary>전투 수명에 따른 공격 허용 상태입니다.</summary>
    public void SetBattleActive(bool value) => IsBattleActive = value;

    /// <summary>풀에서 데이터 적용을 끝냈을 때 발생합니다.</summary>
    public event Action<HeroActor> Spawned;

    /// <summary>합성 트랜잭션의 재료 예약 상태를 설정합니다.</summary>
    public void SetSynthesisReserved(bool value)
    {
        IsSynthesisReserved = value;
        PlacementStateChanged?.Invoke(this);
    }

    /// <summary>
    /// 스폰 및 배치가 완료됐고 드래그와 사망 상태가 아닐 때만 true입니다.
    /// 공격 컴포넌트는 이 값을 기준으로 동작합니다.
    /// </summary>
    public bool CanAttack =>
        IsSpawned &&
        IsBattleActive &&
        !IsBeingDestroyed &&
        IsPlaced &&
        !IsDragging &&
        !IsSynthesisReserved &&
        !character.IsDead;

    /// <summary>
    /// 영웅의 Character가 사망했을 때 발생합니다.
    /// 실제 제거와 풀 반환은 외부 시스템이 담당합니다.
    /// </summary>
    public event Action<HeroActor> Died;

    /// <summary>풀 밖에서 직접 파괴된 경우에도 외부 등록을 정리하도록 알립니다.</summary>
    public event Action<HeroActor> Destroyed;

    /// <summary>풀 반환으로 스폰 상태가 해제됐을 때 발생합니다.</summary>
    public event Action<HeroActor> Despawned;

    /// <summary>
    /// 배치 또는 드래그 상태가 변경됐을 때 발생합니다.
    /// </summary>
    public event Action<HeroActor> PlacementStateChanged;

    private void Awake()
    {
        character ??= GetComponent<Character>();
    }

    /// <summary>
    /// 풀에서 꺼낸 영웅에 데이터를 적용하고 런타임 상태를 초기화합니다.
    /// </summary>
    public void OnSpawned(HeroData heroData)
    {
        if (heroData == null)
        {
            throw new ArgumentNullException(nameof(heroData));
        }

        if (IsSpawned)
        {
            throw new InvalidOperationException($"{name} is already spawned.");
        }

        data = heroData;

        character.Died -= HandleCharacterDied;
        character.Died += HandleCharacterDied;
        character.Initialize(data);

        IsSpawned = true;
        IsBattleActive = true;
        IsPlaced = false;
        IsDragging = false;
        IsSynthesisReserved = false;
        Spawned?.Invoke(this);
    }

    /// <summary>
    /// 전장 또는 소환 대기열의 영웅 드래그를 시작하고 공격할 수 없는 상태로 전환합니다.
    /// </summary>
    public bool BeginDrag()
    {
        if (!IsSpawned || IsDragging || IsSynthesisReserved || IsBeingDestroyed || !IsBattleActive)
        {
            return false;
        }

        IsDragging = true;
        IsPlaced = false;
        PlacementStateChanged?.Invoke(this);
        return true;
    }

    /// <summary>
    /// 드래그 또는 최초 배치를 끝내고 최종 배치 성공 여부를 적용합니다.
    /// </summary>
    public void CompletePlacement(bool isPlaced)
    {
        if (!IsSpawned)
        {
            return;
        }

        IsDragging = false;
        IsPlaced = isPlaced;
        PlacementStateChanged?.Invoke(this);
    }

    /// <summary>
    /// 풀 반환 전에 이벤트 구독과 런타임 데이터를 초기화합니다.
    /// GameObject 비활성화는 풀 서비스가 담당합니다.
    /// </summary>
    public void OnDespawned()
    {
        character.Died -= HandleCharacterDied;
        character.ResetRuntimeState();

        data = null;
        IsBattleActive = false;
        IsSynthesisReserved = false;
        IsSpawned = false;
        IsPlaced = false;
        IsDragging = false;
        Despawned?.Invoke(this);
    }

    private void HandleCharacterDied(Character _)
    {
        if (IsSpawned)
        {
            Died?.Invoke(this);
        }
    }

    private void OnDestroy()
    {
        IsBeingDestroyed = true;
        Destroyed?.Invoke(this);
        Destroyed = null;
        Spawned = null;
        Despawned = null;
        Died = null;
        if (character != null)
        {
            character.Died -= HandleCharacterDied;
        }

        PlacementStateChanged = null;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        character = GetComponent<Character>();
    }
#endif
}
