using UnityEngine;

public class LocalPlayerEnabler : MonoBehaviour
{
    public GameObject obj;
    [SerializeField] public bool visible;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (visible)
        {
            obj.SetActive(true);
        }
        else
        {
            obj.SetActive(false);
        }
        
    }
}
