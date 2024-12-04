using System.Collections.Generic;
using Microsoft.Xna.Framework;
using UnityEngine;

namespace FarseerPhysics
{
    public class Body
    {
        public World World;
        public Shape Shape;
        public bool Awake;
        public Transform Xf;

        public FixtureProxy Proxy;

        public object UserData;
        
        public ContactEdge ContactList { get; internal set; }

        public OnCollisionEventHandler OnCollision;
        public OnSeparationEventHandler OnSeparation;

        internal Category _collidesWith;

        /// <summary>
        /// Defaults to Category.All
        /// 
        /// The collision mask bits. This states the categories that this
        /// fixture would accept for collision.
        /// Use Settings.UseFPECollisionCategories to change the behavior.
        /// </summary>
        public Category CollidesWith
        {
            get { return _collidesWith; }

            set
            {
                if (_collidesWith == value)
                    return;

                _collidesWith = value;
                Refilter();
            }
        }
        
        /// <summary>
        /// Get the type of the child Shape. You can use this to down cast to the concrete Shape.
        /// </summary>
        /// <value>The type of the shape.</value>
        public ShapeType ShapeType
        {
            get { return Shape.ShapeType; }
        }
        
        internal Category _collisionCategories;

        /// <summary>
        /// The collision categories this fixture is a part of.
        /// 
        /// If Settings.UseFPECollisionCategories is set to false:
        /// Defaults to Category.Cat1
        /// 
        /// If Settings.UseFPECollisionCategories is set to true:
        /// Defaults to Category.All
        /// </summary>
        public Category CollisionCategories
        {
            get { return _collisionCategories; }

            set
            {
                if (_collisionCategories == value)
                    return;

                _collisionCategories = value;
                Refilter();
            }
        }
        
        /// <summary>
        /// Get the world body origin position.
        /// </summary>
        /// <returns>Return the world position of the body's origin.</returns>
        public FVector2 Position
        {
            get { return Xf.p; }
            set
            {
                Debug.Assert(!float.IsNaN(value.X) && !float.IsNaN(value.Y));

                SetTransform(ref value, Rotation);
            }
        }

        /// <summary>
        /// Get the angle in radians.
        /// </summary>
        /// <returns>Return the current world rotation angle in radians.</returns>
        public float Rotation
        {
            get { return Xf.q.GetAngle(); }
            set
            {
                Debug.Assert(!float.IsNaN(value));

                SetTransform(ref Xf.p, value);
            }
        }
        
        public Body()
        {
            Awake = true;

            _collisionCategories = Settings.DefaultFixtureCollisionCategories;
            _collidesWith = Category.All;
            Xf.q.Set(0);
        }
        
        /// <summary>
        /// Contacts are persistant and will keep being persistant unless they are
        /// flagged for filtering.
        /// This methods flags all contacts associated with the body for filtering.
        /// </summary>
        internal void Refilter()
        {
            // Flag associated contacts for filtering.
            ContactEdge edge = ContactList;
            while (edge != null)
            {
                Contact contact = edge.Contact;
                Body fixtureA = contact.FixtureA;
                Body fixtureB = contact.FixtureB;
                if (fixtureA == this || fixtureB == this)
                {
                    contact.FlagForFiltering();
                }

                edge = edge.Next;
            }
            
            if (World == null)
            {
                return;
            }

            // Touch each proxy so that new pairs may be created
            IBroadPhase broadPhase = World.ContactManager.BroadPhase;
            broadPhase.TouchProxy(Proxy.ProxyId);
        }
        
        /// <summary>
        /// Set the position of the body's origin and rotation.
        /// This breaks any contacts and wakes the other bodies.
        /// Manipulating a body's transform may cause non-physical behavior.
        /// </summary>
        /// <param name="position">The world position of the body's local origin.</param>
        /// <param name="rotation">The world rotation in radians.</param>
        public void SetPos(FVector2 position)
        {
            SetTransformIgnoreContacts(ref position, 0);
        }
        
        /// <summary>
        /// Set the position of the body's origin and rotation.
        /// This breaks any contacts and wakes the other bodies.
        /// Manipulating a body's transform may cause non-physical behavior.
        /// </summary>
        /// <param name="position">The world position of the body's local origin.</param>
        /// <param name="rotation">The world rotation in radians.</param>
        public void SetTransform(ref FVector2 position, float rotation)
        {
            SetTransformIgnoreContacts(ref position, rotation);
        }
        
        /// <summary>
        /// For teleporting a body without considering new contacts immediately.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <param name="angle">The angle.</param>
        public void SetTransformIgnoreContacts(ref FVector2 position, float angle)
        {
            Xf.q.Set(angle);
            Xf.p = position;
            
            IBroadPhase broadPhase = World.ContactManager.BroadPhase;
            Synchronize(broadPhase, ref Xf);
        }
        
        internal void Synchronize(IBroadPhase broadPhase, ref Transform transform1)
        {
            // Compute an AABB that covers the swept Shape (may miss some rotation effect).
            AABB aabb1;
            Shape.ComputeAABB(out aabb1, ref transform1);

            Proxy.AABB = aabb1;
            
            broadPhase.MoveProxy(Proxy.ProxyId, ref Proxy.AABB, FVector2.Zero);
        }
        
        
        // These support body activation/deactivation.
        internal void CreateProxies(IBroadPhase broadPhase)
        {
            Proxy = new FixtureProxy();
            Shape.ComputeAABB(out Proxy.AABB, ref Xf);
            Proxy.Body = this;

            //FPE note: This line needs to be after the previous two because FixtureProxy is a struct
            Proxy.ProxyId = broadPhase.AddProxy(ref Proxy);
        }
        
        internal void DestroyProxies(IBroadPhase broadPhase)
        {
            broadPhase.RemoveProxy(Proxy.ProxyId);
            Proxy.ProxyId = -1;
        }

        internal void Destroy()
        {
            
        }
        
        public bool ShouldCollide(Body other)
        {
            return true;
        }
    }
}