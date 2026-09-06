using UnityEngine;

/// <summary>
/// 영웅 드래그 중 현재 위치의 배치 가능 여부를 Renderer 색상으로 표시합니다.
/// 공유 Material을 복제하지 않고 MaterialPropertyBlock을 사용합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class HeroDragVisualFeedback : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private Color validColor = new(0.2f, 1f, 0.25f, 1f);
    [SerializeField] private Color invalidColor = new(1f, 0.15f, 0.12f, 1f);

    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        propertyBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// 배치 가능하면 초록색, 불가능하면 빨간색 오버레이를 적용합니다.
    /// </summary>
    public void SetPlacementValidity(bool canPlace)
    {
        Color color = canPlace ? validColor : invalidColor;
        propertyBlock.Clear();
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i] is not LineRenderer && renderers[i] is not TrailRenderer)
            {
                renderers[i].SetPropertyBlock(propertyBlock);
            }
        }
    }

    /// <summary>
    /// 드래그 표시를 제거하고 Renderer의 원래 Material 표현으로 되돌립니다.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i] is not LineRenderer && renderers[i] is not TrailRenderer)
            {
                renderers[i].SetPropertyBlock(null);
            }
        }
    }

    private void OnDisable()
    {
        if (renderers != null)
        {
            Clear();
        }
    }
}
