/*
* Farseer Physics Engine:
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

using System;
using Microsoft.Xna.Framework;

namespace FarseerPhysics
{
    public class CircleShape : Shape
    {
        internal FVector2 _position;

        public CircleShape(float radius)
        {
            ShapeType = ShapeType.Circle;
            _radius = radius;
            _position = FVector2.Zero;
        }

        public CircleShape()
        {
            ShapeType = ShapeType.Circle;
            _radius = 0.0f;
            _position = FVector2.Zero;
        }

        public FVector2 Position
        {
            get { return _position; }
            set
            {
                _position = value;
            }
        }

        public override Shape Clone()
        {
            CircleShape shape = new CircleShape();
            shape._radius = Radius;
            shape._position = _position;
            shape.ShapeType = ShapeType;
            return shape;
        }

        /// <summary>
        /// Test a point for containment in this shape. This only works for convex shapes.
        /// </summary>
        /// <param name="transform">The shape world transform.</param>
        /// <param name="point">a point in world coordinates.</param>
        /// <returns>True if the point is inside the shape</returns>
        public override bool TestPoint(ref Transform transform, ref FVector2 point)
        {
            FVector2 center = transform.p + MathUtils.Mul(transform.q, Position);
            FVector2 d = point - center;
            return FVector2.Dot(d, d) <= Radius * Radius;
        }

        /// <summary>
        /// Cast a ray against a child shape.
        /// </summary>
        /// <param name="output">The ray-cast results.</param>
        /// <param name="input">The ray-cast input parameters.</param>
        /// <param name="transform">The transform to be applied to the shape.</param>
        /// <param name="childIndex">The child shape index.</param>
        /// <returns>True if the ray-cast hits the shape</returns>
        public override bool RayCast(out RayCastOutput output, ref RayCastInput input, ref Transform transform,
                                     int childIndex)
        {
            // Collision Detection in Interactive 3D Environments by Gino van den Bergen
            // From Section 3.1.2
            // x = s + a * r
            // norm(x) = radius

            output = new RayCastOutput();

            FVector2 position = transform.p + MathUtils.Mul(transform.q, Position);
            FVector2 s = input.Point1 - position;
            float b = FVector2.Dot(s, s) - Radius * Radius;

            // Solve quadratic equation.
            FVector2 r = input.Point2 - input.Point1;
            float c = FVector2.Dot(s, r);
            float rr = FVector2.Dot(r, r);
            float sigma = c * c - rr * b;

            // Check for negative discriminant and short segment.
            if (sigma < 0.0f || rr < Settings.Epsilon)
            {
                return false;
            }

            // Find the point of intersection of the line with the circle.
            float a = -(c + (float)Math.Sqrt(sigma));

            // Is the intersection point on the segment?
            if (0.0f <= a && a <= input.MaxFraction * rr)
            {
                a /= rr;
                output.Fraction = a;

                //TODO: Check results here
                output.Normal = s + a * r;
                output.Normal.Normalize();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Given a transform, compute the associated axis aligned bounding box for a child shape.
        /// </summary>
        /// <param name="aabb">The aabb results.</param>
        /// <param name="transform">The world transform of the shape.</param>
        /// <param name="childIndex">The child shape index.</param>
        public override void ComputeAABB(out AABB aabb, ref Transform transform)
        {
            FVector2 p = transform.p + MathUtils.Mul(transform.q, Position);
            aabb.LowerBound = new FVector2(p.X - Radius, p.Y - Radius);
            aabb.UpperBound = new FVector2(p.X + Radius, p.Y + Radius);
        }
        
        /// <summary>
        /// Compare the circle to another circle
        /// </summary>
        /// <param name="shape">The other circle</param>
        /// <returns>True if the two circles are the same size and have the same position</returns>
        public bool CompareTo(CircleShape shape)
        {
            return (Radius == shape.Radius && Position == shape.Position);
        }

        /// <summary>
        /// Method used by the BuoyancyController
        /// </summary>
        public override float ComputeSubmergedArea(FVector2 normal, float offset, Transform xf, out FVector2 sc)
        {
            sc = FVector2.Zero;

            FVector2 p = MathUtils.Mul(ref xf, Position);
            float l = -(FVector2.Dot(normal, p) - offset);
            if (l < -Radius + Settings.Epsilon)
            {
                //Completely dry
                return 0;
            }
            if (l > Radius)
            {
                //Completely wet
                sc = p;
                return Settings.Pi * Radius * Radius;
            }

            //Magic
            float r2 = Radius * Radius;
            float l2 = l * l;
            float area = r2 * (float)((Math.Asin(l / Radius) + Settings.Pi / 2) + l * Math.Sqrt(r2 - l2));
            float com = -2.0f / 3.0f * (float)Math.Pow(r2 - l2, 1.5f) / area;

            sc.X = p.X + normal.X * com;
            sc.Y = p.Y + normal.Y * com;

            return area;
        }
    }
}