using Microsoft.Xna.Framework;
using UnityEngine;

namespace FarseerPhysics
{
    public static class Extensions
    {
        public static Vector3 ToVector3(this FVector2 v)
        {
            return new Vector3(v.X, 0, v.Y);
        }
        
        public static FVector2 ToFVector2(this Vector3 v)
        {
            return new FVector2(v.x, v.z);
        }
        
        public static FVector2 ToFVector2Y(this Vector3 v)
        {
            return new FVector2(v.x, v.y);
        }
        
        public static Vector2 ToVector2(this FVector2 v)
        {
            return new Vector2(v.X, v.Y);
        }
        
        public static FVector2 ToFVector2(this Vector2 v)
        {
            return new FVector2(v.x, v.y);
        }
    }
}