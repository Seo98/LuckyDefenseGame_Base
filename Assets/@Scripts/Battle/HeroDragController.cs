using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Unity Input System의 마우스 입력으로 영웅을 자유롭게 드래그하고 유효한 위치에 배치합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class HeroDragController : MonoBehaviour
{
    [SerializeField]
    private Camera inputCamera;

    [SerializeField]
    private HeroPlacementBoard placementBoard;

    [SerializeField]
    private HeroSummonStagingArea stagingArea;

    [SerializeField]
    private LayerMask heroLayerMask = ~0;

    [SerializeField, Min(1f)]
    private float maximumRayDistance = 500f;

    private HeroActor draggedHero;
    private HeroDragVisualFeedback dragFeedback;
    private Vector3 originalPosition;
    private int originalStagingSlot = -1;
    private bool originatedFromStaging;
    private Plane dragPlane;
    private readonly RaycastHit[] heroRaycastHits = new RaycastHit[32];
    private bool interactionEnabled = true;
    [SerializeField, Min(1f)] private float dragThresholdPixels = 8f;
    private HeroActor pressedHero;
    private Vector2 pressPosition;
    private bool worldPress;

    /// <summary>클릭한 영웅을 전달합니다. 빈 공간 클릭은 null로 선택을 해제합니다.</summary>
    public event System.Action<HeroActor> SelectionRequested;

    /// <summary>전투 종료 시 입력을 잠그고 진행 중인 드래그를 취소합니다.</summary>
    public void SetInteractionEnabled(bool value)
    {
        interactionEnabled = value;
        if (!value) { worldPress = false; pressedHero = null; CancelDrag(); }
    }

    /// <summary>
    /// 현재 드래그 중인 영웅입니다. 드래그 중이 아니면 null입니다.
    /// </summary>
    public HeroActor DraggedHero => draggedHero;

    private void Awake()
    {
        inputCamera ??= Camera.main;
    }

    /// <summary>
    /// 수동 조립 또는 추후 DI에서 카메라, 배치 보드와 소환 대기열을 연결합니다.
    /// </summary>
    public void Initialize(
        Camera camera,
        HeroPlacementBoard board,
        HeroSummonStagingArea staging)
    {
        inputCamera = camera != null
            ? camera
            : throw new System.ArgumentNullException(nameof(camera));
        placementBoard = board != null
            ? board
            : throw new System.ArgumentNullException(nameof(board));
        stagingArea = staging != null
            ? staging
            : throw new System.ArgumentNullException(nameof(staging));
    }

    private void Update()
    {
        if (!interactionEnabled) return;
        if (!ReferenceEquals(draggedHero, null) && (draggedHero == null || !draggedHero.IsSpawned))
            CancelDrag();
        // Unity Input System의 Mouse 장치를 직접 읽어 프로토타입 드래그를 처리합니다.
        Mouse mouse = Mouse.current;
        if (mouse == null || inputCamera == null || placementBoard == null)
        {
            return;
        }

        Vector2 pointerPosition = mouse.position.ReadValue();

        if (draggedHero == null && mouse.leftButton.wasPressedThisFrame)
        {
            worldPress = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
            pressPosition = pointerPosition;
            pressedHero = worldPress ? FindNearestHero(inputCamera.ScreenPointToRay(pointerPosition)) : null;
        }

        if (worldPress && draggedHero == null && mouse.leftButton.isPressed &&
            (pointerPosition - pressPosition).sqrMagnitude >= dragThresholdPixels * dragThresholdPixels)
        {
            worldPress = false;
            if (pressedHero != null)
            {
                SelectionRequested?.Invoke(pressedHero);
                TryBeginDrag(pointerPosition, pressedHero);
            }
            pressedHero = null;
        }

        if (draggedHero != null && mouse.leftButton.isPressed)
        {
            MoveDraggedHero(pointerPosition);
        }

        if (draggedHero != null && mouse.leftButton.wasReleasedThisFrame)
        {
            EndDrag();
        }
        else if (worldPress && mouse.leftButton.wasReleasedThisFrame)
        {
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                SelectionRequested?.Invoke(pressedHero);
            worldPress = false;
            pressedHero = null;
        }
    }

    private void TryBeginDrag(Vector2 pointerPosition, HeroActor hero)
    {
        if (hero == null || hero.IsSynthesisReserved)
        {
            return;
        }

        originatedFromStaging = stagingArea != null &&
                                stagingArea.TryBeginMove(hero, out originalStagingSlot);

        if (!originatedFromStaging &&
            !placementBoard.TryBeginMove(hero, out originalPosition))
        {
            originalStagingSlot = -1;
            return;
        }

        draggedHero = hero;
        draggedHero.Despawned += HandleDraggedHeroRemoved;
        draggedHero.Destroyed += HandleDraggedHeroRemoved;
        dragFeedback = draggedHero.GetComponent<HeroDragVisualFeedback>();
        placementBoard.CanPlaceAt(draggedHero, draggedHero.transform.position, out Vector3 previewPosition);
        dragPlane = new Plane(Vector3.up, previewPosition);
        MoveDraggedHero(pointerPosition);
    }

    private void MoveDraggedHero(Vector2 pointerPosition)
    {
        Ray ray = inputCamera.ScreenPointToRay(pointerPosition);
        if (dragPlane.Raycast(ray, out float enter))
        {
            Vector3 candidatePosition = ray.GetPoint(enter);

            bool canPlace = placementBoard.CanPlaceAt(
                draggedHero,
                candidatePosition,
                out Vector3 resolvedPosition);
            // 미리보기와 최종 드롭에 동일한 바닥 정렬 결과를 사용합니다.
            draggedHero.transform.position = resolvedPosition;
            dragFeedback?.SetPlacementValidity(canPlace);
        }
    }

    private void EndDrag()
    {
        HeroActor hero = draggedHero;
        UnsubscribeHero(hero);
        draggedHero = null;

        if (placementBoard.TryPlaceAt(hero, hero.transform.position))
        {
            if (originatedFromStaging) stagingArea.Release(hero);
            dragFeedback?.Clear();
            ResetDragState();
            return;
        }

        draggedHero = hero;
        CancelDrag();
    }

    /// <summary>배치를 확정하지 않고 원래 벤치 칸 또는 필드 예약 위치로 되돌립니다.</summary>
    public void CancelDrag()
    {
        HeroActor hero = draggedHero;
        UnsubscribeHero(hero);
        draggedHero = null;
        if (hero == null || !hero.IsSpawned)
        {
            if (!ReferenceEquals(hero, null))
            {
                stagingArea?.Release(hero);
                if (placementBoard != null) placementBoard.Release(hero, out _);
            }
            if (dragFeedback != null) dragFeedback.Clear();
            ResetDragState();
            return;
        }

        bool restored = originatedFromStaging
            ? stagingArea != null && stagingArea.TryRestore(hero, originalStagingSlot)
            : placementBoard != null && placementBoard.TryRestore(hero);

        if (!restored)
        {
            hero.CompletePlacement(false);
            Debug.LogError($"Could not restore {hero.name} to its original position.");
        }

        dragFeedback?.Clear();
        ResetDragState();
    }

    private void ResetDragState()
    {
        dragFeedback = null;
        originalPosition = default;
        originalStagingSlot = -1;
        originatedFromStaging = false;
    }

    private void HandleDraggedHeroRemoved(HeroActor hero)
    {
        UnsubscribeHero(hero);
        draggedHero = null;
        if (dragFeedback != null) dragFeedback.Clear();
        ResetDragState();
    }

    private void UnsubscribeHero(HeroActor hero)
    {
        if (ReferenceEquals(hero, null)) return;
        hero.Despawned -= HandleDraggedHeroRemoved;
        hero.Destroyed -= HandleDraggedHeroRemoved;
    }

    private HeroActor FindNearestHero(Ray ray)
    {
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            heroRaycastHits,
            maximumRayDistance,
            heroLayerMask,
            QueryTriggerInteraction.Collide);

        HeroActor nearestHero = null;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = heroRaycastHits[i];
            HeroActor candidate = hit.collider.GetComponentInParent<HeroActor>();
            if (candidate != null && hit.distance < nearestDistance)
            {
                nearestHero = candidate;
                nearestDistance = hit.distance;
            }
        }

        return nearestHero;
    }

    private void OnDisable()
    {
        worldPress = false;
        pressedHero = null;
        CancelDrag();
    }

    private void OnValidate()
    {
        maximumRayDistance = Mathf.Max(1f, maximumRayDistance);
    }
}
