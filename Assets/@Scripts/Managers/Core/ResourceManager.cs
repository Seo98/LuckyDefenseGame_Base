using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

/// <summary>
/// Addressable Asset System을 기반으로 리소스를 관리하는 매니저 클래스
/// 리소스의 비동기 로딩, 캐싱, 레이블 기반 일괄 로딩 기능을 수행합니다.
/// </summary>
/// <remarks>
/// 이 클래스는 MonoBehaviour를 상속받지 않으며, 게임 내 유일한 리소스 공급자 역할을 합니다.<br/>
/// 네트워크 객체의 생성(Instantiate/Spawn)은 담당하지 않고, 오직 원본 에셋(Prefab)을 메모리에 로드하여 제공하는 역할에 집중합니다.
/// </remarks>
public class ResourceManager
{
    private Dictionary<string, Object> _resources = new Dictionary<string, Object>();
    private Dictionary<string, AsyncOperationHandle> _handles = new Dictionary<string, AsyncOperationHandle>();
    /// <summary>
    /// 리소스를 가져옵니다. 없다면 즉시 동기 로딩(WaitForCompletion)을 시도합니다.
    /// ItemDatabase처럼 반드시 있어야 하는 에셋에 사용하세요.
    /// </summary>
    public T Load<T>(string key) where T : Object
    {
        // 1. 캐시에 있으면 반환
        if (_resources.TryGetValue(key, out Object resource))
        {
            return resource as T;
        }

        // 2. 없으면 어드레서블로 즉시 로딩 (2021.2+ 기능)
        // 비동기를 동기처럼 기다리게 함. 약간의 프레임 드랍이 있을 수 있지만 초기화엔 안전함.
        var op = Addressables.LoadAssetAsync<T>(key);
        T result = op.WaitForCompletion();

        if (result != null)
        {
            if (!_handles.ContainsKey(key))
            {
                _handles.Add(key, op);
                _resources.Add(key, result);
            }
        }
        else
        {
            // 실패 시 핸들 해제
            Addressables.Release(op);
            Debug.LogError($"[ResourceManager] Load Failed: {key}");
        }

        return result;
    }
    #region IResourceProvider 구현

    /// <summary>
    /// 내부 캐시에 이미 로드되어 있는 리소스를 동기적으로 반환합니다.
    /// </summary>
    /// <remarks>
    /// - 비동기 로딩 과정 없이 즉시 리소스를 가져옵니다.<br/>
    /// - 만약 리소스가 로드되어 있지 않다면 null을 반환합니다.<br/>
    /// - 주로 사운드 재생이나 이펙트 생성 등 빈번하게 호출되는 로직에서 사용합니다.
    /// </remarks>
    /// <typeparam name="T">가져올 리소스의 타입</typeparam>
    /// <param name="key">리소스의 키 값</param>
    /// <returns>캐시된 리소스 원본(T). 찾지 못할 경우 null을 반환합니다.</returns>
    public T GetLoadedResource<T>(string key) where T : Object
    {
        if (_resources.TryGetValue(key, out Object resource))
        {
            return resource as T;
        }
        return null;
    }

    /// <summary>
    /// 어드레서블(Addressable) 시스템을 사용하여 리소스를 비동기적으로 로드합니다.
    /// </summary>
    /// <remarks>
    /// - 이미 로드된 리소스는 캐시에서 즉시 반환됩니다.<br/>
    /// - 로드가 완료되면 등록된 <paramref name="callback"/>이 실행됩니다.
    /// </remarks>
    /// <typeparam name="T">로드할 리소스의 타입 (예: GameObject, AudioClip, Material)</typeparam>
    /// <param name="key">어드레서블에 등록된 리소스의 주소(Address) 또는 키 값</param>
    /// <param name="callback">로드가 완료되었을 때 호출될 콜백 함수 (로드된 리소스를 인자로 받음)</param>
    public void LoadAsync<T>(string key, Action<T> callback = null) where T : Object
    {

        if (_resources.TryGetValue(key, out Object resource))
        {
            callback?.Invoke(resource as T);
            return;
        }

        var asyncOperation = Addressables.LoadAssetAsync<T>(key);

        asyncOperation.Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                if (!_resources.ContainsKey(key))
                {
                    _resources.Add(key, op.Result);
                }

                callback?.Invoke(op.Result);
            }
            else
            {
                Debug.LogError($"[ResourceManager] Failed to load asset. Key: {key}, Exception: {op.OperationException}");
                callback?.Invoke(null);
            }
        };
    }

    #endregion

    // LoadAllAsync >> 제가 레퍼런스 공유에 올려드린것처럼, 어드레서블이 특정 레이블 세팅(Preload) 라벨이름을 가지고 있는 모든 리소스를 비동기적으로 일괄 로드하는 기능입니다.
    #region Batch Loading

    /// <summary>
    /// 특정 레이블(Label)이 지정된 모든 리소스를 비동기적으로 일괄 로드합니다.
    /// </summary>
    /// <remarks>
    /// 맵 진입 전 로딩 화면 등에서 특정 그룹의 에셋들을 미리 메모리에 올려둘 때 사용합니다.
    /// </remarks>
    /// <typeparam name="T">로드할 리소스들의 공통 타입 (일반적으로 Object)</typeparam>
    /// <param name="label">어드레서블 그룹에 설정된 레이블 이름</param>
    /// <param name="progressCallback">
    /// 개별 리소스가 로드될 때마다 호출되는 콜백입니다.<br/>
    /// 매개변수: (string key, int currentCount, int totalCount)
    /// </param>
    public void LoadAllAsync<T>(string label, Action<string, int, int> progressCallback) where T : Object
    {
        var opHandle = Addressables.LoadResourceLocationsAsync(label, typeof(T));

        opHandle.Completed += (op) =>
        {
            if (op.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[ResourceManager] Failed to load locations for label: {label}");
                return;
            }

            var locations = op.Result;
            int totalCount = locations.Count;
            int currentCount = 0;

            if (totalCount == 0)
            {
                progressCallback?.Invoke("Complete", 0, 0);
                return;
            }

            foreach (var location in locations)
            {
                LoadAsync<T>(location.PrimaryKey, (obj) =>
                {
                    currentCount++;
                    progressCallback?.Invoke(location.PrimaryKey, currentCount, totalCount);
                });
            }
        };
    }

    #endregion

    #region 메모리 해제

    public void Release(string key)
    {
        if (_handles.TryGetValue(key, out var handle))
        {
            Addressables.Release(handle);
            _handles.Remove(key);
            _resources.Remove(key);
        }
    }

    public void ReleaseAll()
    {
        foreach (var handle in _handles.Values)
        {
            Addressables.Release(handle);
        }
        _handles.Clear();
        _resources.Clear();
    }

    #endregion

}
