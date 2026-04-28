using UnityEngine;

public class Target : MonoBehaviour, IShootable
{
    public Collider TakeHit()
    {
        return GetComponent<Collider>();
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
