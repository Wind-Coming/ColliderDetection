/*
* Farseer Physics Engine based on Box2D.XNA port:
* Copyright (c) 2011 Ian Qvist
* 
* Original source Box2D:
* Copyright (c) 2006-2011 Erin Catto http://www.box2d.org 
* 
* This software is provided 'as-is', without any express or implied 
* warranty.  In no event will the authors be held liable for any damages 
* arising from the use of this software. 
* Permission is granted to anyone to use this software for any purpose, 
* including commercial applications, and to alter it and redistribute it 
* freely, subject to the following restrictions: 
* 1. The origin of this software must not be misrepresented; you must not 
* claim that you wrote the original software. If you use this software 
* in a product, an acknowledgment in the product documentation would be 
* appreciated but is not required. 
* 2. Altered source versions must be plainly marked as such, and must not be 
* misrepresented as being the original software. 
* 3. This notice may not be removed or altered from any source distribution. 
*/
//#define USE_ACTIVE_CONTACT_SET

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace FarseerPhysics
{
    /// <summary>
    /// A contact edge is used to connect bodies and contacts together
    /// in a contact graph where each body is a node and each contact
    /// is an edge. A contact edge belongs to a doubly linked list
    /// maintained in each attached body. Each contact has two contact
    /// nodes, one for each attached body.
    /// </summary>
    public sealed class ContactEdge
    {
        /// <summary>
        /// The contact
        /// </summary>
        public Contact Contact;

        /// <summary>
        /// The next contact edge in the body's contact list
        /// </summary>
        public ContactEdge Next;

        /// <summary>
        /// Provides quick access to the other body attached.
        /// </summary>
        public Body Other;

        /// <summary>
        /// The previous contact edge in the body's contact list
        /// </summary>
        public ContactEdge Prev;
    }
    
    [Flags]
    public enum ContactFlags
    {
        None = 0,

        /// <summary>
        /// Used when crawling contact graph when forming islands.
        /// </summary>
        Island = 0x0001,

        /// <summary>
        /// Set when the shapes are touching.
        /// </summary>
        Touching = 0x0002,

        /// <summary>
        /// This contact can be disabled (by user)
        /// </summary>
        Enabled = 0x0004,

        /// <summary>
        /// This contact needs filtering because a fixture filter was changed.
        /// </summary>
        Filter = 0x0008,

        /// <summary>
        /// This bullet contact had a TOI event
        /// </summary>
        BulletHit = 0x0010,

        /// <summary>
        /// This contact has a valid TOI i the field TOI
        /// </summary>
        TOI = 0x0020
    }

    /// <summary>
    /// The class manages contact between two shapes. A contact exists for each overlapping
    /// AABB in the broad-phase (except if filtered). Therefore a contact object may exist
    /// that has no contact points.
    /// </summary>
    public class Contact
    {
        private static EdgeShape _edge = new EdgeShape();

        private static ContactType[,] _registers = new[,]
                                                       {
                                                           {
                                                               ContactType.Circle,
                                                               ContactType.EdgeAndCircle,
                                                               ContactType.PolygonAndCircle,
                                                               ContactType.ChainAndCircle,
                                                           },
                                                           {
                                                               ContactType.EdgeAndCircle,
                                                               ContactType.NotSupported,
                                                               // 1,1 is invalid (no ContactType.Edge)
                                                               ContactType.EdgeAndPolygon,
                                                               ContactType.NotSupported,
                                                               // 1,3 is invalid (no ContactType.EdgeAndLoop)
                                                           },
                                                           {
                                                               ContactType.PolygonAndCircle,
                                                               ContactType.EdgeAndPolygon,
                                                               ContactType.Polygon,
                                                               ContactType.ChainAndPolygon,
                                                           },
                                                           {
                                                               ContactType.ChainAndCircle,
                                                               ContactType.NotSupported,
                                                               // 3,1 is invalid (no ContactType.EdgeAndLoop)
                                                               ContactType.ChainAndPolygon,
                                                               ContactType.NotSupported,
                                                               // 3,3 is invalid (no ContactType.Loop)
                                                           },
                                                       };

        public Body FixtureA;
        public Body FixtureB;
        internal ContactFlags Flags;
        
        /// Get or set the desired tangent speed for a conveyor belt behavior. In meters per second.

        public Manifold Manifold;
        
        // Nodes for connecting bodies.
        internal ContactEdge NodeA = new ContactEdge();
        internal ContactEdge NodeB = new ContactEdge();
        
        private ContactType _type;

        private Contact(Body fA, Body fB)
        {
            Reset(fA, fB);
        }

        /// Enable/disable this contact. This can be used inside the pre-solve
        /// contact listener. The contact is only disabled for the current
        /// time step (or sub-step in continuous collisions).
        /// NOTE: If you are setting Enabled to a constant true or false,
        /// use the explicit Enable() or Disable() functions instead to 
        /// save the CPU from doing a branch operation.
        public bool Enabled
        {
            set
            {
                if (value)
                {
                    Flags |= ContactFlags.Enabled;
                }
                else
                {
                    Flags &= ~ContactFlags.Enabled;
                }
            }

            get { return (Flags & ContactFlags.Enabled) == ContactFlags.Enabled; }
        }

        /// <summary>
        /// Enable this contact.
        /// </summary>
        public void Enable()
        {
            Flags |= ContactFlags.Enabled;
        }

        /// <summary>
        /// Disable this contact.
        /// </summary>
        public void Disable()
        {
            Flags &= ~ContactFlags.Enabled;
        }

        /// <summary>
        /// Get the contact manifold. Do not modify the manifold unless you understand the
        /// internals of Box2D.
        /// </summary>
        /// <param name="manifold">The manifold.</param>
        public void GetManifold(out Manifold manifold)
        {
            manifold = Manifold;
        }

        /// <summary>
        /// Determines whether this contact is touching.
        /// </summary>
        /// <returns>
        /// 	<c>true</c> if this instance is touching; otherwise, <c>false</c>.
        /// </returns>
        public bool IsTouching()
        {
            return (Flags & ContactFlags.Touching) == ContactFlags.Touching;
        }

        /// <summary>
        /// Flag this contact for filtering. Filtering will occur the next time step.
        /// </summary>
        public void FlagForFiltering()
        {
            Flags |= ContactFlags.Filter;
        }

        private void Reset(Body fA, Body fB)
        {
            Flags = ContactFlags.Enabled;

            FixtureA = fA;
            FixtureB = fB;

            NodeA.Contact = null;
            NodeA.Prev = null;
            NodeA.Next = null;
            NodeA.Other = null;

            NodeB.Contact = null;
            NodeB.Prev = null;
            NodeB.Next = null;
            NodeB.Other = null;
            
            Manifold.PointCount = 0;
        }

        /// <summary>
        /// Update the contact manifold and touching status.
        /// Note: do not assume the fixture AABBs are overlapping or are valid.
        /// </summary>
        /// <param name="contactManager">The contact manager.</param>
        internal void Update(ContactManager contactManager)
        {
            Manifold oldManifold = Manifold;

            // Re-enable this contact.
            Flags |= ContactFlags.Enabled;

            bool touching;
            bool wasTouching = (Flags & ContactFlags.Touching) == ContactFlags.Touching;


            Body bodyA = FixtureA;
            Body bodyB = FixtureB;
            
            {
                Evaluate(ref Manifold, ref bodyA.Xf, ref bodyB.Xf);
                touching = Manifold.PointCount > 0;

                // Match old contact ids to new contact ids and copy the
                // stored impulses to warm start the solver.
                for (int i = 0; i < Manifold.PointCount; ++i)
                {
                    ManifoldPoint mp2 = Manifold.Points[i];
                    mp2.NormalImpulse = 0.0f;
                    mp2.TangentImpulse = 0.0f;
                    ContactID id2 = mp2.Id;

                    for (int j = 0; j < oldManifold.PointCount; ++j)
                    {
                        ManifoldPoint mp1 = oldManifold.Points[j];

                        if (mp1.Id.Key == id2.Key)
                        {
                            mp2.NormalImpulse = mp1.NormalImpulse;
                            mp2.TangentImpulse = mp1.TangentImpulse;
                            break;
                        }
                    }

                    Manifold.Points[i] = mp2;
                }

                if (touching != wasTouching)
                {
                    bodyA.Awake = true;
                    bodyB.Awake = true;
                }
            }

            if (touching)
            {
                Flags |= ContactFlags.Touching;
            }
            else
            {
                Flags &= ~ContactFlags.Touching;
            }

            if (wasTouching == false)
            {
                if (touching)
                {

#if true
                    bool enabledA, enabledB;

                    // Report the collision to both participants. Track which ones returned true so we can
                    // later call OnSeparation if the contact is disabled for a different reason.
                    if (FixtureA.OnCollision != null)
                        enabledA = FixtureA.OnCollision(FixtureA, FixtureB, this);
                    else
                        enabledA = true;

                    // Reverse the order of the reported fixtures. The first fixture is always the one that the
                    // user subscribed to.
                    if (FixtureB.OnCollision != null)
                        enabledB = FixtureB.OnCollision(FixtureB, FixtureA, this);
                    else
                        enabledB = true;

                    Enabled = enabledA && enabledB;

                    // BeginContact can also return false and disable the contact
                    if (enabledA && enabledB && contactManager.BeginContact != null)
                    {
                        Enabled = contactManager.BeginContact(this);
                    }

                    // If the user disabled the contact (needed to exclude it in TOI solver) at any point by
                    // any of the callbacks, we need to mark it as not touching and call any separation
                    // callbacks for fixtures that didn't explicitly disable the collision.
                    if (!Enabled)
                    {
                        Flags &= ~ContactFlags.Touching;
                    }

#else
					//Report the collision to both participants:
					if (FixtureA.OnCollision != null)
						Enabled = FixtureA.OnCollision(FixtureA, FixtureB, this);

					//Reverse the order of the reported fixtures. The first fixture is always the one that the
					//user subscribed to.
					if (FixtureB.OnCollision != null)
						Enabled = FixtureB.OnCollision(FixtureB, FixtureA, this);

					//BeginContact can also return false and disable the contact
					if (contactManager.BeginContact != null)
						Enabled = contactManager.BeginContact(this);

					//if the user disabled the contact (needed to exclude it in TOI solver), we also need to mark
					//it as not touching.
					if (Enabled == false)
						Flags &= ~ContactFlags.Touching;
#endif
                }
            }
            else
            {
                if (touching == false)
                {
                    Separation();

                    if (contactManager.EndContact != null)
                        contactManager.EndContact(this);
                }
            }
        }

        /// <summary>
        /// 分开
        /// </summary>
        public void Separation()
        {
            //Report the separation to both participants:
            if (FixtureA != null && FixtureA.OnSeparation != null)
                FixtureA.OnSeparation(FixtureA, FixtureB);

            //Reverse the order of the reported fixtures. The first fixture is always the one that the
            //user subscribed to.
            if (FixtureB != null && FixtureB.OnSeparation != null)
                FixtureB.OnSeparation(FixtureB, FixtureA);
        }

        /// <summary>
        /// Evaluate this contact with your own manifold and transforms.   
        /// </summary>
        /// <param name="manifold">The manifold.</param>
        /// <param name="transformA">The first transform.</param>
        /// <param name="transformB">The second transform.</param>
        private void Evaluate(ref Manifold manifold, ref Transform transformA, ref Transform transformB)
        {
            switch (_type)
            {
                case ContactType.Polygon:
                    Collision.CollidePolygons(ref manifold,
                                                        (PolygonShape)FixtureA.Shape, ref transformA,
                                                        (PolygonShape)FixtureB.Shape, ref transformB);
                    break;
                case ContactType.PolygonAndCircle:
                    Collision.CollidePolygonAndCircle(ref manifold,
                                                                (PolygonShape)FixtureA.Shape, ref transformA,
                                                                (CircleShape)FixtureB.Shape, ref transformB);
                    break;
                case ContactType.EdgeAndCircle:
                    Collision.CollideEdgeAndCircle(ref manifold,
                                                             (EdgeShape)FixtureA.Shape, ref transformA,
                                                             (CircleShape)FixtureB.Shape, ref transformB);
                    break;
                case ContactType.EdgeAndPolygon:
                    Collision.CollideEdgeAndPolygon(ref manifold,
                                                              (EdgeShape)FixtureA.Shape, ref transformA,
                                                              (PolygonShape)FixtureB.Shape, ref transformB);
                    break;

                case ContactType.Circle:
                    Collision.CollideCircles(ref manifold,
                                                       (CircleShape)FixtureA.Shape, ref transformA,
                                                       (CircleShape)FixtureB.Shape, ref transformB);
                    break;
            }
        }

        /// <summary>
        /// Evaluate this contact with your own manifold and transforms.   
        /// </summary>
        /// <param name="manifold">The manifold.</param>
        /// <param name="shapeB"></param>
        /// <param name="transformA">The first transform.</param>
        /// <param name="transformB">The second transform.</param>
        /// <param name="shapeA"></param>
        public static void Evaluate(ref Manifold manifold, Shape shapeA, Shape shapeB, ref Transform transformA, ref Transform transformB)
        {
            ShapeType type1 = shapeA.ShapeType;
            ShapeType type2 = shapeB.ShapeType;

            ContactType type = _registers[(int)type1, (int)type2];

            switch (type)
            {
                case ContactType.Polygon:
                    Collision.CollidePolygons(ref manifold,
                                                        (PolygonShape)shapeA, ref transformA,
                                                        (PolygonShape)shapeB, ref transformB);
                    break;
                case ContactType.PolygonAndCircle:
                    Collision.CollidePolygonAndCircle(ref manifold,
                                                                (PolygonShape)shapeA, ref transformA,
                                                                (CircleShape)shapeB, ref transformB);
                    break;
                case ContactType.EdgeAndCircle:
                    Collision.CollideEdgeAndCircle(ref manifold,
                                                             (EdgeShape)shapeA, ref transformA,
                                                             (CircleShape)shapeB, ref transformB);
                    break;
                case ContactType.EdgeAndPolygon:
                    Collision.CollideEdgeAndPolygon(ref manifold,
                                                              (EdgeShape)shapeA, ref transformA,
                                                              (PolygonShape)shapeB, ref transformB);
                    break;

                case ContactType.Circle:
                    Collision.CollideCircles(ref manifold,
                                                       (CircleShape)shapeA, ref transformA,
                                                       (CircleShape)shapeB, ref transformB);
                    break;
            }
        }

        internal static Contact Create(Body fixtureA, Body fixtureB)
        {
            ShapeType type1 = fixtureA.ShapeType;
            ShapeType type2 = fixtureB.ShapeType;

            Debug.Assert(ShapeType.Unknown < type1 && type1 < ShapeType.TypeCount);
            Debug.Assert(ShapeType.Unknown < type2 && type2 < ShapeType.TypeCount);

            Contact c;
            Queue<Contact> pool = fixtureA.World.ContactPool;
            if (pool.Count > 0)
            {
                c = pool.Dequeue();
                if ((type1 >= type2 || (type1 == ShapeType.Edge && type2 == ShapeType.Polygon))
                    &&
                    !(type2 == ShapeType.Edge && type1 == ShapeType.Polygon))
                {
                    c.Reset(fixtureA, fixtureB);
                }
                else
                {
                    c.Reset(fixtureB, fixtureA);
                }
            }
            else
            {
                // Edge+Polygon is non-symetrical due to the way Erin handles collision type registration.
                if ((type1 >= type2 || (type1 == ShapeType.Edge && type2 == ShapeType.Polygon))
                    &&
                    !(type2 == ShapeType.Edge && type1 == ShapeType.Polygon))
                {
                    c = new Contact(fixtureA, fixtureB);
                }
                else
                {
                    c = new Contact(fixtureB, fixtureA);
                }
            }

            c._type = _registers[(int)type1, (int)type2];

            return c;
        }

        internal void Destroy()
        {
#if USE_ACTIVE_CONTACT_SET
            FixtureA.Body.World.ContactManager.RemoveActiveContact(this);
#endif
            FixtureA.World.ContactPool.Enqueue(this);
            Reset(null, null);
        }

        #region Nested type: ContactType

        private enum ContactType
        {
            NotSupported,
            Polygon,
            PolygonAndCircle,
            Circle,
            EdgeAndPolygon,
            EdgeAndCircle,
            ChainAndPolygon,
            ChainAndCircle,
        }

        #endregion
    }
}