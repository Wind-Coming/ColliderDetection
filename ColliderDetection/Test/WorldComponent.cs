using System.Collections;
using System.Collections.Generic;
using FarseerPhysics;
using Framework;
using Microsoft.Xna.Framework;
using UnityEngine;

public class WorldComponent : MonoSceneSingleton<WorldComponent>
{
    public Vector2Int min;
    public Vector2Int max;
    
    public World World;

    // Start is called before the first frame update
    protected override void Awake()
    {
        SingletonMgr.Initialize();
        base.Awake();
        AABB aabb = new AABB(new FVector2(min.x, min.y), new FVector2(max.x, max.y));
        World = new World(aabb);
    }

    // Update is called once per frame
    void Update()
    {
        World.Update();
    }
}
