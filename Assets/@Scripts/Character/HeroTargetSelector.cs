using System;
using UnityEngine;

/// <summary>
/// 영웅의 타원형 공격 범위에서 Enemy 하나를 선택하고 유효한 동안 유지합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(HeroActor))]
public sealed class HeroTargetSelector : MonoBehaviour
{
    [SerializeField]
    private HeroActor heroActor;

    [SerializeField]
    private LayerMask enemyLayerMask = ~0;

    [SerializeField, Min(1)]
    private int candidateBufferSize = 128;

    private Collider[] candidateBuffer;

    /// <summary>
    /// 현재 고정된 공격 대상입니다. 유효한 대상이 없으면 null입니다.
    /// </summary>
    public EnemyActor CurrentTarget { get; private set; }

    /// <summary>
    /// 고정 대상이 변경될 때 새 대상을 발행합니다. 대상 해제 시 null입니다.
    /// </summary>
    public event Action<EnemyActor> TargetChanged;

    private void Awake()
    {
        heroActor ??= GetComponent<HeroActor>();
        candidateBuffer = new Collider[Mathf.Max(1, candidateBufferSize)];
    }

    /// <summary>
    /// 현재 대상을 유지할 수 있으면 그대로 반환하고, 아니면 새 대상을 탐색합니다.
    /// </summary>
    public bool TryGetOrAcquireTarget(out EnemyActor target)
    {
        if (IsTargetValid(CurrentTarget))
        {
            target = CurrentTarget;
            return true;
        }

        ClearTarget();

        if (!heroActor.CanAttack || heroActor.Data == null)
        {
            target = null;
            return false;
        }

        Vector2 range = heroActor.Data.AttackRange;
        float broadPhaseRadius = Mathf.Max(range.x, range.y);
        if (broadPhaseRadius <= 0f)
        {
            target = null;
            return false;
        }

        int candidateCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            broadPhaseRadius,
            candidateBuffer,
            enemyLayerMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < candidateCount; i++)
        {
            Collider candidateCollider = candidateBuffer[i];
            if (candidateCollider == null)
            {
                continue;
            }

            EnemyActor enemy = candidateCollider.GetComponentInParent<EnemyActor>();
            if (IsTargetValid(enemy))
            {
                SetTarget(enemy);
                target = enemy;
                ClearCandidateBuffer(candidateCount);
                return true;
            }
        }

        ClearCandidateBuffer(candidateCount);
        target = null;
        return false;
    }

    /// <summary>
    /// 지정한 Enemy가 살아 있고 현재 타원형 범위 안에 있는지 확인합니다.
    /// </summary>
    public bool IsTargetValid(EnemyActor enemy)
    {
        return heroActor != null &&
               heroActor.CanAttack &&
               enemy != null &&
               enemy.IsSpawned &&
               enemy.Character != null &&
               !enemy.Character.IsDead &&
               IsInsideEllipse(enemy.transform.position);
    }

    /// <summary>
    /// 현재 고정 대상을 해제합니다.
    /// </summary>
    public void ClearTarget()
    {
        if (CurrentTarget == null)
        {
            return;
        }

        CurrentTarget = null;
        TargetChanged?.Invoke(null);
    }

    private bool IsInsideEllipse(Vector3 worldPosition)
    {
        if (heroActor.Data == null)
        {
            return false;
        }

        Vector2 range = heroActor.Data.AttackRange;
        if (range.x <= 0f || range.y <= 0f)
        {
            return false;
        }

        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        float normalizedX = localPosition.x / range.x;
        float normalizedZ = localPosition.z / range.y;
        return normalizedX * normalizedX + normalizedZ * normalizedZ <= 1f;
    }

    private void SetTarget(EnemyActor target)
    {
        if (CurrentTarget == target)
        {
            return;
        }

        CurrentTarget = target;
        TargetChanged?.Invoke(CurrentTarget);
    }

    private void ClearCandidateBuffer(int count)
    {
        int clearCount = Mathf.Min(count, candidateBuffer.Length);
        for (int i = 0; i < clearCount; i++)
        {
            candidateBuffer[i] = null;
        }
    }

    private void OnDisable()
    {
        ClearTarget();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        heroActor = GetComponent<HeroActor>();
    }
#endif

    private void OnValidate()
    {
        candidateBufferSize = Mathf.Max(1, candidateBufferSize);
    }
}
