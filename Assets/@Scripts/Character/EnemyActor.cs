using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Character), typeof(EnemyPathFollower))]
public sealed class EnemyActor : MonoBehaviour
{
    [SerializeField] private Character character;
    [SerializeField] private EnemyPathFollower pathFollower;

    private EnemyData data;
    private bool removalRequested;

    public EnemyData Data => data;
    public Character Character => character;
    public bool IsSpawned { get; private set; }

    /// <summary>
    /// Enemy가 전장에서 제거되어야 할 때 발생합니다.
    /// 실제 풀 반환과 보상 처리는 외부 전투 시스템이 담당합니다.
    /// </summary>
    public event Action<EnemyActor, EnemyRemovalReason> RemovalRequested;

    private void Awake()
    {
        character ??= GetComponent<Character>();
        pathFollower ??= GetComponent<EnemyPathFollower>();
    }

    /// <summary>
    /// 풀에서 꺼낸 Enemy에 데이터와 이동 경로를 적용합니다.
    /// </summary>
    public void OnSpawned(EnemyData enemyData, LoopPath path, int waypointIndex = 0)
    {
        if (enemyData == null)
        {
            throw new ArgumentNullException(nameof(enemyData));
        }

        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (IsSpawned)
        {
            throw new InvalidOperationException($"{name} is already spawned.");
        }

        data = enemyData;
        removalRequested = false;

        character.Died -= HandleCharacterDied;
        character.Died += HandleCharacterDied;
        character.Initialize(data);
        pathFollower.Initialize(path, data.MoveSpeed, waypointIndex);

        IsSpawned = true;
    }

    /// <summary>
    /// 풀 반환 전에 이벤트와 런타임 상태를 정리합니다.
    /// GameObject 비활성화는 풀 서비스가 담당합니다.
    /// </summary>
    public void OnDespawned()
    {
        character.Died -= HandleCharacterDied;
        pathFollower.Stop();
        character.ResetRuntimeState();

        data = null;
        removalRequested = false;
        IsSpawned = false;
    }

    /// <summary>
    /// 지정한 원인으로 Enemy 제거를 요청합니다.
    /// 동일한 스폰 주기에는 한 번만 발생합니다.
    /// </summary>
    public void RequestRemoval(EnemyRemovalReason reason)
    {
        if (!IsSpawned || removalRequested)
        {
            return;
        }

        removalRequested = true;
        RemovalRequested?.Invoke(this, reason);
    }

    private void HandleCharacterDied(Character _)
    {
        RequestRemoval(EnemyRemovalReason.Killed);
    }

    private void OnDestroy()
    {
        if (character != null)
        {
            character.Died -= HandleCharacterDied;
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        character = GetComponent<Character>();
        pathFollower = GetComponent<EnemyPathFollower>();
    }
#endif
}
