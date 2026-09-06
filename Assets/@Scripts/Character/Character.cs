using System;
using UnityEngine;

public sealed class Character : MonoBehaviour
{
    private CharacterData data;
    private float currentHealth;

    public CharacterData Data => data;
    public float CurrentHealth => currentHealth;
    public bool IsInitialized => data != null;
    public bool IsDead => IsInitialized && currentHealth <= 0f;

    public event Action<Character> HealthChanged;
    public event Action<Character> Died;

    /// <summary>
    /// 지정한 데이터로 캐릭터의 런타임 상태를 초기화합니다.
    /// </summary>
    public void Initialize(CharacterData characterData)
    {
        if (characterData == null)
        {
            throw new ArgumentNullException(nameof(characterData));
        }

        data = characterData;
        currentHealth = data.MaxHealth;

        HealthChanged?.Invoke(this);
    }

    /// <summary>
    /// 풀 반환을 위해 캐릭터의 런타임 상태를 초기화 전 상태로 되돌립니다.
    /// </summary>
    public void ResetRuntimeState()
    {
        data = null;
        currentHealth = 0f;
    }

    /// <summary>
    /// 캐릭터에게 피해를 적용합니다.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (!IsInitialized || IsDead || damage <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        HealthChanged?.Invoke(this);

        if (IsDead)
        {
            Died?.Invoke(this);
        }
    }
}
