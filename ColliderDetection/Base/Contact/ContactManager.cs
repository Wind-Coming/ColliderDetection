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

using System.Collections.Generic;
using UnityEngine;

namespace FarseerPhysics
{
    public class ContactManager
    {
        public int testA;
        public int testB;
        
        /// <summary>
        /// Fires when a contact is created
        /// </summary>
        public BeginContactDelegate BeginContact;

        public IBroadPhase BroadPhase;

        /// <summary>
        /// The filter used by the contact manager.
        /// </summary>
        public CollisionFilterDelegate ContactFilter;

        public List<Contact> ContactList = new List<Contact>(128);
        
#if USE_ACTIVE_CONTACT_SET
        /// <summary>
        /// The set of active contacts.
        /// </summary>
		public HashSet<Contact> ActiveContacts = new HashSet<Contact>();

        /// <summary>
        /// A temporary copy of active contacts that is used during updates so
		/// the hash set can have members added/removed during the update.
		/// This list is cleared after every update.
        /// </summary>
		List<Contact> ActiveList = new List<Contact>();
#endif

        /// <summary>
        /// Fires when a contact is deleted
        /// </summary>
        public EndContactDelegate EndContact;

        public EndContactDelegate RemoveContact;

        /// <summary>
        /// Fires when the broadphase detects that two Fixtures are close to each other.
        /// </summary>
        public BroadphaseDelegate OnBroadphaseCollision;
        

        /// <summary>
        /// Fires before the solver runs
        /// </summary>
        public PreSolveDelegate PreSolve;

        internal ContactManager(IBroadPhase broadPhase)
        {
            BroadPhase = broadPhase;
            OnBroadphaseCollision = AddPair;
        }

        // Broad-phase callback.
        private void AddPair(ref FixtureProxy proxyA, ref FixtureProxy proxyB)
        {
            Body fixtureA = proxyA.Body;
            Body fixtureB = proxyB.Body;
            
            Body bodyA = fixtureA;
            Body bodyB = fixtureB;

            // Are the fixtures on the same body?
            if (bodyA == bodyB)
            {
                return;
            }

            // Does a contact already exist?
            ContactEdge edge = bodyB.ContactList;
            while (edge != null)
            {
                if (edge.Other == bodyA)
                {
                    Body fA = edge.Contact.FixtureA;
                    Body fB = edge.Contact.FixtureB;

                    if (fA == fixtureA && fB == fixtureB)
                    {
                        // A contact already exists.
                        return;
                    }

                    if (fA == fixtureB && fB == fixtureA)
                    {
                        // A contact already exists.
                        return;
                    }
                }

                edge = edge.Next;
            }

            // Does a joint override collision? Is at least one body dynamic?
            if (bodyB.ShouldCollide(bodyA) == false)
                return;

            //Check default filter
            if (ShouldCollide(fixtureA, fixtureB) == false)
                return;

            // Check user filtering.
            if (ContactFilter != null && ContactFilter(fixtureA, fixtureB) == false)
                return;

            // Call the factory.
            Contact c = Contact.Create(fixtureA, fixtureB);

            // Contact creation may swap fixtures.
            fixtureA = c.FixtureA;
            fixtureB = c.FixtureB;
            bodyA = fixtureA;
            bodyB = fixtureB;

            // Insert into the world.
            ContactList.Add(c);

#if USE_ACTIVE_CONTACT_SET
			ActiveContacts.Add(c);
#endif
            // Connect to island graph.

            // Connect to body A
            c.NodeA.Contact = c;
            c.NodeA.Other = bodyB;

            c.NodeA.Prev = null;
            c.NodeA.Next = bodyA.ContactList;
            if (bodyA.ContactList != null)
            {
                bodyA.ContactList.Prev = c.NodeA;
            }
            bodyA.ContactList = c.NodeA;

            // Connect to body B
            c.NodeB.Contact = c;
            c.NodeB.Other = bodyA;

            c.NodeB.Prev = null;
            c.NodeB.Next = bodyB.ContactList;
            if (bodyB.ContactList != null)
            {
                bodyB.ContactList.Prev = c.NodeB;
            }
            bodyB.ContactList = c.NodeB;
        }

        internal void FindNewContacts()
        {
            BroadPhase.UpdatePairs(OnBroadphaseCollision);
        }

        internal void Destroy(Contact contact)
        {
            Body fixtureA = contact.FixtureA;
            Body fixtureB = contact.FixtureB;

            if (EndContact != null && contact.IsTouching())
            {
                EndContact(contact);
            }

            if (RemoveContact != null)
            {
                RemoveContact(contact);
            }

            if (contact.IsTouching())
            {
                contact.Separation();
            }

            // Remove from the world.
            ContactList.Remove(contact);

            // Remove from body 1
            if (contact.NodeA.Prev != null)
            {
                contact.NodeA.Prev.Next = contact.NodeA.Next;
            }

            if (contact.NodeA.Next != null)
            {
                contact.NodeA.Next.Prev = contact.NodeA.Prev;
            }

            if (contact.NodeA == fixtureA.ContactList)
            {
                fixtureA.ContactList = contact.NodeA.Next;
            }

            // Remove from body 2
            if (contact.NodeB.Prev != null)
            {
                contact.NodeB.Prev.Next = contact.NodeB.Next;
            }

            if (contact.NodeB.Next != null)
            {
                contact.NodeB.Next.Prev = contact.NodeB.Prev;
            }

            if (contact.NodeB == fixtureB.ContactList)
            {
                fixtureB.ContactList = contact.NodeB.Next;
            }

#if USE_ACTIVE_CONTACT_SET
			if (ActiveContacts.Contains(contact))
			{
				ActiveContacts.Remove(contact);
			}
#endif
			contact.Destroy();
        }

        internal void Collide()
        {
            // Update awake contacts.
#if USE_ACTIVE_CONTACT_SET
			ActiveList.AddRange(ActiveContacts);

			foreach (var c in ActiveList)
			{
#else
            for (int i = 0; i < ContactList.Count; i++)
            {
                Contact c = ContactList[i];
#endif
                Body fixtureA = c.FixtureA;
                Body fixtureB = c.FixtureB;
                
                if(fixtureA.Proxy.ProxyId % 2 != Time.frameCount % 2)//分帧
                    continue;
                
                if (fixtureA.Awake == false && fixtureB.Awake == false)
                {
#if USE_ACTIVE_CONTACT_SET
					ActiveContacts.Remove(c);
#endif
					continue;
                }

                // Is this contact flagged for filtering?
                if ((c.Flags & ContactFlags.Filter) == ContactFlags.Filter)
                {
                    // Should these bodies collide?
                    if (fixtureB.ShouldCollide(fixtureA) == false)
                    {
                        Contact cNuke = c;
                        Destroy(cNuke);
                        continue;
                    }

                    // Check default filtering
                    if (ShouldCollide(fixtureA, fixtureB) == false)
                    {
                        Contact cNuke = c;
                        Destroy(cNuke);
                        continue;
                    }

                    // Check user filtering.
                    if (ContactFilter != null && ContactFilter(fixtureA, fixtureB) == false)
                    {
                        Contact cNuke = c;
                        Destroy(cNuke);
                        continue;
                    }

                    // Clear the filtering flag.
                    c.Flags &= ~ContactFlags.Filter;
                }

                int proxyIdA = fixtureA.Proxy.ProxyId;
                int proxyIdB = fixtureB.Proxy.ProxyId;

                bool overlap = BroadPhase.TestOverlap(proxyIdA, proxyIdB);

                // Here we destroy contacts that cease to overlap in the broad-phase.
                if (overlap == false)
                {
                    Contact cNuke = c;
                    Destroy(cNuke);
                    continue;
                }

                // The contact persists.
                c.Update(this);
            }

#if USE_ACTIVE_CONTACT_SET
			ActiveList.Clear();
#endif
        }

        private static bool ShouldCollide(Body fixtureA, Body fixtureB)
        {
            bool collide = (fixtureA.CollidesWith & fixtureB.CollisionCategories) != 0 && (fixtureA.CollisionCategories & fixtureB.CollidesWith) != 0;
            
            return collide;
        }

		internal void UpdateContacts(ContactEdge contactEdge, bool value)
		{
#if USE_ACTIVE_CONTACT_SET
			if(value)
			{
				while(contactEdge != null)
				{
					var c = contactEdge.Contact;
					if (!ActiveContacts.Contains(c))
					{
						ActiveContacts.Add(c);
					}
					contactEdge = contactEdge.Next;
				}
			}
			else
			{
				while (contactEdge != null)
				{
					var c = contactEdge.Contact;
					if (!contactEdge.Other.Awake)
					{
						if (ActiveContacts.Contains(c))
						{
							ActiveContacts.Remove(c);
						}
					}
					contactEdge = contactEdge.Next;
				}
			}
#endif
		}

#if USE_ACTIVE_CONTACT_SET
		internal void RemoveActiveContact(Contact contact)
		{
			if (ActiveContacts.Contains(contact))
				ActiveContacts.Remove(contact);
		}
#endif
	}
}