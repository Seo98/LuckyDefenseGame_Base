using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyPathFollower : MonoBehaviour
{
    [Header("Path")]
    [SerializeField] private LoopPath loopPath;
    [SerializeField, Min(0)] private int startWaypointIndex;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 3f;
    [SerializeField, Min(0f)] private float rotationSpeed = 10f;
    [SerializeField, Min(0.001f)] private float arrivalDistance = 0.1f;

    private int currentWaypointIndex;
    private bool isFollowing = true;

    private void Awake()
    {
        currentWaypointIndex = startWaypointIndex;
    }

    private void Update()
    {
        if (!isFollowing ||
            loopPath == null ||
            !loopPath.TryGetWaypoint(currentWaypointIndex, out Transform target))
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        Vector3 targetPosition = target.position;
        Vector3 offset = targetPosition - currentPosition;

        if (offset.sqrMagnitude <= arrivalDistance * arrivalDistance)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % loopPath.WaypointCount;
            return;
        }

        transform.position = Vector3.MoveTowards(
            currentPosition,
            targetPosition,
            moveSpeed * Time.deltaTime);

        RotateTowards(offset);
    }

    /// <summary>
    /// 이동 경로, 이동속도와 시작 Waypoint를 설정하고 이동을 시작합니다.
    /// </summary>
    public void Initialize(LoopPath path, float speed, int waypointIndex = 0)
    {
        loopPath = path;
        moveSpeed = Mathf.Max(0f, speed);
        currentWaypointIndex = waypointIndex;
        isFollowing = loopPath != null && loopPath.WaypointCount > 0;
    }

    /// <summary>
    /// 이동을 중지하고 현재 경로의 런타임 참조를 해제합니다.
    /// </summary>
    public void Stop()
    {
        isFollowing = false;
        loopPath = null;
        currentWaypointIndex = 0;
    }

    private void RotateTowards(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);
    }
}
