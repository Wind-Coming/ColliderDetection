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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace FarseerPhysics
{
    internal enum ContactFeatureType : byte
    {
        Vertex = 0,
        Face = 1,
    }

    /// <summary>
    /// The features that intersect to form the contact point
    /// This must be 4 bytes or less.
    /// </summary>
    public struct ContactFeature
    {
        /// <summary>
        /// Feature index on ShapeA
        /// </summary>
        public byte IndexA;

        /// <summary>
        /// Feature index on ShapeB
        /// </summary>
        public byte IndexB;

        /// <summary>
        /// The feature type on ShapeA
        /// </summary>
        public byte TypeA;

        /// <summary>
        /// The feature type on ShapeB
        /// </summary>
        public byte TypeB;
    }

    /// <summary>
    /// Contact ids to facilitate warm starting.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct ContactID
    {
        /// <summary>
        /// The features that intersect to form the contact point
        /// </summary>
        [FieldOffset(0)]
        public ContactFeature Features;

        /// <summary>
        /// Used to quickly compare contact ids.
        /// </summary>
        [FieldOffset(0)]
        public uint Key;
    }

    /// <summary>
    /// A manifold point is a contact point belonging to a contact
    /// manifold. It holds details related to the geometry and dynamics
    /// of the contact points.
    /// The local point usage depends on the manifold type:
    /// -ShapeType.Circles: the local center of circleB
    /// -SeparationFunction.FaceA: the local center of cirlceB or the clip point of polygonB
    /// -SeparationFunction.FaceB: the clip point of polygonA
    /// This structure is stored across time steps, so we keep it small.
    /// Note: the impulses are used for internal caching and may not
    /// provide reliable contact forces, especially for high speed collisions.
    /// </summary>
    public struct ManifoldPoint
    {
        /// <summary>
        /// Uniquely identifies a contact point between two Shapes
        /// </summary>
        public ContactID Id;

        public FVector2 LocalPoint;

        public float NormalImpulse;

        public float TangentImpulse;
    }

    public enum ManifoldType
    {
        Circles,
        FaceA,
        FaceB
    }

    /// <summary>
    /// A manifold for two touching convex Shapes.
    /// Box2D supports multiple types of contact:
    /// - clip point versus plane with radius
    /// - point versus point with radius (circles)
    /// The local point usage depends on the manifold type:
    /// -ShapeType.Circles: the local center of circleA
    /// -SeparationFunction.FaceA: the center of faceA
    /// -SeparationFunction.FaceB: the center of faceB
    /// Similarly the local normal usage:
    /// -ShapeType.Circles: not used
    /// -SeparationFunction.FaceA: the normal on polygonA
    /// -SeparationFunction.FaceB: the normal on polygonB
    /// We store contacts in this way so that position correction can
    /// account for movement, which is critical for continuous physics.
    /// All contact scenarios must be expressed in one of these types.
    /// This structure is stored across time steps, so we keep it small.
    /// </summary>
    public struct Manifold
    {
        /// <summary>
        /// Not use for Type.SeparationFunction.Points
        /// </summary>
        public FVector2 LocalNormal;

        /// <summary>
        /// Usage depends on manifold type
        /// </summary>
        public FVector2 LocalPoint;

        /// <summary>
        /// The number of manifold points
        /// </summary>
        public int PointCount;

        /// <summary>
        /// The points of contact
        /// </summary>
        public FixedArray2<ManifoldPoint> Points;

        public ManifoldType Type;

        /// <summary>
        /// 距离，只有圆圆才有
        /// </summary>
        public float distSqr;
    }

    /// <summary>
    /// This is used for determining the state of contact points.
    /// </summary>
    public enum PointState
    {
        /// <summary>
        /// Point does not exist
        /// </summary>
        Null,

        /// <summary>
        /// Point was added in the update
        /// </summary>
        Add,

        /// <summary>
        /// Point persisted across the update
        /// </summary>
        Persist,

        /// <summary>
        /// Point was removed in the update
        /// </summary>
        Remove,
    }

    /// <summary>
    /// Used for computing contact manifolds.
    /// </summary>
    public struct ClipVertex
    {
        public ContactID ID;
        public FVector2 V;
    }

    /// <summary>
    /// Ray-cast input data. The ray extends from p1 to p1 + maxFraction * (p2 - p1).
    /// </summary>
    public struct RayCastInput
    {
        public float MaxFraction;
        public FVector2 Point1, Point2;
    }

    /// <summary>
    /// Ray-cast output data.  The ray hits at p1 + fraction * (p2 - p1), where p1 and p2
    /// come from RayCastInput. 
    /// </summary>
    public struct RayCastOutput
    {
        public float Fraction;
        public FVector2 Normal;
    }

    /// <summary>
    /// An axis aligned bounding box.
    /// </summary>
    [Serializable]
    public struct AABB
    {
        private static DistanceInput _input = new DistanceInput();

        /// <summary>
        /// The lower vertex
        /// </summary>
        public FVector2 LowerBound;

        /// <summary>
        /// The upper vertex
        /// </summary>
        public FVector2 UpperBound;

        public AABB(FVector2 min, FVector2 max)
            : this(ref min, ref max)
        {
        }

        public AABB(ref FVector2 min, ref FVector2 max)
        {
            LowerBound = min;
            UpperBound = max;
        }

        public AABB(FVector2 center, float width, float height)
        {
            LowerBound = center - new FVector2(width / 2, height / 2);
            UpperBound = center + new FVector2(width / 2, height / 2);
        }

        /// <summary>
        /// Get the center of the AABB.
        /// </summary>
        /// <value></value>
        public FVector2 Center
        {
            get { return 0.5f * (LowerBound + UpperBound); }
        }

        /// <summary>
        /// Get the extents of the AABB (half-widths).
        /// </summary>
        /// <value></value>
        public FVector2 Extents
        {
            get { return 0.5f * (UpperBound - LowerBound); }
        }

        /// <summary>
        /// Get the perimeter length
        /// </summary>
        /// <value></value>
        public float Perimeter
        {
            get
            {
                float wx = UpperBound.X - LowerBound.X;
                float wy = UpperBound.Y - LowerBound.Y;
                return 2.0f * (wx + wy);
            }
        }

        /// <summary>
        /// Gets the vertices of the AABB.
        /// </summary>
        /// <value>The corners of the AABB</value>
        public Vertices Vertices
        {
            get
            {
                Vertices vertices = new Vertices();
                vertices.Add(LowerBound);
                vertices.Add(new FVector2(LowerBound.X, UpperBound.Y));
                vertices.Add(UpperBound);
                vertices.Add(new FVector2(UpperBound.X, LowerBound.Y));
                return vertices;
            }
        }

        /// <summary>
        /// first quadrant
        /// </summary>
        public AABB Q1
        {
            get { return new AABB(Center, UpperBound); }
        }

        public AABB Q2
        {
            get
            {
                return new AABB(new FVector2(LowerBound.X, Center.Y), new FVector2(Center.X, UpperBound.Y));
                ;
            }
        }

        public AABB Q3
        {
            get { return new AABB(LowerBound, Center); }
        }

        public AABB Q4
        {
            get { return new AABB(new FVector2(Center.X, LowerBound.Y), new FVector2(UpperBound.X, Center.Y)); }
        }

        public FVector2[] GetVertices()
        {
            FVector2 p1 = UpperBound;
            FVector2 p2 = new FVector2(UpperBound.X, LowerBound.Y);
            FVector2 p3 = LowerBound;
            FVector2 p4 = new FVector2(LowerBound.X, UpperBound.Y);
            return new[] { p1, p2, p3, p4 };
        }

        /// <summary>
        /// Verify that the bounds are sorted.
        /// </summary>
        /// <returns>
        /// 	<c>true</c> if this instance is valid; otherwise, <c>false</c>.
        /// </returns>
        public bool IsValid()
        {
            FVector2 d = UpperBound - LowerBound;
            bool valid = d.X >= 0.0f && d.Y >= 0.0f;
            valid = valid && LowerBound.IsValid() && UpperBound.IsValid();
            return valid;
        }

        /// <summary>
        /// Combine an AABB into this one.
        /// </summary>
        /// <param name="aabb">The aabb.</param>
        public void Combine(ref AABB aabb)
        {
            LowerBound = FVector2.Min(LowerBound, aabb.LowerBound);
            UpperBound = FVector2.Max(UpperBound, aabb.UpperBound);
        }

        /// <summary>
        /// Combine two AABBs into this one.
        /// </summary>
        /// <param name="aabb1">The aabb1.</param>
        /// <param name="aabb2">The aabb2.</param>
        public void Combine(ref AABB aabb1, ref AABB aabb2)
        {
            LowerBound = FVector2.Min(aabb1.LowerBound, aabb2.LowerBound);
            UpperBound = FVector2.Max(aabb1.UpperBound, aabb2.UpperBound);
        }

        /// <summary>
        /// Does this aabb contain the provided AABB.
        /// </summary>
        /// <param name="aabb">The aabb.</param>
        /// <returns>
        /// 	<c>true</c> if it contains the specified aabb; otherwise, <c>false</c>.
        /// </returns>
        public bool Contains(ref AABB aabb)
        {
            bool result = true;
            result = result && LowerBound.X <= aabb.LowerBound.X;
            result = result && LowerBound.Y <= aabb.LowerBound.Y;
            result = result && aabb.UpperBound.X <= UpperBound.X;
            result = result && aabb.UpperBound.Y <= UpperBound.Y;
            return result;
        }

        /// <summary>
        /// Determines whether the AAABB contains the specified point.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>
        /// 	<c>true</c> if it contains the specified point; otherwise, <c>false</c>.
        /// </returns>
        public bool Contains(ref FVector2 point)
        {
            //using epsilon to try and gaurd against float rounding errors.
            if ((point.X > (LowerBound.X + Settings.Epsilon) && point.X < (UpperBound.X - Settings.Epsilon) &&
                 (point.Y > (LowerBound.Y + Settings.Epsilon) && point.Y < (UpperBound.Y - Settings.Epsilon))))
            {
                return true;
            }
            return false;
        }

        public static bool TestOverlap(AABB a, AABB b)
        {
            return TestOverlap(ref a, ref b);
        }

        public static bool TestOverlap(ref AABB a, ref AABB b)
        {
            FVector2 d1 = b.LowerBound - a.UpperBound;
            FVector2 d2 = a.LowerBound - b.UpperBound;

            if (d1.X > 0.0f || d1.Y > 0.0f)
                return false;

            if (d2.X > 0.0f || d2.Y > 0.0f)
                return false;

            return true;
        }

        public static bool TestOverlap(Shape shapeA, int indexA,
                                       Shape shapeB, int indexB,
                                       ref Transform xfA, ref Transform xfB)
        {
            _input.ProxyA.Set(shapeA, indexA);
            _input.ProxyB.Set(shapeB, indexB);
            _input.TransformA = xfA;
            _input.TransformB = xfB;
            _input.UseRadii = true;

            SimplexCache cache;
            DistanceOutput output;
            Distance.ComputeDistance(out output, out cache, _input);

            return output.Distance < 10.0f * Settings.Epsilon;
        }


        // From Real-time Collision Detection, p179.
        public bool RayCast(out RayCastOutput output, ref RayCastInput input)
        {
            output = new RayCastOutput();

            float tmin = -Settings.MaxFloat;
            float tmax = Settings.MaxFloat;

            FVector2 p = input.Point1;
            FVector2 d = input.Point2 - input.Point1;
            FVector2 absD = MathUtils.Abs(d);

            FVector2 normal = FVector2.Zero;

            for (int i = 0; i < 2; ++i)
            {
                float absD_i = i == 0 ? absD.X : absD.Y;
                float lowerBound_i = i == 0 ? LowerBound.X : LowerBound.Y;
                float upperBound_i = i == 0 ? UpperBound.X : UpperBound.Y;
                float p_i = i == 0 ? p.X : p.Y;

                if (absD_i < Settings.Epsilon)
                {
                    // Parallel.
                    if (p_i < lowerBound_i || upperBound_i < p_i)
                    {
                        return false;
                    }
                }
                else
                {
                    float d_i = i == 0 ? d.X : d.Y;

                    float inv_d = 1.0f / d_i;
                    float t1 = (lowerBound_i - p_i) * inv_d;
                    float t2 = (upperBound_i - p_i) * inv_d;

                    // Sign of the normal vector.
                    float s = -1.0f;

                    if (t1 > t2)
                    {
                        MathUtils.Swap(ref t1, ref t2);
                        s = 1.0f;
                    }

                    // Push the min up
                    if (t1 > tmin)
                    {
                        if (i == 0)
                        {
                            normal.X = s;
                        }
                        else
                        {
                            normal.Y = s;
                        }

                        tmin = t1;
                    }

                    // Pull the max down
                    tmax = Math.Min(tmax, t2);

                    if (tmin > tmax)
                    {
                        return false;
                    }
                }
            }

            // Does the ray start inside the box?
            // Does the ray intersect beyond the max fraction?
            if (tmin < 0.0f || input.MaxFraction < tmin)
            {
                return false;
            }

            // Intersection.
            output.Fraction = tmin;
            output.Normal = normal;
            return true;
        }
    }

    /// <summary>
    /// This lets us treate and edge shape and a polygon in the same
    /// way in the SAT collider.
    /// </summary>
    public class TempPolygon
    {
        public int Count;
        public FVector2[] Normals = new FVector2[Settings.MaxPolygonVertices];
        public FVector2[] Vertices = new FVector2[Settings.MaxPolygonVertices];
    }

    public struct EPAxis
    {
        public int Index;
        public float Separation;
        public EPAxisType Type;
    }

    // Reference face used for clipping
    public struct ReferenceFace
    {
        public int i1, i2;

        public FVector2 v1, v2;

        public FVector2 normal;

        public FVector2 sideNormal1;
        public float sideOffset1;

        public FVector2 sideNormal2;
        public float sideOffset2;
    }

    public enum EPAxisType
    {
        Unknown,
        EdgeA,
        EdgeB,
    }

    public static class Collision
    {
        public static void GetPointStates(out FixedArray2<PointState> state1, out FixedArray2<PointState> state2,
                                          ref Manifold manifold1, ref Manifold manifold2)
        {
            state1 = new FixedArray2<PointState>();
            state2 = new FixedArray2<PointState>();

            // Detect persists and removes.
            for (int i = 0; i < manifold1.PointCount; ++i)
            {
                ContactID id = manifold1.Points[i].Id;

                state1[i] = PointState.Remove;

                for (int j = 0; j < manifold2.PointCount; ++j)
                {
                    if (manifold2.Points[j].Id.Key == id.Key)
                    {
                        state1[i] = PointState.Persist;
                        break;
                    }
                }
            }

            // Detect persists and adds.
            for (int i = 0; i < manifold2.PointCount; ++i)
            {
                ContactID id = manifold2.Points[i].Id;

                state2[i] = PointState.Add;

                for (int j = 0; j < manifold1.PointCount; ++j)
                {
                    if (manifold1.Points[j].Id.Key == id.Key)
                    {
                        state2[i] = PointState.Persist;
                        break;
                    }
                }
            }
        }


        /// Compute the collision manifold between two circles.
        public static void CollideCircles(ref Manifold manifold,
                                          CircleShape circleA, ref Transform xfA,
                                          CircleShape circleB, ref Transform xfB)
        {
            manifold.PointCount = 0;

            FVector2 pA = MathUtils.Mul(ref xfA, circleA.Position);
            FVector2 pB = MathUtils.Mul(ref xfB, circleB.Position);

            FVector2 d = pB - pA;
            float distSqr = FVector2.Dot(d, d);
            float radius = circleA.Radius + circleB.Radius;
            manifold.distSqr = distSqr;
            if (distSqr > radius * radius)
            {
                return;
            }

            manifold.Type = ManifoldType.Circles;
            manifold.LocalPoint = circleA.Position;
            manifold.LocalNormal = FVector2.Zero;
            manifold.PointCount = 1;

            ManifoldPoint p0 = manifold.Points[0];

            p0.LocalPoint = circleB.Position;
            p0.Id.Key = 0;

            manifold.Points[0] = p0;
        }

        /// <summary>
        /// Compute the collision manifold between a polygon and a circle.
        /// </summary>
        /// <param name="manifold">The manifold.</param>
        /// <param name="polygonA">The polygon A.</param>
        /// <param name="xfA">The transform of A.</param>
        /// <param name="circleB">The circle B.</param>
        /// <param name="xfB">The transform of B.</param>
        public static void CollidePolygonAndCircle(ref Manifold manifold,
                                                   PolygonShape polygonA, ref Transform xfA,
                                                   CircleShape circleB, ref Transform xfB)
        {
            manifold.PointCount = 0;

            // Compute circle position in the frame of the polygon.
            FVector2 c = MathUtils.Mul(ref xfB, circleB.Position);
            FVector2 cLocal = MathUtils.MulT(ref xfA, c);

            // Find the min separating edge.
            int normalIndex = 0;
            float separation = -Settings.MaxFloat;
            float radius = polygonA.Radius + circleB.Radius;
            int vertexCount = polygonA.Vertices.Count;

            for (int i = 0; i < vertexCount; ++i)
            {
                FVector2 value1 = polygonA.Normals[i];
                FVector2 value2 = cLocal - polygonA.Vertices[i];
                float s = value1.X * value2.X + value1.Y * value2.Y;

                if (s > radius)
                {
                    // Early out.
                    return;
                }

                if (s > separation)
                {
                    separation = s;
                    normalIndex = i;
                }
            }

            // Vertices that subtend the incident face.
            int vertIndex1 = normalIndex;
            int vertIndex2 = vertIndex1 + 1 < vertexCount ? vertIndex1 + 1 : 0;
            FVector2 v1 = polygonA.Vertices[vertIndex1];
            FVector2 v2 = polygonA.Vertices[vertIndex2];

            // If the center is inside the polygon ...
            if (separation < Settings.Epsilon)
            {
                manifold.PointCount = 1;
                manifold.Type = ManifoldType.FaceA;
                manifold.LocalNormal = polygonA.Normals[normalIndex];
                manifold.LocalPoint = 0.5f * (v1 + v2);

                ManifoldPoint p0 = manifold.Points[0];

                p0.LocalPoint = circleB.Position;
                p0.Id.Key = 0;

                manifold.Points[0] = p0;

                return;
            }

            // Compute barycentric coordinates
            float u1 = (cLocal.X - v1.X) * (v2.X - v1.X) + (cLocal.Y - v1.Y) * (v2.Y - v1.Y);
            float u2 = (cLocal.X - v2.X) * (v1.X - v2.X) + (cLocal.Y - v2.Y) * (v1.Y - v2.Y);

            if (u1 <= 0.0f)
            {
                float r = (cLocal.X - v1.X) * (cLocal.X - v1.X) + (cLocal.Y - v1.Y) * (cLocal.Y - v1.Y);
                if (r > radius * radius)
                {
                    return;
                }

                manifold.PointCount = 1;
                manifold.Type = ManifoldType.FaceA;
                manifold.LocalNormal = cLocal - v1;
                float factor = 1f /
                               (float)
                               Math.Sqrt(manifold.LocalNormal.X * manifold.LocalNormal.X +
                                         manifold.LocalNormal.Y * manifold.LocalNormal.Y);
                manifold.LocalNormal.X = manifold.LocalNormal.X * factor;
                manifold.LocalNormal.Y = manifold.LocalNormal.Y * factor;
                manifold.LocalPoint = v1;

                ManifoldPoint p0b = manifold.Points[0];

                p0b.LocalPoint = circleB.Position;
                p0b.Id.Key = 0;

                manifold.Points[0] = p0b;
            }
            else if (u2 <= 0.0f)
            {
                float r = (cLocal.X - v2.X) * (cLocal.X - v2.X) + (cLocal.Y - v2.Y) * (cLocal.Y - v2.Y);
                if (r > radius * radius)
                {
                    return;
                }

                manifold.PointCount = 1;
                manifold.Type = ManifoldType.FaceA;
                manifold.LocalNormal = cLocal - v2;
                float factor = 1f /
                               (float)
                               Math.Sqrt(manifold.LocalNormal.X * manifold.LocalNormal.X +
                                         manifold.LocalNormal.Y * manifold.LocalNormal.Y);
                manifold.LocalNormal.X = manifold.LocalNormal.X * factor;
                manifold.LocalNormal.Y = manifold.LocalNormal.Y * factor;
                manifold.LocalPoint = v2;

                ManifoldPoint p0c = manifold.Points[0];

                p0c.LocalPoint = circleB.Position;
                p0c.Id.Key = 0;

                manifold.Points[0] = p0c;
            }
            else
            {
                FVector2 faceCenter = 0.5f * (v1 + v2);
                FVector2 value1 = cLocal - faceCenter;
                FVector2 value2 = polygonA.Normals[vertIndex1];
                float separation2 = value1.X * value2.X + value1.Y * value2.Y;
                if (separation2 > radius)
                {
                    return;
                }

                manifold.PointCount = 1;
                manifold.Type = ManifoldType.FaceA;
                manifold.LocalNormal = polygonA.Normals[vertIndex1];
                manifold.LocalPoint = faceCenter;

                ManifoldPoint p0d = manifold.Points[0];

                p0d.LocalPoint = circleB.Position;
                p0d.Id.Key = 0;

                manifold.Points[0] = p0d;
            }
        }

        /// <summary>
        /// Compute the collision manifold between two polygons.
        /// </summary>
        /// <param name="manifold">The manifold.</param>
        /// <param name="polyA">The poly A.</param>
        /// <param name="transformA">The transform A.</param>
        /// <param name="polyB">The poly B.</param>
        /// <param name="transformB">The transform B.</param>
        public static void CollidePolygons(ref Manifold manifold,
                                           PolygonShape polyA, ref Transform transformA,
                                           PolygonShape polyB, ref Transform transformB)
        {
            manifold.PointCount = 0;
            float totalRadius = polyA.Radius + polyB.Radius;

            int edgeA = 0;
            float separationA = FindMaxSeparation(out edgeA, polyA, ref transformA, polyB, ref transformB);
            if (separationA > totalRadius)
                return;

            int edgeB = 0;
            float separationB = FindMaxSeparation(out edgeB, polyB, ref transformB, polyA, ref transformA);
            if (separationB > totalRadius)
                return;

            PolygonShape poly1; // reference polygon
            PolygonShape poly2; // incident polygon
            Transform xf1, xf2;
            int edge1; // reference edge
            bool flip;
            const float k_relativeTol = 0.98f;
            const float k_absoluteTol = 0.001f;

            if (separationB > k_relativeTol * separationA + k_absoluteTol)
            {
                poly1 = polyB;
                poly2 = polyA;
                xf1 = transformB;
                xf2 = transformA;
                edge1 = edgeB;
                manifold.Type = ManifoldType.FaceB;
                flip = true;
            }
            else
            {
                poly1 = polyA;
                poly2 = polyB;
                xf1 = transformA;
                xf2 = transformB;
                edge1 = edgeA;
                manifold.Type = ManifoldType.FaceA;
                flip = false;
            }

            FixedArray2<ClipVertex> incidentEdge;
            FindIncidentEdge(out incidentEdge, poly1, ref xf1, edge1, poly2, ref xf2);

            int count1 = poly1.Vertices.Count;

            int iv1 = edge1;
            int iv2 = edge1 + 1 < count1 ? edge1 + 1 : 0;

            FVector2 v11 = poly1.Vertices[iv1];
            FVector2 v12 = poly1.Vertices[iv2];

            FVector2 localTangent = v12 - v11;
            localTangent.Normalize();

            FVector2 localNormal = new FVector2(localTangent.Y, -localTangent.X);
            FVector2 planePoint = 0.5f * (v11 + v12);

            FVector2 tangent = MathUtils.Mul(xf1.q, localTangent);

            float normalx = tangent.Y;
            float normaly = -tangent.X;

            v11 = MathUtils.Mul(ref xf1, v11);
            v12 = MathUtils.Mul(ref xf1, v12);

            // Face offset.
            float frontOffset = normalx * v11.X + normaly * v11.Y;

            // Side offsets, extended by polytope skin thickness.
            float sideOffset1 = -(tangent.X * v11.X + tangent.Y * v11.Y) + totalRadius;
            float sideOffset2 = tangent.X * v12.X + tangent.Y * v12.Y + totalRadius;

            // Clip incident edge against extruded edge1 side edges.
            FixedArray2<ClipVertex> clipPoints1;
            FixedArray2<ClipVertex> clipPoints2;

            // Clip to box side 1
            int np = ClipSegmentToLine(out clipPoints1, ref incidentEdge, -tangent, sideOffset1, iv1);

            if (np < 2)
                return;

            // Clip to negative box side 1
            np = ClipSegmentToLine(out clipPoints2, ref clipPoints1, tangent, sideOffset2, iv2);

            if (np < 2)
            {
                return;
            }

            // Now clipPoints2 contains the clipped points.
            manifold.LocalNormal = localNormal;
            manifold.LocalPoint = planePoint;

            int pointCount = 0;
            for (int i = 0; i < Settings.MaxManifoldPoints; ++i)
            {
                FVector2 value = clipPoints2[i].V;
                float separation = normalx * value.X + normaly * value.Y - frontOffset;

                if (separation <= totalRadius)
                {
                    ManifoldPoint cp = manifold.Points[pointCount];
                    cp.LocalPoint = MathUtils.MulT(ref xf2, clipPoints2[i].V);
                    cp.Id = clipPoints2[i].ID;

                    if (flip)
                    {
                        // Swap features
                        ContactFeature cf = cp.Id.Features;
                        cp.Id.Features.IndexA = cf.IndexB;
                        cp.Id.Features.IndexB = cf.IndexA;
                        cp.Id.Features.TypeA = cf.TypeB;
                        cp.Id.Features.TypeB = cf.TypeA;
                    }

                    manifold.Points[pointCount] = cp;

                    ++pointCount;
                }
            }

            manifold.PointCount = pointCount;
        }

        /// <summary>
        /// Compute contact points for edge versus circle.
        /// This accounts for edge connectivity.
        /// </summary>
        /// <param name="manifold">The manifold.</param>
        /// <param name="edgeA">The edge A.</param>
        /// <param name="transformA">The transform A.</param>
        /// <param name="circleB">The circle B.</param>
        /// <param name="transformB">The transform B.</param>
        public static void CollideEdgeAndCircle(ref Manifold manifold,
                                                EdgeShape edgeA, ref Transform transformA,
                                                CircleShape circleB, ref Transform transformB)
        {
            manifold.PointCount = 0;

            // Compute circle in frame of edge
            FVector2 Q = MathUtils.MulT(ref transformA, MathUtils.Mul(ref transformB, ref circleB._position));

            FVector2 A = edgeA.Vertex1, B = edgeA.Vertex2;
            FVector2 e = B - A;

            // Barycentric coordinates
            float u = FVector2.Dot(e, B - Q);
            float v = FVector2.Dot(e, Q - A);

            float radius = edgeA.Radius + circleB.Radius;

            ContactFeature cf;
            cf.IndexB = 0;
            cf.TypeB = (byte)ContactFeatureType.Vertex;

            FVector2 P, d;

            // Region A
            if (v <= 0.0f)
            {
                P = A;
                d = Q - P;
                float dd;
                FVector2.Dot(ref d, ref d, out dd);
                if (dd > radius * radius)
                {
                    return;
                }

                // Is there an edge connected to A?
                if (edgeA.HasVertex0)
                {
                    FVector2 A1 = edgeA.Vertex0;
                    FVector2 B1 = A;
                    FVector2 e1 = B1 - A1;
                    float u1 = FVector2.Dot(e1, B1 - Q);

                    // Is the circle in Region AB of the previous edge?
                    if (u1 > 0.0f)
                    {
                        return;
                    }
                }

                cf.IndexA = 0;
                cf.TypeA = (byte)ContactFeatureType.Vertex;
                manifold.PointCount = 1;
                manifold.Type = ManifoldType.Circles;
                manifold.LocalNormal = FVector2.Zero;
                manifold.LocalPoint = P;
                ManifoldPoint mp = new ManifoldPoint();
                mp.Id.Key = 0;
                mp.Id.Features = cf;
                mp.LocalPoint = circleB.Position;
                manifold.Points[0] = mp;
                return;
            }

            // Region B
            if (u <= 0.0f)
            {
                P = B;
                d = Q - P;
                float dd;
                FVector2.Dot(ref d, ref d, out dd);
                if (dd > radius * radius)
                {
                    return;
                }

                // Is there an edge connected to B?
                if (edgeA.HasVertex3)
                {
                    FVector2 B2 = edgeA.Vertex3;
                    FVector2 A2 = B;
                    FVector2 e2 = B2 - A2;
                    float v2 = FVector2.Dot(e2, Q - A2);

                    // Is the circle in Region AB of the next edge?
                    if (v2 > 0.0f)
                    {
                        return;
                    }
                }

                cf.IndexA = 1;
                cf.TypeA = (byte)ContactFeatureType.Vertex;
                manifold.PointCount = 1;
                manifold.Type = ManifoldType.Circles;
                manifold.LocalNormal = FVector2.Zero;
                manifold.LocalPoint = P;
                ManifoldPoint mp = new ManifoldPoint();
                mp.Id.Key = 0;
                mp.Id.Features = cf;
                mp.LocalPoint = circleB.Position;
                manifold.Points[0] = mp;
                return;
            }

            // Region AB
            float den;
            FVector2.Dot(ref e, ref e, out den);
            Debug.Assert(den > 0.0f);
            P = (1.0f / den) * (u * A + v * B);
            d = Q - P;
            float dd2;
            FVector2.Dot(ref d, ref d, out dd2);
            if (dd2 > radius * radius)
            {
                return;
            }

            FVector2 n = new FVector2(-e.Y, e.X);
            if (FVector2.Dot(n, Q - A) < 0.0f)
            {
                n = new FVector2(-n.X, -n.Y);
            }
            n.Normalize();

            cf.IndexA = 0;
            cf.TypeA = (byte)ContactFeatureType.Face;
            manifold.PointCount = 1;
            manifold.Type = ManifoldType.FaceA;
            manifold.LocalNormal = n;
            manifold.LocalPoint = A;
            ManifoldPoint mp2 = new ManifoldPoint();
            mp2.Id.Key = 0;
            mp2.Id.Features = cf;
            mp2.LocalPoint = circleB.Position;
            manifold.Points[0] = mp2;
        }

        /// <summary>
        /// Collides and edge and a polygon, taking into account edge adjacency.
        /// </summary>
        /// <param name="manifold">The manifold.</param>
        /// <param name="edgeA">The edge A.</param>
        /// <param name="xfA">The xf A.</param>
        /// <param name="polygonB">The polygon B.</param>
        /// <param name="xfB">The xf B.</param>
        public static void CollideEdgeAndPolygon(ref Manifold manifold,
                                                 EdgeShape edgeA, ref Transform xfA,
                                                 PolygonShape polygonB, ref Transform xfB)
        {
            EPCollider collider = new EPCollider();
            collider.Collide(ref manifold, edgeA, ref xfA, polygonB, ref xfB);
        }

        public class EPCollider
        {
            private TempPolygon _polygonB = new TempPolygon();

            Transform _xf;
            FVector2 _centroidB;
            FVector2 _v0, _v1, _v2, _v3;
            FVector2 _normal0, _normal1, _normal2;
            FVector2 _normal;
            VertexType _type1, _type2;
            FVector2 _lowerLimit, _upperLimit;
            float _radius;
            bool _front;

            enum VertexType
            {
                e_isolated,
                e_concave,
                e_convex
            }

            // Algorithm:
            // 1. Classify v1 and v2
            // 2. Classify polygon centroid as front or back
            // 3. Flip normal if necessary
            // 4. Initialize normal range to [-pi, pi] about face normal
            // 5. Adjust normal range according to adjacent edges
            // 6. Visit each separating axes, only accept axes within the range
            // 7. Return if _any_ axis indicates separation
            // 8. Clip
            public void Collide(ref Manifold manifold, EdgeShape edgeA, ref Transform xfA, PolygonShape polygonB, ref Transform xfB)
            {
                _xf = MathUtils.MulT(xfA, xfB);

                _centroidB = xfB.p;//MathUtils.Mul(ref _xf, polygonB.MassData.Centroid);

                _v0 = edgeA.Vertex0;
                _v1 = edgeA._vertex1;
                _v2 = edgeA._vertex2;
                _v3 = edgeA.Vertex3;

                bool hasVertex0 = edgeA.HasVertex0;
                bool hasVertex3 = edgeA.HasVertex3;

                FVector2 edge1 = _v2 - _v1;
                edge1.Normalize();
                _normal1 = new FVector2(edge1.Y, -edge1.X);
                float offset1 = FVector2.Dot(_normal1, _centroidB - _v1);
                float offset0 = 0.0f, offset2 = 0.0f;
                bool convex1 = false, convex2 = false;

                // Is there a preceding edge?
                if (hasVertex0)
                {
                    FVector2 edge0 = _v1 - _v0;
                    edge0.Normalize();
                    _normal0 = new FVector2(edge0.Y, -edge0.X);
                    convex1 = MathUtils.Cross(edge0, edge1) >= 0.0f;
                    offset0 = FVector2.Dot(_normal0, _centroidB - _v0);
                }

                // Is there a following edge?
                if (hasVertex3)
                {
                    FVector2 edge2 = _v3 - _v2;
                    edge2.Normalize();
                    _normal2 = new FVector2(edge2.Y, -edge2.X);
                    convex2 = MathUtils.Cross(edge1, edge2) > 0.0f;
                    offset2 = FVector2.Dot(_normal2, _centroidB - _v2);
                }

                // Determine front or back collision. Determine collision normal limits.
                if (hasVertex0 && hasVertex3)
                {
                    if (convex1 && convex2)
                    {
                        _front = offset0 >= 0.0f || offset1 >= 0.0f || offset2 >= 0.0f;
                        if (_front)
                        {
                            _normal = _normal1;
                            _lowerLimit = _normal0;
                            _upperLimit = _normal2;
                        }
                        else
                        {
                            _normal = -_normal1;
                            _lowerLimit = -_normal1;
                            _upperLimit = -_normal1;
                        }
                    }
                    else if (convex1)
                    {
                        _front = offset0 >= 0.0f || (offset1 >= 0.0f && offset2 >= 0.0f);
                        if (_front)
                        {
                            _normal = _normal1;
                            _lowerLimit = _normal0;
                            _upperLimit = _normal1;
                        }
                        else
                        {
                            _normal = -_normal1;
                            _lowerLimit = -_normal2;
                            _upperLimit = -_normal1;
                        }
                    }
                    else if (convex2)
                    {
                        _front = offset2 >= 0.0f || (offset0 >= 0.0f && offset1 >= 0.0f);
                        if (_front)
                        {
                            _normal = _normal1;
                            _lowerLimit = _normal1;
                            _upperLimit = _normal2;
                        }
                        else
                        {
                            _normal = -_normal1;
                            _lowerLimit = -_normal1;
                            _upperLimit = -_normal0;
                        }
                    }
                    else
                    {
                        _front = offset0 >= 0.0f && offset1 >= 0.0f && offset2 >= 0.0f;
                        if (_front)
                        {
                            _normal = _normal1;
                            _lowerLimit = _normal1;
                            _upperLimit = _normal1;
                        }
                        else
                        {
                            _normal = -_normal1;
                            _lowerLimit = -_normal2;
                            _upperLimit = -_normal0;
                        }
                    }
                }
                else if (hasVertex0)
                {
                    if (convex1)
                    {
                        _front = offset0 >= 0.0f || offset1 >= 0.0f;
                        if (_front)
                        {
                            _normal = _normal1;
                            _lowerLimit = _normal0;
                            _upperLimit = -_normal1;
                        }
                        else
                        {
                            _normal = -_normal1;
                            _lowerLimit = _normal1;
                            _upperLimit = -_normal1;
                        }
                    }
                    else
                    {
                        _front = offset0 >= 0.0f && offset1 >= 0.0f;
                        if (_front)
                        {
                            _normal = _normal1;
                            _lowerLimit = _normal1;
                            _upperLimit = -_normal1;
                        }
                        else
                        {
                            _normal = -_normal1;
                            _lowerLimit = _normal1;
                            _upperLimit = -_normal0;
                        }
                    }
                }
                else if (hasVertex3)
                {
                    if (convex2)
                    {
                        _front = offset1 >= 0.0f || offset2 >= 0.0f;
                        if (_front)
                        {
                            _normal = _normal1;
                            _lowerLimit = -_normal1;
                            _upperLimit = _normal2;
                        }
                        else
                        {
                            _normal = -_normal1;
                            _lowerLimit = -_normal1;
                            _upperLimit = _normal1;
                        }
                    }
                    else
                    {
                        _front = offset1 >= 0.0f && offset2 >= 0.0f;
                        if (_front)
                        {
                            _normal = _normal1;
                            _lowerLimit = -_normal1;
                            _upperLimit = _normal1;
                        }
                        else
                        {
                            _normal = -_normal1;
                            _lowerLimit = -_normal2;
                            _upperLimit = _normal1;
                        }
                    }
                }
                else
                {
                    _front = offset1 >= 0.0f;
                    if (_front)
                    {
                        _normal = _normal1;
                        _lowerLimit = -_normal1;
                        _upperLimit = -_normal1;
                    }
                    else
                    {
                        _normal = -_normal1;
                        _lowerLimit = _normal1;
                        _upperLimit = _normal1;
                    }
                }

                // Get polygonB in frameA
                _polygonB.Count = polygonB.Vertices.Count;
                for (int i = 0; i < polygonB.Vertices.Count; ++i)
                {
                    _polygonB.Vertices[i] = MathUtils.Mul(ref _xf, polygonB.Vertices[i]);
                    _polygonB.Normals[i] = MathUtils.Mul(_xf.q, polygonB.Normals[i]);
                }

                _radius = 2.0f * Settings.PolygonRadius;

                manifold.PointCount = 0;

                EPAxis edgeAxis = ComputeEdgeSeparation();

                // If no valid normal can be found than this edge should not collide.
                if (edgeAxis.Type == EPAxisType.Unknown)
                {
                    return;
                }

                if (edgeAxis.Separation > _radius)
                {
                    return;
                }

                EPAxis polygonAxis = ComputePolygonSeparation();
                if (polygonAxis.Type != EPAxisType.Unknown && polygonAxis.Separation > _radius)
                {
                    return;
                }

                // Use hysteresis for jitter reduction.
                const float k_relativeTol = 0.98f;
                const float k_absoluteTol = 0.001f;

                EPAxis primaryAxis;
                if (polygonAxis.Type == EPAxisType.Unknown)
                {
                    primaryAxis = edgeAxis;
                }
                else if (polygonAxis.Separation > k_relativeTol * edgeAxis.Separation + k_absoluteTol)
                {
                    primaryAxis = polygonAxis;
                }
                else
                {
                    primaryAxis = edgeAxis;
                }

                FixedArray2<ClipVertex> ie = new FixedArray2<ClipVertex>();
                ReferenceFace rf;
                if (primaryAxis.Type == EPAxisType.EdgeA)
                {
                    manifold.Type = ManifoldType.FaceA;

                    // Search for the polygon normal that is most anti-parallel to the edge normal.
                    int bestIndex = 0;
                    float bestValue = FVector2.Dot(_normal, _polygonB.Normals[0]);
                    for (int i = 1; i < _polygonB.Count; ++i)
                    {
                        float value = FVector2.Dot(_normal, _polygonB.Normals[i]);
                        if (value < bestValue)
                        {
                            bestValue = value;
                            bestIndex = i;
                        }
                    }

                    int i1 = bestIndex;
                    int i2 = i1 + 1 < _polygonB.Count ? i1 + 1 : 0;

                    ClipVertex c0 = ie[0];
                    c0.V = _polygonB.Vertices[i1];
                    c0.ID.Features.IndexA = 0;
                    c0.ID.Features.IndexB = (byte)i1;
                    c0.ID.Features.TypeA = (byte)ContactFeatureType.Face;
                    c0.ID.Features.TypeB = (byte)ContactFeatureType.Vertex;
                    ie[0] = c0;

                    ClipVertex c1 = ie[1];
                    c1.V = _polygonB.Vertices[i2];
                    c1.ID.Features.IndexA = 0;
                    c1.ID.Features.IndexB = (byte)i2;
                    c1.ID.Features.TypeA = (byte)ContactFeatureType.Face;
                    c1.ID.Features.TypeB = (byte)ContactFeatureType.Vertex;
                    ie[1] = c1;

                    if (_front)
                    {
                        rf.i1 = 0;
                        rf.i2 = 1;
                        rf.v1 = _v1;
                        rf.v2 = _v2;
                        rf.normal = _normal1;
                    }
                    else
                    {
                        rf.i1 = 1;
                        rf.i2 = 0;
                        rf.v1 = _v2;
                        rf.v2 = _v1;
                        rf.normal = -_normal1;
                    }
                }
                else
                {
                    manifold.Type = ManifoldType.FaceB;
                    ClipVertex c0 = ie[0];
                    c0.V = _v1;
                    c0.ID.Features.IndexA = 0;
                    c0.ID.Features.IndexB = (byte)primaryAxis.Index;
                    c0.ID.Features.TypeA = (byte)ContactFeatureType.Vertex;
                    c0.ID.Features.TypeB = (byte)ContactFeatureType.Face;
                    ie[0] = c0;

                    ClipVertex c1 = ie[1];
                    c1.V = _v2;
                    c1.ID.Features.IndexA = 0;
                    c1.ID.Features.IndexB = (byte)primaryAxis.Index;
                    c1.ID.Features.TypeA = (byte)ContactFeatureType.Vertex;
                    c1.ID.Features.TypeB = (byte)ContactFeatureType.Face;
                    ie[1] = c1;

                    rf.i1 = primaryAxis.Index;
                    rf.i2 = rf.i1 + 1 < _polygonB.Count ? rf.i1 + 1 : 0;
                    rf.v1 = _polygonB.Vertices[rf.i1];
                    rf.v2 = _polygonB.Vertices[rf.i2];
                    rf.normal = _polygonB.Normals[rf.i1];
                }

                rf.sideNormal1 = new FVector2(rf.normal.Y, -rf.normal.X);
                rf.sideNormal2 = -rf.sideNormal1;
                rf.sideOffset1 = FVector2.Dot(rf.sideNormal1, rf.v1);
                rf.sideOffset2 = FVector2.Dot(rf.sideNormal2, rf.v2);

                // Clip incident edge against extruded edge1 side edges.
                FixedArray2<ClipVertex> clipPoints1;
                FixedArray2<ClipVertex> clipPoints2;
                int np;

                // Clip to box side 1
                np = ClipSegmentToLine(out clipPoints1, ref ie, rf.sideNormal1, rf.sideOffset1, rf.i1);

                if (np < Settings.MaxManifoldPoints)
                {
                    return;
                }

                // Clip to negative box side 1
                np = ClipSegmentToLine(out clipPoints2, ref clipPoints1, rf.sideNormal2, rf.sideOffset2, rf.i2);

                if (np < Settings.MaxManifoldPoints)
                {
                    return;
                }

                // Now clipPoints2 contains the clipped points.
                if (primaryAxis.Type == EPAxisType.EdgeA)
                {
                    manifold.LocalNormal = rf.normal;
                    manifold.LocalPoint = rf.v1;
                }
                else
                {
                    manifold.LocalNormal = polygonB.Normals[rf.i1];
                    manifold.LocalPoint = polygonB.Vertices[rf.i1];
                }

                int pointCount = 0;
                for (int i = 0; i < Settings.MaxManifoldPoints; ++i)
                {
                    float separation;

                    separation = FVector2.Dot(rf.normal, clipPoints2[i].V - rf.v1);

                    if (separation <= _radius)
                    {
                        ManifoldPoint cp = manifold.Points[pointCount];

                        if (primaryAxis.Type == EPAxisType.EdgeA)
                        {
                            cp.LocalPoint = MathUtils.MulT(ref _xf, clipPoints2[i].V);
                            cp.Id = clipPoints2[i].ID;
                        }
                        else
                        {
                            cp.LocalPoint = clipPoints2[i].V;
                            cp.Id.Features.TypeA = clipPoints2[i].ID.Features.TypeB;
                            cp.Id.Features.TypeB = clipPoints2[i].ID.Features.TypeA;
                            cp.Id.Features.IndexA = clipPoints2[i].ID.Features.IndexB;
                            cp.Id.Features.IndexB = clipPoints2[i].ID.Features.IndexA;
                        }

                        manifold.Points[pointCount] = cp;
                        ++pointCount;
                    }
                }

                manifold.PointCount = pointCount;
            }

            private EPAxis ComputeEdgeSeparation()
            {
                EPAxis axis;
                axis.Type = EPAxisType.EdgeA;
                axis.Index = _front ? 0 : 1;
                axis.Separation = Settings.MaxFloat;

                for (int i = 0; i < _polygonB.Count; ++i)
                {
                    float s = FVector2.Dot(_normal, _polygonB.Vertices[i] - _v1);
                    if (s < axis.Separation)
                    {
                        axis.Separation = s;
                    }
                }

                return axis;
            }

            private EPAxis ComputePolygonSeparation()
            {
                EPAxis axis;
                axis.Type = EPAxisType.Unknown;
                axis.Index = -1;
                axis.Separation = -Settings.MaxFloat;

                FVector2 perp = new FVector2(-_normal.Y, _normal.X);

                for (int i = 0; i < _polygonB.Count; ++i)
                {
                    FVector2 n = -_polygonB.Normals[i];

                    float s1 = FVector2.Dot(n, _polygonB.Vertices[i] - _v1);
                    float s2 = FVector2.Dot(n, _polygonB.Vertices[i] - _v2);
                    float s = Math.Min(s1, s2);

                    if (s > _radius)
                    {
                        // No collision
                        axis.Type = EPAxisType.EdgeB;
                        axis.Index = i;
                        axis.Separation = s;
                        return axis;
                    }

                    // Adjacency
                    if (FVector2.Dot(n, perp) >= 0.0f)
                    {
                        if (FVector2.Dot(n - _upperLimit, _normal) < -Settings.AngularSlop)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (FVector2.Dot(n - _lowerLimit, _normal) < -Settings.AngularSlop)
                        {
                            continue;
                        }
                    }

                    if (s > axis.Separation)
                    {
                        axis.Type = EPAxisType.EdgeB;
                        axis.Index = i;
                        axis.Separation = s;
                    }
                }

                return axis;
            }
        }

        /// <summary>
        /// Clipping for contact manifolds.
        /// </summary>
        /// <param name="vOut">The v out.</param>
        /// <param name="vIn">The v in.</param>
        /// <param name="normal">The normal.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="vertexIndexA">The vertex index A.</param>
        /// <returns></returns>
        private static int ClipSegmentToLine(out FixedArray2<ClipVertex> vOut, ref FixedArray2<ClipVertex> vIn,
                                             FVector2 normal, float offset, int vertexIndexA)
        {
            vOut = new FixedArray2<ClipVertex>();

            ClipVertex v0 = vIn[0];
            ClipVertex v1 = vIn[1];

            // Start with no output points
            int numOut = 0;

            // Calculate the distance of end points to the line
            float distance0 = normal.X * v0.V.X + normal.Y * v0.V.Y - offset;
            float distance1 = normal.X * v1.V.X + normal.Y * v1.V.Y - offset;

            // If the points are behind the plane
            if (distance0 <= 0.0f) vOut[numOut++] = v0;
            if (distance1 <= 0.0f) vOut[numOut++] = v1;

            // If the points are on different sides of the plane
            if (distance0 * distance1 < 0.0f)
            {
                // Find intersection point of edge and plane
                float interp = distance0 / (distance0 - distance1);

                ClipVertex cv = vOut[numOut];

                cv.V.X = v0.V.X + interp * (v1.V.X - v0.V.X);
                cv.V.Y = v0.V.Y + interp * (v1.V.Y - v0.V.Y);

                // VertexA is hitting edgeB.
                cv.ID.Features.IndexA = (byte)vertexIndexA;
                cv.ID.Features.IndexB = v0.ID.Features.IndexB;
                cv.ID.Features.TypeA = (byte)ContactFeatureType.Vertex;
                cv.ID.Features.TypeB = (byte)ContactFeatureType.Face;

                vOut[numOut] = cv;

                ++numOut;
            }

            return numOut;
        }

        /// <summary>
        /// Find the separation between poly1 and poly2 for a give edge normal on poly1.
        /// </summary>
        /// <param name="poly1">The poly1.</param>
        /// <param name="xf1">The XF1.</param>
        /// <param name="edge1">The edge1.</param>
        /// <param name="poly2">The poly2.</param>
        /// <param name="xf2">The XF2.</param>
        /// <returns></returns>
        private static float EdgeSeparation(PolygonShape poly1, ref Transform xf1, int edge1,
                                            PolygonShape poly2, ref Transform xf2)
        {
            List<FVector2> vertices1 = poly1.Vertices;
            List<FVector2> normals1 = poly1.Normals;

            int count2 = poly2.Vertices.Count;
            List<FVector2> vertices2 = poly2.Vertices;

            Debug.Assert(0 <= edge1 && edge1 < poly1.Vertices.Count);

            // Convert normal from poly1's frame into poly2's frame.
            FVector2 normal1World = MathUtils.Mul(xf1.q, normals1[edge1]);
            FVector2 normal1 = MathUtils.MulT(xf2.q, normal1World);

            // Find support vertex on poly2 for -normal.
            int index = 0;
            float minDot = Settings.MaxFloat;

            for (int i = 0; i < count2; ++i)
            {
                float dot = FVector2.Dot(vertices2[i], normal1);
                if (dot < minDot)
                {
                    minDot = dot;
                    index = i;
                }
            }

            FVector2 v1 = MathUtils.Mul(ref xf1, vertices1[edge1]);
            FVector2 v2 = MathUtils.Mul(ref xf2, vertices2[index]);
            float separation = FVector2.Dot(v2 - v1, normal1World);
            return separation;
        }

        /// <summary>
        /// Find the max separation between poly1 and poly2 using edge normals from poly1.
        /// </summary>
        /// <param name="edgeIndex">Index of the edge.</param>
        /// <param name="poly1">The poly1.</param>
        /// <param name="xf1">The XF1.</param>
        /// <param name="poly2">The poly2.</param>
        /// <param name="xf2">The XF2.</param>
        /// <returns></returns>
        private static float FindMaxSeparation(out int edgeIndex,
                                               PolygonShape poly1, ref Transform xf1,
                                               PolygonShape poly2, ref Transform xf2)
        {
            int count1 = poly1.Vertices.Count;
            List<FVector2> normals1 = poly1.Normals;

            // Vector pointing from the centroid of poly1 to the centroid of poly2.
            FVector2 d = xf2.p - xf1.p;
            FVector2 dLocal1 = MathUtils.MulT(xf1.q, d);

            // Find edge normal on poly1 that has the largest projection onto d.
            int edge = 0;
            float maxDot = -Settings.MaxFloat;
            for (int i = 0; i < count1; ++i)
            {
                float dot = FVector2.Dot(normals1[i], dLocal1);
                if (dot > maxDot)
                {
                    maxDot = dot;
                    edge = i;
                }
            }

            // Get the separation for the edge normal.
            float s = EdgeSeparation(poly1, ref  xf1, edge, poly2, ref xf2);

            // Check the separation for the previous edge normal.
            int prevEdge = edge - 1 >= 0 ? edge - 1 : count1 - 1;
            float sPrev = EdgeSeparation(poly1, ref  xf1, prevEdge, poly2, ref xf2);

            // Check the separation for the next edge normal.
            int nextEdge = edge + 1 < count1 ? edge + 1 : 0;
            float sNext = EdgeSeparation(poly1, ref xf1, nextEdge, poly2, ref xf2);

            // Find the best edge and the search direction.
            int bestEdge;
            float bestSeparation;
            int increment;
            if (sPrev > s && sPrev > sNext)
            {
                increment = -1;
                bestEdge = prevEdge;
                bestSeparation = sPrev;
            }
            else if (sNext > s)
            {
                increment = 1;
                bestEdge = nextEdge;
                bestSeparation = sNext;
            }
            else
            {
                edgeIndex = edge;
                return s;
            }

            // Perform a local search for the best edge normal.
            for (; ; )
            {
                if (increment == -1)
                    edge = bestEdge - 1 >= 0 ? bestEdge - 1 : count1 - 1;
                else
                    edge = bestEdge + 1 < count1 ? bestEdge + 1 : 0;

                s = EdgeSeparation(poly1, ref xf1, edge, poly2, ref xf2);

                if (s > bestSeparation)
                {
                    bestEdge = edge;
                    bestSeparation = s;
                }
                else
                {
                    break;
                }
            }

            edgeIndex = bestEdge;
            return bestSeparation;
        }

        private static void FindIncidentEdge(out FixedArray2<ClipVertex> c,
                                             PolygonShape poly1, ref Transform xf1, int edge1,
                                             PolygonShape poly2, ref Transform xf2)
        {
            c = new FixedArray2<ClipVertex>();
            Vertices normals1 = poly1.Normals;

            int count2 = poly2.Vertices.Count;
            Vertices vertices2 = poly2.Vertices;
            Vertices normals2 = poly2.Normals;

            Debug.Assert(0 <= edge1 && edge1 < poly1.Vertices.Count);

            // Get the normal of the reference edge in poly2's frame.
            FVector2 normal1 = MathUtils.MulT(xf2.q, MathUtils.Mul(xf1.q, normals1[edge1]));


            // Find the incident edge on poly2.
            int index = 0;
            float minDot = Settings.MaxFloat;
            for (int i = 0; i < count2; ++i)
            {
                float dot = FVector2.Dot(normal1, normals2[i]);
                if (dot < minDot)
                {
                    minDot = dot;
                    index = i;
                }
            }

            // Build the clip vertices for the incident edge.
            int i1 = index;
            int i2 = i1 + 1 < count2 ? i1 + 1 : 0;

            ClipVertex cv0 = c[0];

            cv0.V = MathUtils.Mul(ref xf2, vertices2[i1]);
            cv0.ID.Features.IndexA = (byte)edge1;
            cv0.ID.Features.IndexB = (byte)i1;
            cv0.ID.Features.TypeA = (byte)ContactFeatureType.Face;
            cv0.ID.Features.TypeB = (byte)ContactFeatureType.Vertex;

            c[0] = cv0;

            ClipVertex cv1 = c[1];
            cv1.V = MathUtils.Mul(ref xf2, vertices2[i2]);
            cv1.ID.Features.IndexA = (byte)edge1;
            cv1.ID.Features.IndexB = (byte)i2;
            cv1.ID.Features.TypeA = (byte)ContactFeatureType.Face;
            cv1.ID.Features.TypeB = (byte)ContactFeatureType.Vertex;

            c[1] = cv1;
        }
    }
}