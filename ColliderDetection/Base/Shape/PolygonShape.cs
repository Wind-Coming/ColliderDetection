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

using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace FarseerPhysics
{
    /// <summary>
    /// Represents a simple non-selfintersecting convex polygon.
    /// Create a convex hull from the given array of points.
    /// </summary>
    public class PolygonShape : Shape
    {
        public Vertices Normals;
        public Vertices Vertices;

        /// <summary>
        /// Initializes a new instance of the <see cref="PolygonShape"/> class.
        /// </summary>
        /// <param name="vertices">The vertices.</param>
        /// <param name="density">The density.</param>
        public PolygonShape(Vertices vertices)
        {
            ShapeType = ShapeType.Polygon;
            _radius = Settings.PolygonRadius;

            Set(vertices);
        }

        internal PolygonShape()
        {
            ShapeType = ShapeType.Polygon;
            _radius = Settings.PolygonRadius;
            Normals = new Vertices(Settings.MaxPolygonVertices);
            Vertices = new Vertices(Settings.MaxPolygonVertices);
        }
        
        public override Shape Clone()
        {
            PolygonShape clone = new PolygonShape();
            clone.ShapeType = ShapeType;
            clone._radius = _radius;

            if (Settings.ConserveMemory)
            {
                clone.Vertices = Vertices;
                clone.Normals = Normals;
            }
            else
            {
                clone.Vertices = new Vertices(Vertices);
                clone.Normals = new Vertices(Normals);
            }

            return clone;
        }

        /// <summary>
        /// Copy vertices. This assumes the vertices define a convex polygon.
        /// It is assumed that the exterior is the the right of each edge.
        /// </summary>
        /// @warning the points may be re-ordered, even if they form a convex polygon
        /// @warning collinear points are handled but not removed. Collinear points
        /// may lead to poor stacking behavior.
        /// <param name="input">The vertices.</param>
        public void Set(Vertices input)
        {
            Debug.Assert(input.Count >= 3 && input.Count <= Settings.MaxPolygonVertices);

            //TODO: Uncomment and remove the other line
            //Vertices = GiftWrap.GetConvexHull(input);
            Vertices = new Vertices(input);
            Normals = new Vertices(Vertices.Count);

            // Compute normals. Ensure the edges have non-zero length.
            for (int i = 0; i < Vertices.Count; ++i)
            {
                int i1 = i;
                int i2 = i + 1 < Vertices.Count ? i + 1 : 0;
                FVector2 edge = Vertices[i2] - Vertices[i1];
                Debug.Assert(edge.LengthSquared() > Settings.Epsilon * Settings.Epsilon);

                FVector2 temp = new FVector2(edge.Y, -edge.X);
                temp.Normalize();
                Normals.Add(temp);
            }
        }
        
        /// <summary>
        /// Build vertices to represent an axis-aligned box.
        /// </summary>
        /// <param name="halfWidth">The half-width.</param>
        /// <param name="halfHeight">The half-height.</param>
        public void SetAsBox(float halfWidth, float halfHeight)
        {
            Set(PolygonTools.CreateRectangle(halfWidth, halfHeight));
        }

        /// <summary>
        /// Build vertices to represent an oriented box.
        /// </summary>
        /// <param name="halfWidth">The half-width..</param>
        /// <param name="halfHeight">The half-height.</param>
        /// <param name="center">The center of the box in local coordinates.</param>
        /// <param name="angle">The rotation of the box in local coordinates.</param>
        public void SetAsBox(float halfWidth, float halfHeight, FVector2 center, float angle)
        {
            Set(PolygonTools.CreateRectangle(halfWidth, halfHeight, center, angle));
        }

        /// <summary>
        /// Test a point for containment in this shape. This only works for convex shapes.
        /// </summary>
        /// <param name="transform">The shape world transform.</param>
        /// <param name="point">a point in world coordinates.</param>
        /// <returns>True if the point is inside the shape</returns>
        public override bool TestPoint(ref Transform transform, ref FVector2 point)
        {
            FVector2 pLocal = MathUtils.MulT(transform.q, point - transform.p);

            for (int i = 0; i < Vertices.Count; ++i)
            {
                float dot = FVector2.Dot(Normals[i], pLocal - Vertices[i]);
                if (dot > 0.0f)
                {
                    return false;
                }
            }

            return true;
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
            output = new RayCastOutput();

            // Put the ray into the polygon's frame of reference.
            FVector2 p1 = MathUtils.MulT(transform.q, input.Point1 - transform.p);
            FVector2 p2 = MathUtils.MulT(transform.q, input.Point2 - transform.p);
            FVector2 d = p2 - p1;

            float lower = 0.0f, upper = input.MaxFraction;

            int index = -1;

            for (int i = 0; i < Vertices.Count; ++i)
            {
                // p = p1 + a * d
                // dot(normal, p - v) = 0
                // dot(normal, p1 - v) + a * dot(normal, d) = 0
                float numerator = FVector2.Dot(Normals[i], Vertices[i] - p1);
                float denominator = FVector2.Dot(Normals[i], d);

                if (denominator == 0.0f)
                {
                    if (numerator < 0.0f)
                    {
                        return false;
                    }
                }
                else
                {
                    // Note: we want this predicate without division:
                    // lower < numerator / denominator, where denominator < 0
                    // Since denominator < 0, we have to flip the inequality:
                    // lower < numerator / denominator <==> denominator * lower > numerator.
                    if (denominator < 0.0f && numerator < lower * denominator)
                    {
                        // Increase lower.
                        // The segment enters this half-space.
                        lower = numerator / denominator;
                        index = i;
                    }
                    else if (denominator > 0.0f && numerator < upper * denominator)
                    {
                        // Decrease upper.
                        // The segment exits this half-space.
                        upper = numerator / denominator;
                    }
                }

                // The use of epsilon here causes the assert on lower to trip
                // in some cases. Apparently the use of epsilon was to make edge
                // shapes work, but now those are handled separately.
                //if (upper < lower - b2_epsilon)
                if (upper < lower)
                {
                    return false;
                }
            }

            Debug.Assert(0.0f <= lower && lower <= input.MaxFraction);

            if (index >= 0)
            {
                output.Fraction = lower;
                output.Normal = MathUtils.Mul(transform.q, Normals[index]);
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
            FVector2 lower = MathUtils.Mul(ref transform, Vertices[0]);
            FVector2 upper = lower;

            for (int i = 1; i < Vertices.Count; ++i)
            {
                FVector2 v = MathUtils.Mul(ref transform, Vertices[i]);
                lower = FVector2.Min(lower, v);
                upper = FVector2.Max(upper, v);
            }

            FVector2 r = new FVector2(Radius, Radius);
            aabb.LowerBound = lower - r;
            aabb.UpperBound = upper + r;
        }

        public bool CompareTo(PolygonShape shape)
        {
            if (Vertices.Count != shape.Vertices.Count)
                return false;

            for (int i = 0; i < Vertices.Count; i++)
            {
                if (Vertices[i] != shape.Vertices[i])
                    return false;
            }

            return (Radius == shape.Radius);
        }

        public override float ComputeSubmergedArea(FVector2 normal, float offset, Transform xf, out FVector2 sc)
        {
            sc = FVector2.Zero;

            //Transform plane into shape co-ordinates
            FVector2 normalL = MathUtils.MulT(xf.q, normal);
            float offsetL = offset - FVector2.Dot(normal, xf.p);

            float[] depths = new float[Settings.MaxPolygonVertices];
            int diveCount = 0;
            int intoIndex = -1;
            int outoIndex = -1;

            bool lastSubmerged = false;
            int i;
            for (i = 0; i < Vertices.Count; i++)
            {
                depths[i] = FVector2.Dot(normalL, Vertices[i]) - offsetL;
                bool isSubmerged = depths[i] < -Settings.Epsilon;
                if (i > 0)
                {
                    if (isSubmerged)
                    {
                        if (!lastSubmerged)
                        {
                            intoIndex = i - 1;
                            diveCount++;
                        }
                    }
                    else
                    {
                        if (lastSubmerged)
                        {
                            outoIndex = i - 1;
                            diveCount++;
                        }
                    }
                }
                lastSubmerged = isSubmerged;
            }
            switch (diveCount)
            {
                case 0:
                        return 0;
                case 1:
                    if (intoIndex == -1)
                    {
                        intoIndex = Vertices.Count - 1;
                    }
                    else
                    {
                        outoIndex = Vertices.Count - 1;
                    }
                    break;
            }
            int intoIndex2 = (intoIndex + 1) % Vertices.Count;
            int outoIndex2 = (outoIndex + 1) % Vertices.Count;

            float intoLambda = (0 - depths[intoIndex]) / (depths[intoIndex2] - depths[intoIndex]);
            float outoLambda = (0 - depths[outoIndex]) / (depths[outoIndex2] - depths[outoIndex]);

            FVector2 intoVec = new FVector2(
                Vertices[intoIndex].X * (1 - intoLambda) + Vertices[intoIndex2].X * intoLambda,
                Vertices[intoIndex].Y * (1 - intoLambda) + Vertices[intoIndex2].Y * intoLambda);
            FVector2 outoVec = new FVector2(
                Vertices[outoIndex].X * (1 - outoLambda) + Vertices[outoIndex2].X * outoLambda,
                Vertices[outoIndex].Y * (1 - outoLambda) + Vertices[outoIndex2].Y * outoLambda);

            //Initialize accumulator
            float area = 0;
            FVector2 center = new FVector2(0, 0);
            FVector2 p2 = Vertices[intoIndex2];
            FVector2 p3;

            float k_inv3 = 1.0f / 3.0f;

            //An awkward loop from intoIndex2+1 to outIndex2
            i = intoIndex2;
            while (i != outoIndex2)
            {
                i = (i + 1) % Vertices.Count;
                if (i == outoIndex2)
                    p3 = outoVec;
                else
                    p3 = Vertices[i];
                //Add the triangle formed by intoVec,p2,p3
                {
                    FVector2 e1 = p2 - intoVec;
                    FVector2 e2 = p3 - intoVec;

                    float D = MathUtils.Cross(e1, e2);

                    float triangleArea = 0.5f * D;

                    area += triangleArea;

                    // Area weighted centroid
                    center += triangleArea * k_inv3 * (intoVec + p2 + p3);
                }
                //
                p2 = p3;
            }

            //Normalize and transform centroid
            center *= 1.0f / area;

            sc = MathUtils.Mul(ref xf, center);

            return area;
        }

        public bool Validate()
        {
            for (int i = 0; i < Vertices.Count; ++i)
            {
                int i1 = i;
                int i2 = i < Vertices.Count - 1 ? i1 + 1 : 0;
                FVector2 p = Vertices[i1];
                FVector2 e = Vertices[i2] - p;

                for (int j = 0; j < Vertices.Count; ++j)
                {
                    if (j == i1 || j == i2)
                    {
                        continue;
                    }

                    FVector2 v = Vertices[j] - p;
                    float c = MathUtils.Cross(e, v);
                    if (c < 0.0f)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}