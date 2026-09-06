using System;
using UnityEngine;

/// <summary>
/// HeroData의 공격 주기와 피해량을 사용해 고정 대상에게 기본 공격을 수행합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(HeroActor), typeof(HeroTargetSelector))]
public sealed class HeroAttackController : MonoBehaviour
{
    [SerializeField]
    private HeroActor heroActor;

    [SerializeField]
    private HeroTargetSelector targetSelector;

    [SerializeField, Min(0.01f)]
    private float targetSearchInterval = 0.1f;

    private float attackCooldown;
    private float targetSearchCooldown;

    /// <summary>
    /// 기본 공격으로 피해를 적용한 직후 발생합니다.
    /// </summary>
    public event Action<HeroActor, EnemyActor, float> Attacked;

    private void Awake()
    {
        heroActor ??= GetComponent<HeroActor>();
        targetSelector ??= GetComponent<HeroTargetSelector>();
    }

    private void Update()
    {
        if (!heroActor.CanAttack || heroActor.Data == null)
        {
            targetSelector.ClearTarget();
            return;
        }

        attackCooldown = Mathf.Max(0f, attackCooldown - Time.deltaTime);
        targetSearchCooldown = Mathf.Max(0f, targetSearchCooldown - Time.deltaTime);

        EnemyActor target;
        if (targetSelector.CurrentTarget != null)
        {
            if (!targetSelector.TryGetOrAcquireTarget(out target))
            {
                targetSearchCooldown = targetSearchInterval;
                return;
            }
        }
        else
        {
            if (targetSearchCooldown > 0f)
            {
                return;
            }

            targetSearchCooldown = targetSearchInterval;
            if (!targetSelector.TryGetOrAcquireTarget(out target))
            {
                return;
            }
        }

        if (attackCooldown > 0f)
        {
            return;
        }

        float damage = heroActor.Data.AttackDamage;
        target.Character.TakeDamage(damage);
        attackCooldown = Mathf.Max(0.01f, heroActor.Data.AttackInterval);
        Attacked?.Invoke(heroActor, target, damage);
    }

    private void OnDisable()
    {
        attackCooldown = 0f;
        targetSearchCooldown = 0f;

        if (targetSelector != null)
        {
            targetSelector.ClearTarget();
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        heroActor = GetComponent<HeroActor>();
        targetSelector = GetComponent<HeroTargetSelector>();
    }
#endif

    private void OnValidate()
    {
        targetSearchInterval = Mathf.Max(0.01f, targetSearchInterval);
    }
}
