using System;
using UnityEngine;

/// <summary>
/// 영웅 소환 구간 확률과 각 구간의 HeroData 후보를 정의합니다.
/// </summary>
[CreateAssetMenu(
    fileName = "HeroSummonTableData",
    menuName = "Defense/Summon/Hero Summon Table Data")]
public sealed class HeroSummonTableData : ScriptableObject
{
    /// <summary>
    /// 같은 소환 구간 내부에서 추첨할 영웅 후보입니다.
    /// </summary>
    [Serializable]
    public sealed class Candidate
    {
        [SerializeField]
        private HeroData heroData;

        [SerializeField, Min(0.001f)]
        private float weight = 1f;

        /// <summary>
        /// 추첨 결과로 사용할 영웅 데이터입니다.
        /// </summary>
        public HeroData HeroData => heroData;

        /// <summary>
        /// 같은 구간 후보 사이에서 사용하는 상대 가중치입니다.
        /// </summary>
        public float Weight => weight;

        /// <summary>
        /// 후보가 추첨 가능한 설정인지 확인합니다.
        /// </summary>
        public bool IsValid => heroData != null && weight > 0f;
    }

    /// <summary>
    /// 하나의 소환 구간과 해당 구간 내부 후보를 묶습니다.
    /// </summary>
    [Serializable]
    public sealed class TierEntry
    {
        [SerializeField]
        private HeroSummonTier tier;

        [SerializeField, Min(0.001f)]
        private float weight = 1f;

        [SerializeField]
        private Candidate[] candidates = Array.Empty<Candidate>();

        /// <summary>
        /// 이 항목이 나타내는 소환 확률 구간입니다.
        /// </summary>
        public HeroSummonTier Tier => tier;

        /// <summary>
        /// 전체 소환 구간 사이에서 사용하는 상대 가중치입니다.
        /// </summary>
        public float Weight => weight;

        /// <summary>
        /// 이 구간 내부의 영웅 후보 수입니다.
        /// </summary>
        public int CandidateCount => candidates?.Length ?? 0;

        /// <summary>
        /// 지정한 인덱스의 영웅 후보를 반환합니다.
        /// </summary>
        public Candidate GetCandidate(int index)
        {
            if (candidates == null || (uint)index >= (uint)candidates.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return candidates[index];
        }

        /// <summary>
        /// 구간 가중치, 후보 및 등급 구성이 유효한지 확인합니다.
        /// </summary>
        public bool IsValid
        {
            get
            {
                if (weight <= 0f || candidates == null || candidates.Length == 0)
                {
                    return false;
                }

                for (int i = 0; i < candidates.Length; i++)
                {
                    Candidate candidate = candidates[i];
                    if (candidate == null ||
                        !candidate.IsValid ||
                        !MatchesTier(tier, candidate.HeroData.Grade))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }

    [SerializeField]
    private TierEntry[] tiers = Array.Empty<TierEntry>();

    /// <summary>
    /// 등록된 소환 구간 수입니다.
    /// </summary>
    public int TierCount => tiers?.Length ?? 0;

    /// <summary>
    /// 지정한 인덱스의 소환 구간을 반환합니다.
    /// </summary>
    public TierEntry GetTier(int index)
    {
        if (tiers == null || (uint)index >= (uint)tiers.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return tiers[index];
    }

    /// <summary>
    /// 모든 구간이 유효하고 같은 소환 구간이 중복되지 않았는지 확인합니다.
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (tiers == null || tiers.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < tiers.Length; i++)
            {
                if (tiers[i] == null || !tiers[i].IsValid)
                {
                    return false;
                }

                for (int j = i + 1; j < tiers.Length; j++)
                {
                    if (tiers[j] != null && tiers[i].Tier == tiers[j].Tier)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    private static bool MatchesTier(HeroSummonTier tier, HeroGrade grade)
    {
        return tier switch
        {
            HeroSummonTier.Normal => grade == HeroGrade.Normal,
            HeroSummonTier.Elite => grade == HeroGrade.Elite,
            HeroSummonTier.Rare => grade is HeroGrade.Rare or HeroGrade.RarePlus,
            HeroSummonTier.Hero => grade is HeroGrade.Hero or HeroGrade.HeroPlus,
            HeroSummonTier.Legendary =>
                grade is HeroGrade.Legendary or HeroGrade.LegendaryPlus,
            _ => false
        };
    }
}
