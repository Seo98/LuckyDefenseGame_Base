using UnityEngine;

public class Managers : MonoBehaviour
{
    private static bool s_initialized;
    private static Managers s_instance;
    public static Managers Instance { get { Init(); return s_instance; } }


    private ResourceManager _resource = new ResourceManager();
    public static ResourceManager Resource { get { return Instance?._resource; } }

    private SoundManager _sound = new SoundManager();
    public static SoundManager Sound { get { return Instance._sound; } }


    public void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        DontDestroyOnLoad(gameObject);

        _sound.Init();
    }


    public static void Init()
    {
        if (s_instance == null)
        {
            GameObject go = GameObject.Find("@Managers");
            if (go == null)
            {
                go = new GameObject { name = "@Managers" };
                go.AddComponent<Managers>();
            }

            if (s_instance == null) s_instance = go.GetComponent<Managers>();

            DontDestroyOnLoad(go);
        }
    }
}
