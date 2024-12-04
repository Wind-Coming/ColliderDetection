using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GenerateBody : MonoBehaviour
{
    public int num = 1000;
    
    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i < num; i++)
        {
            GameObject g = new GameObject(i.ToString());
            g.transform.position = new Vector3(Random.Range(0, 32), Random.Range(0, 32), 0);
            BodyComponent body = g.AddComponent<BodyComponent>();
            body.radius = Random.Range(0.1f, 1);
            body.operate = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
