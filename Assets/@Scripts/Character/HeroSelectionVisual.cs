using UnityEngine;

/// <summary>태생 등급 바닥 오라와 독립적인 선택 링입니다. 재질 교체 없이 드래그 색상과 공존합니다.</summary>
[RequireComponent(typeof(HeroActor))]
public sealed class HeroSelectionVisual : MonoBehaviour
{
    [SerializeField] private LineRenderer birthRing;
    [SerializeField] private LineRenderer selectionRing;
    private HeroActor hero;
    private Collider bodyCollider;
    private const int Segments = 48;
    private readonly Vector3[] birthPoints = new Vector3[Segments];
    private readonly Vector3[] selectionPoints = new Vector3[Segments];
    private void Awake()
    {
        hero = GetComponent<HeroActor>();
        bodyCollider = GetComponentInChildren<Collider>();
        Configure(birthRing);
        Configure(selectionRing);
        hero.Spawned += UpdateBirth;
        hero.Despawned += Clear;
    }
    private static void Configure(LineRenderer ring)
    {
        if (ring == null) return;
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = Segments;
    }
    private void LateUpdate()
    {
        if (hero == null || !hero.IsSpawned || hero.IsBeingDestroyed ||
            ((birthRing == null || !birthRing.enabled) && (selectionRing == null || !selectionRing.enabled)) ||
            !HeroPlacementUtility.TryGetWorldBounds(bodyCollider, out Bounds bounds)) return;
        // 부모/높이/회전이 바뀌어도 Collider 발밑 바로 위에 수평으로 유지합니다.
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) + .16f;
        Vector3 center = new(bounds.center.x, bounds.min.y + .06f, bounds.center.z);
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            Vector3 direction = new(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            birthPoints[i] = center + direction * radius;
            selectionPoints[i] = center + direction * (radius + .15f);
        }
        if (birthRing != null && birthRing.enabled) birthRing.SetPositions(birthPoints);
        if (selectionRing != null && selectionRing.enabled) selectionRing.SetPositions(selectionPoints);
    }
    private void UpdateBirth(HeroActor actor)
    {
        if (birthRing == null) return;
        Color color = HeroPresentation.GradeColor(actor.Data.BirthGrade);
        birthRing.startColor = birthRing.endColor = color;
        birthRing.enabled = color.a > 0;
        SetSelected(false);
    }
    /// <summary>선택 링만 켜거나 끕니다. 태생 오라는 유지합니다.</summary>
    public void SetSelected(bool value) { if (selectionRing != null) selectionRing.enabled = value; }
    private void Clear(HeroActor _) { SetSelected(false); if (birthRing != null) birthRing.enabled = false; }
    private void OnDestroy()
    {
        if (hero == null) return;
        hero.Spawned -= UpdateBirth; hero.Despawned -= Clear;
    }
}
