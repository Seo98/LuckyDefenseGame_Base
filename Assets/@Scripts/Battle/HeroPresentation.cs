using UnityEngine;

/// <summary>UI/선택 표시에서 공유하는 등급 이름과 태생 색상입니다.</summary>
public static class HeroPresentation
{
    /// <summary>기획상 등급 이름입니다.</summary>
    public static string GradeName(HeroGrade grade) => grade switch
    {
        HeroGrade.Normal => "일반", HeroGrade.Elite => "정예", HeroGrade.Rare => "희귀",
        HeroGrade.RarePlus => "희귀+", HeroGrade.Hero => "영웅", HeroGrade.HeroPlus => "영웅+",
        HeroGrade.Legendary => "전설", HeroGrade.LegendaryPlus => "전설+",
        HeroGrade.Transcendent => "초월", HeroGrade.TranscendentPlus => "초월+", _ => grade.ToString()
    };
    /// <summary>일반 무표시, 정예 초록, 희귀 파랑, 영웅 보라, 전설 금색, 초월 청록입니다.</summary>
    public static Color GradeColor(HeroGrade grade) => grade switch
    {
        HeroGrade.Normal => Color.clear,
        HeroGrade.Elite => new Color(.2f, 1f, .45f),
        HeroGrade.Rare or HeroGrade.RarePlus => new Color(.25f, .65f, 1f),
        HeroGrade.Hero or HeroGrade.HeroPlus => new Color(.8f, .35f, 1f),
        HeroGrade.Legendary or HeroGrade.LegendaryPlus => new Color(1f, .75f, .15f),
        _ => new Color(.1f, 1f, .9f)
    };
    /// <summary>선택 UI와 합성 결과 미리보기에 공통 사용합니다.</summary>
    public static string Describe(HeroData data) => data == null ? "" :
        $"<b>{data.DisplayName}</b>\n{GradeName(data.Grade)} · 태생 {GradeName(data.BirthGrade)}\n\n" +
        $"공격력  {data.AttackDamage:0.#}\n공격 주기  {data.AttackInterval:0.##}초\n" +
        $"사거리  {data.AttackRange.x:0.#} × {data.AttackRange.y:0.#}\n인구수  {data.PopulationCost}";
}
