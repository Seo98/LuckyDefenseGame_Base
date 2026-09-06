using UnityEngine;

public abstract class CharacterData : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    public string DisplayName => displayName;
    public float MaxHealth => maxHealth;
}