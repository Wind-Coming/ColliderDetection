using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace FarseerPhysics
{
    public delegate void BroadphaseDelegate(ref FixtureProxy proxyA, ref FixtureProxy proxyB);

    public interface IBroadPhase
    {
        int ProxyCount { get; }
        void UpdatePairs(BroadphaseDelegate callback);

        bool TestOverlap(int proxyIdA, int proxyIdB);

        int AddProxy(ref FixtureProxy proxy);

        void RemoveProxy(int proxyId);

        void MoveProxy(int proxyId, ref AABB aabb, FVector2 displacement);

        FixtureProxy GetProxy(int proxyId);

        void TouchProxy(int proxyId);

        void GetFatAABB(int proxyId, out AABB aabb);

        void Query(Func<int, bool> callback, ref AABB aabb);

        void Query(List<FixtureProxy> list, ref AABB query);

        void RayCast(Func<RayCastInput, int, float> callback, ref RayCastInput input);
    }
}