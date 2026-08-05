using UnityEngine;

public class StreamManager : MonoBehaviour
{
    public static StreamManager Instance { get; private set; }

    [SerializeField]
    private bool shouldPersist = false;

    void Awake()
    {
        if (Instance) { DestroyImmediate(gameObject); return; }
        Instance = this;
        if (shouldPersist) DontDestroyOnLoad(gameObject);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
