using UnityEngine;

[DisallowMultipleComponent]
public sealed class LoopPath : MonoBehaviour
{
    [SerializeField] private Transform[] waypoints;

    /// <summary>
    /// 등록된 Waypoint 개수입니다.
    /// </summary>
    public int WaypointCount => waypoints?.Length ?? 0;

    /// <summary>
    /// 지정한 순번의 Waypoint를 반환합니다.
    /// 순번은 경로의 처음과 끝을 기준으로 자동 순환됩니다.
    /// </summary>
    public bool TryGetWaypoint(int index, out Transform waypoint)
    {
        if (WaypointCount == 0)
        {
            waypoint = null;
            return false;
        }

        int wrappedIndex = index % WaypointCount;

        if (wrappedIndex < 0)
        {
            wrappedIndex += WaypointCount;
        }

        waypoint = waypoints[wrappedIndex];
        return waypoint != null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RefreshWaypoints();
    }

    private void RefreshWaypoints()
    {
        int childCount = transform.childCount;
        waypoints = new Transform[childCount];

        for (int i = 0; i < childCount; i++)
        {
            waypoints[i] = transform.GetChild(i);
        }
    }
#endif
}
