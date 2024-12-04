using System;
using System.Collections.Generic;
using FarseerPhysics;
using UnityEngine;

namespace FarseerPhysics
{
    [Flags]
    public enum Category
    {
        None = 0,
        All = int.MaxValue,
        Unit = 1,
        Group = 2,
        Item = 4,
        Bullet = 8,
        SafeZone = 16,
        InnerCircle = 32,
        ViewCircle = 64,
        NeutralUnit = 128,
        MonsterUnit = 256,
        MapProp = 512,
        ZCat11 = 1024,
        ZCat12 = 2048,
        ZCat13 = 4096,
        ZCat14 = 8192,
        ZCat15 = 16384,
        ZCat16 = 32768,
        ZCat17 = 65536,
        ZCat18 = 131072,
        ZCat19 = 262144,
        ZCat20 = 524288,
        ZCat21 = 1048576,
        ZCat22 = 2097152,
        ZCat23 = 4194304,
        ZCat24 = 8388608,
        ZCat25 = 16777216,
        ZCat26 = 33554432,
        ZCat27 = 67108864,
        ZCat28 = 134217728,
        ZCat29 = 268435456,
        ZCat30 = 536870912,
        ZCat31 = 1073741824
    }

    /// <summary>
    /// This proxy is used internally to connect fixtures to the broad-phase.
    /// </summary>
    public struct FixtureProxy
    {
        public int ProxyId;
        public AABB AABB;
        public Body Body;
    }
    
    public class World
    {
        /// <summary>
        /// 缓存用
        /// </summary>
        internal Queue<Contact> ContactPool = new Queue<Contact>(256);

        public ContactManager ContactManager { get; private set; }
        public List<Body> BodyList { get; private set; }

        private HashSet<Body> _bodyAddList = new HashSet<Body>();
        private HashSet<Body> _bodyRemoveList = new HashSet<Body>();

        public World(AABB aabb)
        {
            QuadTreeBroadPhase quadTreeBroadPhase = new QuadTreeBroadPhase(aabb);
            //DynamicTreeBroadPhase quadTreeBroadPhase = new DynamicTreeBroadPhase();

            ContactManager = new ContactManager(quadTreeBroadPhase);

            BodyList = new List<Body>(64);
        }
        
        /// <summary>
        /// Add body.
        /// </summary>
        /// <returns></returns>
        public void AddBody(Body body)
        {
            Debug.Assert(!_bodyAddList.Contains(body), "You are adding the same body more than once.");

            if (!_bodyAddList.Contains(body))
                _bodyAddList.Add(body);

            body.World = this;
            body.CreateProxies(ContactManager.BroadPhase);
        }
        
        /// <summary>
        /// Destroy body.
        /// </summary>
        /// <param name="body">The body.</param>
        public void RemoveBody(Body body)
        {
            Debug.Assert(!_bodyRemoveList.Contains(body),
                "The body is already marked for removal. You are removing the body more than once.");
            
            if (!_bodyRemoveList.Contains(body))
                _bodyRemoveList.Add(body);
        }
        
        private void ProcessAddedBodies()
        {
            if (_bodyAddList.Count > 0)
            {
                foreach (Body body in _bodyAddList)
                {
                    // Add to world list.
                    BodyList.Add(body);
                }
                _bodyAddList.Clear();
            }
        }

        private void ProcessRemovedBodies()
        {
            if (_bodyRemoveList.Count > 0)
            {
                foreach (Body body in _bodyRemoveList)
                {
                    Debug.Assert(BodyList.Count > 0);

                    // You tried to remove a body that is not contained in the BodyList.
                    // Are you removing the body more than once?
                    Debug.Assert(BodyList.Contains(body));
                    
                    // Delete the attached contacts.
                    ContactEdge ce = body.ContactList;
                    while (ce != null)
                    {
                        ContactEdge ce0 = ce;
                        ce = ce.Next;
                        ContactManager.Destroy(ce0.Contact);
                    }
                    body.ContactList = null;

                    // Delete the attached fixtures. This destroys broad-phase proxies.
                    body.DestroyProxies(ContactManager.BroadPhase);
                    body.Destroy();

                    body.World = null;
                    
                    // Remove world body list.
                    BodyList.Remove(body);

#if USE_AWAKE_BODY_SET
                    Debug.Assert(!AwakeBodySet.Contains(body));
#endif
                }

                _bodyRemoveList.Clear();
            }
        }

        public void RayCast(Shape shape, Transform xf, ref List<FixtureProxy> outList, Category category = Category.All)
        {
            AABB aabb1;
            shape.ComputeAABB(out aabb1, ref xf);

            outList.Clear();
            ContactManager.BroadPhase.Query(outList, ref aabb1);
            for (int i = 0; i < outList.Count; i++)
            {
                Body body = outList[i].Body;

                if ((body.CollisionCategories & category) == Category.None)
                {
                    outList.RemoveAt(i);
                    i--;
                    continue;
                }
                Manifold manifold = new Manifold();

                Transform bodyxf = body.Xf;
                
                Contact.Evaluate(ref manifold, shape, body.Shape, ref xf, ref bodyxf);

                if (manifold.PointCount == 0)
                {
                    outList.RemoveAt(i);
                    i--;
                }
            }
        }
        
        public void Update()
        {
            ProcessAddedBodies();

            ProcessRemovedBodies();
            
            ContactManager.FindNewContacts();
            ContactManager.Collide();
        }
    }
}