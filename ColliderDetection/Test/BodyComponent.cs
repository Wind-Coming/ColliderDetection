using System;
using System.Collections;
using System.Collections.Generic;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using Sirenix.OdinInspector;
using UnityEngine;

public class BodyComponent : MonoBehaviour
{
    [ShowInInspector]
    public Body Body;
    public float radius = 1;
    public bool operate = false;

    void Start()
    {
        Body = new Body();
        Body.Shape = new CircleShape(radius);
        Body.OnCollision = OnCollision;
        Body.OnSeparation = OnSeparation;
        Body.CollisionCategories = Category.Unit;
        WorldComponent.Inst.World.AddBody(Body);
        Body.SetPos(transform.position.ToFVector2Y());
    }

    private void OnSeparation(Body fixturea, Body fixtureb)
    {
        Debug.Log($"OnSeparation{fixturea.Proxy.ProxyId}----{fixtureb.Proxy.ProxyId}");
    }

    private bool OnCollision(Body fixturea, Body fixtureb, Contact contact)
    {
        Debug.Log($"OnCollision{fixturea.Proxy.ProxyId}----{fixtureb.Proxy.ProxyId}");
        return true;
    }

    private void Update()
    {
        if(!operate)
            return;
        
        FVector2 v = FVector2.Zero;
        if (Input.GetKey(KeyCode.A))
        {
            v -= FVector2.UnitX;
        }
        
        if (Input.GetKey(KeyCode.D))
        {
            v += FVector2.UnitX;
        }
        
        if (Input.GetKey(KeyCode.W))
        {
            v += FVector2.UnitY;
        }
        
        if (Input.GetKey(KeyCode.S))
        {
            v -= FVector2.UnitY;
        }

        transform.position += new Vector3(v.X, v.Y, 0) * Time.deltaTime * 5;
        FVector2 fv = new FVector2(transform.position.x, transform.position.y);
        Body.SetTransform(ref fv, 0);
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            List<FixtureProxy> list = new List<FixtureProxy>();
            WorldComponent.Inst.World.RayCast(Body.Shape, Body.Xf, ref list);
            foreach (var fi in list)
            {
                Debug.Log(fi.ProxyId);
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawSphere(transform.position, radius);
    }
}
