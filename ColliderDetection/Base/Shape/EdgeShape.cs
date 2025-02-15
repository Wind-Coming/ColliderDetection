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

using Microsoft.Xna.Framework;

namespace FarseerPhysics
{
    /// <summary>
    /// A line segment (edge) Shape. These can be connected in chains or loops
    /// to other edge Shapes. The connectivity information is used to ensure
    /// correct contact normals.
    /// </summary>
    public class EdgeShape : Shape
    {
        public bool HasVertex0, HasVertex3;

        /// <summary>
        /// Optional adjacent vertices. These are used for smooth collision.
        /// </summary>
        public FVector2 Vertex0;

        /// <summary>
        /// Optional adjacent vertices. These are used for smooth collision.
        /// </summary>
        public FVector2 Vertex3;

        /// <summary>
        /// Edge start vertex
        /// </summary>
        internal FVector2 _vertex1;

        /// <summary>
        /// Edge end vertex
        /// </summary>
        internal FVector2 _vertex2;

        internal EdgeShape()
        {
            ShapeType = ShapeType.Edge;
            _radius = Settings.PolygonRadius;
        }

        public EdgeShape(FVector2 start, FVector2 end)
        {
            ShapeType = ShapeType.Edge;
            _radius = Settings.PolygonRadius;
            Set(start, end);
        }

        /// <summary>
        /// These are the edge vertices
        /// </summary>
        public FVector2 Vertex1
        {
            get { return _vertex1; }
            set
            {
                _vertex1 = value;
            }
        }

        /// <summary>
        /// These are the edge vertices
        /// </summary>
        public FVector2 Vertex2
        {
            get { return _vertex2; }
            set
            {
                _vertex2 = value;
            }
        }

        /// <summary>
        /// Set this as an isolated edge.
        /// </summary>
        /// <param name="start">The start.</param>
        /// <param name="end">The end.</param>
        public void Set(FVector2 start, FVector2 end)
        {
            _vertex1 = start;
            _vertex2 = end;
            HasVertex0 = false;
            HasVertex3 = false;
        }

        public override Shape Clone()
        {
            EdgeShape edge = new EdgeShape();
            edge._radius = _radius;
            edge.HasVertex0 = HasVertex0;
            edge.HasVertex3 = HasVertex3;
            edge.Vertex0 = Vertex0;
            edge._vertex1 = _vertex1;
            edge._vertex2 = _vertex2;
            edge.Vertex3 = Vertex3;
            return edge;
        }

        /// <summary>
        /// Test a point for containment in this shape. This only works for convex shapes.
        /// </summary>
        /// <param name="transform">The shape world transform.</param>
        /// <param name="point">a point in world coordinates.</param>
        /// <returns>True if the point is inside the shape</returns>
        public override bool TestPoint(ref Transform transform, ref FVector2 point)
        {
            return false;
        }

        /// <summary>
        /// Cast a ray against a child shape.
        /// </summary>
        /// <param name="output">The ray-cast results.</param>
        /// <param name="input">The ray-cast input parameters.</param>
        /// <param name="transform">The transform to be applied to the shape.</param>
        /// <param name="childIndex">The child shape index.</param>
        /// <returns>True if the ray-cast hits the shape</returns>
        public override bool RayCast(out RayCastOutput output, ref RayCastInput input,
                                     ref Transform transform, int childIndex)
        {
            // p = p1 + t * d
            // v = v1 + s * e
            // p1 + t * d = v1 + s * e
            // s * e - t * d = p1 - v1

            output = new RayCastOutput();

            // Put the ray into the edge's frame of reference.
            FVector2 p1 = MathUtils.MulT(transform.q, input.Point1 - transform.p);
            FVector2 p2 = MathUtils.MulT(transform.q, input.Point2 - transform.p);
            FVector2 d = p2 - p1;

            FVector2 v1 = _vertex1;
            FVector2 v2 = _vertex2;
            FVector2 e = v2 - v1;
            FVector2 normal = new FVector2(e.Y, -e.X);
            normal.Normalize();

            // q = p1 + t * d
            // dot(normal, q - v1) = 0
            // dot(normal, p1 - v1) + t * dot(normal, d) = 0
            float numerator = FVector2.Dot(normal, v1 - p1);
            float denominator = FVector2.Dot(normal, d);

            if (denominator == 0.0f)
            {
                return false;
            }

            float t = numerator / denominator;
            if (t < 0.0f || input.MaxFraction < t)
            {
                return false;
            }

            FVector2 q = p1 + t * d;

            // q = v1 + s * r
            // s = dot(q - v1, r) / dot(r, r)
            FVector2 r = v2 - v1;
            float rr = FVector2.Dot(r, r);
            if (rr == 0.0f)
            {
                return false;
            }

            float s = FVector2.Dot(q - v1, r) / rr;
            if (s < 0.0f || 1.0f < s)
            {
                return false;
            }

            output.Fraction = t;
            if (numerator > 0.0f)
            {
                output.Normal = -normal;
            }
            else
            {
                output.Normal = normal;
            }
            return true;
        }

        /// <summary>
        /// Given a transform, compute the associated axis aligned bounding box for a child shape.
        /// </summary>
        /// <param name="aabb">The aabb results.</param>
        /// <param name="transform">The world transform of the shape.</param>
        /// <param name="childIndex">The child shape index.</param>
        public override void ComputeAABB(out AABB aabb, ref Transform transform)
        {
            FVector2 v1 = MathUtils.Mul(ref transform, _vertex1);
            FVector2 v2 = MathUtils.Mul(ref transform, _vertex2);

            FVector2 lower = FVector2.Min(v1, v2);
            FVector2 upper = FVector2.Max(v1, v2);

            FVector2 r = new FVector2(Radius, Radius);
            aabb.LowerBound = lower - r;
            aabb.UpperBound = upper + r;
        }
        
        public override float ComputeSubmergedArea(FVector2 normal, float offset, Transform xf, out FVector2 sc)
        {
            sc = FVector2.Zero;
            return 0;
        }

        public bool CompareTo(EdgeShape shape)
        {
            return (HasVertex0 == shape.HasVertex0 &&
                    HasVertex3 == shape.HasVertex3 &&
                    Vertex0 == shape.Vertex0 &&
                    Vertex1 == shape.Vertex1 &&
                    Vertex2 == shape.Vertex2 &&
                    Vertex3 == shape.Vertex3);
        }
    }
}