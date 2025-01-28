using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using Seb.Helpers;

namespace Seb.AccelerationStructures
{
	public class BVH
	{
		public readonly NodeList allNodes;
		public readonly Triangle[] allTris;
		public BuildStats stats;

		public Triangle[] GetTriangles() => allTris;

		readonly BVHTriangle[] AllTriangles;

		public BVH(Vector3[] verts, int[] indices, Vector3[] normals)
		{
			// Start recording stats
			var sw = System.Diagnostics.Stopwatch.StartNew();
			stats = new();

			// Construct BVH
			allNodes = new();
			AllTriangles = new BVHTriangle[indices.Length / 3];
			BoundingBox bounds = new BoundingBox();

			for (int i = 0; i < indices.Length; i += 3)
			{
				Vector3 a = verts[indices[i + 0]];
				Vector3 b = verts[indices[i + 1]];
				Vector3 c = verts[indices[i + 2]];
				Vector3 centre = (a + b + c) / 3;
				Vector3 max = Vector3.Max(Vector3.Max(a, b), c);
				Vector3 min = Vector3.Min(Vector3.Min(a, b), c);
				AllTriangles[i / 3] = new BVHTriangle(centre, min, max, i);
				bounds.GrowToInclude(min, max);
			}

			allNodes.Add(new Node(bounds));
			Split(0, verts, 0, AllTriangles.Length);

			allTris = new Triangle[AllTriangles.Length];
			for (int i = 0; i < AllTriangles.Length; i++)
			{
				BVHTriangle buildTri = AllTriangles[i];
				Vector3 a = verts[indices[buildTri.Index + 0]];
				Vector3 b = verts[indices[buildTri.Index + 1]];
				Vector3 c = verts[indices[buildTri.Index + 2]];
				Vector3 norm_a = normals[indices[buildTri.Index + 0]];
				Vector3 norm_b = normals[indices[buildTri.Index + 1]];
				Vector3 norm_c = normals[indices[buildTri.Index + 2]];
				allTris[i] = new Triangle(a, b, c, norm_a, norm_b, norm_c);
			}

			// Finish recording stats
			sw.Stop();
			stats.TimeMs = (int)sw.ElapsedMilliseconds;
		}


		public (bool hit, float dst, Vector3 pos, bool backface) Search(Vector3 rayOrigin, Vector3 rayDir)
		{
			Stack<Node> stack = new();
			stack.Push(allNodes.Nodes[0]);

			float minDst = float.MaxValue;
			int hitTriangleIndex = -1;
			Vector3 hitPoint = Vector3.zero;
			bool backface = false;

			while (stack.Count > 0)
			{
				Node node = stack.Pop();

				if (node.TriangleCount > 0)
				{
					for (int i = 0; i < node.TriangleCount; i++)
					{
						int triIndex = node.StartIndex + i;
						Triangle tri = allTris[triIndex];
						var hitInfo = RayTriangle(rayOrigin, rayDir, tri);
						if (hitInfo.hit)
						{
							if (hitInfo.dst < minDst)
							{
								minDst = hitInfo.dst;
								hitTriangleIndex = triIndex;
								hitPoint = rayOrigin + rayDir * minDst;
								backface = hitInfo.backface;
							}
						}
					}
				}
				else
				{
					Node childA = allNodes.Nodes[node.StartIndex];
					Node childB = allNodes.Nodes[node.StartIndex + 1];

					float dstA = RayBoundingBox(rayOrigin, rayDir, childA.BoundsMin, childA.BoundsMax).dst;
					float dstB = RayBoundingBox(rayOrigin, rayDir, childB.BoundsMin, childB.BoundsMax).dst;

					if (dstA > dstB)
					{
						if (dstA < minDst) stack.Push(childA);
						if (dstB < minDst) stack.Push(childB);
					}
					else
					{
						if (dstB < minDst) stack.Push(childB);
						if (dstA < minDst) stack.Push(childA);
					}
				}
			}

			return (hitTriangleIndex != -1, minDst, hitPoint, backface);
		}

		public static (bool hit, float dst) RayBoundingBox(Vector3 rayOrigin, Vector3 rayDir, Vector3 boxMin, Vector3 boxMax)
		{
			float invDirX = rayDir.x == 0 ? float.PositiveInfinity : 1 / rayDir.x;
			float invDirY = rayDir.y == 0 ? float.PositiveInfinity : 1 / rayDir.y;
			float invDirZ = rayDir.z == 0 ? float.PositiveInfinity : 1 / rayDir.z;

			float tx1 = (boxMin.x - rayOrigin.x) * invDirX;
			float tx2 = (boxMax.x - rayOrigin.x) * invDirX;
			float tmin = Mathf.Min(tx1, tx2);
			float tmax = Mathf.Max(tx1, tx2);

			float ty1 = (boxMin.y - rayOrigin.y) * invDirY;
			float ty2 = (boxMax.y - rayOrigin.y) * invDirY;
			tmin = Mathf.Max(tmin, Mathf.Min(ty1, ty2));
			tmax = Mathf.Min(tmax, Mathf.Max(ty1, ty2));

			float tz1 = (boxMin.z - rayOrigin.z) * invDirZ;
			float tz2 = (boxMax.z - rayOrigin.z) * invDirZ;
			tmin = Mathf.Max(tmin, Mathf.Min(tz1, tz2));
			tmax = Mathf.Min(tmax, Mathf.Max(tz1, tz2));

			bool hit = tmax >= tmin && tmax > 0;
			float dst = tmin > 0 ? tmin : tmax;
			if (!hit) dst = Mathf.Infinity;
			return (hit, dst);
		}

		static (bool hit, float dst, bool backface) RayTriangle(Vector3 rayOrigin, Vector3 rayDir, Triangle tri)
		{
			Vector3 edgeAB = tri.B - tri.A;
			Vector3 edgeAC = tri.C - tri.A;
			Vector3 ao = rayOrigin - tri.A;
			Vector3 dao = Vector3.Cross(ao, rayDir);
			Vector3 normalVector = Vector3.Cross(edgeAB, edgeAC);

			float determinant = -Vector3.Dot(rayDir, normalVector);
			float invDet = 1 / determinant;

			// Calculate dst to triangle & barycentric coordinates of intersection point
			float dst = Vector3.Dot(ao, normalVector) * invDet;
			float u = Vector3.Dot(edgeAC, dao) * invDet;
			float v = -Vector3.Dot(edgeAB, dao) * invDet;
			float w = 1 - u - v;

			// Initialize hit info
			bool backface = determinant < 0;
			bool hit = Mathf.Abs(determinant) >= 1E-8 && dst >= 0 && u >= 0 && v >= 0 && w >= 0;
			return (hit, dst, backface);
		}

		void Split(int parentIndex, Vector3[] verts, int triGlobalStart, int triNum, int depth = 0)
		{
			const int MaxDepth = 32;
			Node parent = allNodes.Nodes[parentIndex];
			Vector3 size = parent.CalculateBoundsSize();
			float parentCost = NodeCost(size, triNum);

			(int splitAxis, float splitPos, float cost) = ChooseSplit(parent, triGlobalStart, triNum);

			if (cost < parentCost && depth < MaxDepth)
			{
				BoundingBox boundsLeft = new();
				BoundingBox boundsRight = new();
				int numOnLeft = 0;

				for (int i = triGlobalStart; i < triGlobalStart + triNum; i++)
				{
					BVHTriangle tri = AllTriangles[i];
					if (tri.Centre[splitAxis] < splitPos)
					{
						boundsLeft.GrowToInclude(tri.Min, tri.Max);

						BVHTriangle swap = AllTriangles[triGlobalStart + numOnLeft];
						AllTriangles[triGlobalStart + numOnLeft] = tri;
						AllTriangles[i] = swap;
						numOnLeft++;
					}
					else
					{
						boundsRight.GrowToInclude(tri.Min, tri.Max);
					}
				}

				int numOnRight = triNum - numOnLeft;
				int triStartLeft = triGlobalStart + 0;
				int triStartRight = triGlobalStart + numOnLeft;

				// Split parent into two children
				int childIndexLeft = allNodes.Add(new(boundsLeft, triStartLeft, 0));
				int childIndexRight = allNodes.Add(new(boundsRight, triStartRight, 0));

				// Update parent
				parent.StartIndex = childIndexLeft;
				allNodes.Nodes[parentIndex] = parent;
				stats.RecordNode(depth, false);

				// Recursively split children
				Split(childIndexLeft, verts, triGlobalStart, numOnLeft, depth + 1);
				Split(childIndexRight, verts, triGlobalStart + numOnLeft, numOnRight, depth + 1);
			}
			else
			{
				// Parent is actually leaf, assign all triangles to it
				parent.StartIndex = triGlobalStart;
				parent.TriangleCount = triNum;
				allNodes.Nodes[parentIndex] = parent;
				stats.RecordNode(depth, true, triNum);
			}
		}

		(int axis, float pos, float cost) ChooseSplit(Node node, int start, int count)
		{
			if (count <= 1) return (0, 0, float.PositiveInfinity);

			float bestSplitPos = 0;
			int bestSplitAxis = 0;
			const int numSplitTests = 5;

			float bestCost = float.MaxValue;

			// Estimate best split pos
			for (int axis = 0; axis < 3; axis++)
			{
				for (int i = 0; i < numSplitTests; i++)
				{
					float splitT = (i + 1) / (numSplitTests + 1f);
					float splitPos = Mathf.Lerp(node.BoundsMin[axis], node.BoundsMax[axis], splitT);
					float cost = EvaluateSplit(axis, splitPos, start, count);
					if (cost < bestCost)
					{
						bestCost = cost;
						bestSplitPos = splitPos;
						bestSplitAxis = axis;
					}
				}
			}

			return (bestSplitAxis, bestSplitPos, bestCost);
		}

		float EvaluateSplit(int splitAxis, float splitPos, int start, int count)
		{
			BoundingBox boundsLeft = new();
			BoundingBox boundsRight = new();
			int numOnLeft = 0;
			int numOnRight = 0;

			for (int i = start; i < start + count; i++)
			{
				BVHTriangle tri = AllTriangles[i];
				if (tri.Centre[splitAxis] < splitPos)
				{
					boundsLeft.GrowToInclude(tri.Min, tri.Max);
					numOnLeft++;
				}
				else
				{
					boundsRight.GrowToInclude(tri.Min, tri.Max);
					numOnRight++;
				}
			}

			float costA = NodeCost(boundsLeft.Size, numOnLeft);
			float costB = NodeCost(boundsRight.Size, numOnRight);
			return costA + costB;
		}

		static float NodeCost(Vector3 size, int numTriangles)
		{
			float area = 2 * (size.x * size.y + size.x * size.z + size.y * size.z);
			return area * numTriangles;
		}

		public struct Node
		{
			public Vector3 BoundsMin;

			public Vector3 BoundsMax;

			// Index of first child (if triangle count is negative) otherwise index of first triangle
			public int StartIndex;
			public int TriangleCount;

			public Node(BoundingBox bounds) : this()
			{
				BoundsMin = bounds.Min;
				BoundsMax = bounds.Max;
				StartIndex = -1;
				TriangleCount = -1;
			}

			public Node(BoundingBox bounds, int startIndex, int triCount)
			{
				BoundsMin = bounds.Min;
				BoundsMax = bounds.Max;
				StartIndex = startIndex;
				TriangleCount = triCount;
			}

			public Vector3 CalculateBoundsSize() => BoundsMax - BoundsMin;
			public Vector3 CalculateBoundsCentre() => (BoundsMin + BoundsMax) / 2;
		}

		public struct BoundingBox
		{
			public Vector3 Min;
			public Vector3 Max;
			public Vector3 Centre => (Min + Max) / 2;
			public Vector3 Size => Max - Min;
			bool hasPoint;

			public void GrowToInclude(Vector3 min, Vector3 max)
			{
				if (hasPoint)
				{
					Min.x = min.x < Min.x ? min.x : Min.x;
					Min.y = min.y < Min.y ? min.y : Min.y;
					Min.z = min.z < Min.z ? min.z : Min.z;
					Max.x = max.x > Max.x ? max.x : Max.x;
					Max.y = max.y > Max.y ? max.y : Max.y;
					Max.z = max.z > Max.z ? max.z : Max.z;
				}
				else
				{
					hasPoint = true;
					Min = min;
					Max = max;
				}
			}
		}


		public readonly struct BVHTriangle
		{
			public readonly Vector3 Centre;
			public readonly Vector3 Min;
			public readonly Vector3 Max;
			public readonly int Index;

			public BVHTriangle(Vector3 centre, Vector3 min, Vector3 max, int index)
			{
				Centre = centre;
				Min = min;
				Max = max;
				Index = index;
			}
		}

		public Node[] GetNodes() => allNodes.Nodes.AsSpan(0, allNodes.NodeCount).ToArray();


		public class NodeList
		{
			public Node[] Nodes = new Node[256];
			int Index;

			public int Add(Node node)
			{
				if (Index >= Nodes.Length)
				{
					Array.Resize(ref Nodes, Nodes.Length * 2);
				}

				int nodeIndex = Index;
				Nodes[Index++] = node;
				return nodeIndex;
			}

			public int NodeCount => Index;
		}

		[System.Serializable]
		public class BuildStats
		{
			public int TimeMs;
			public int TriangleCount;
			public int TotalNodeCount;
			public int LeafNodeCount;

			public int LeafDepthMax;
			public int LeafDepthMin = int.MaxValue;
			public int LeafDepthSum;

			public int LeafMaxTriCount;
			public int LeafMinTriCount = int.MaxValue;

			public void RecordNode(int depth, bool isLeaf, int triCount = 0)
			{
				TotalNodeCount++;

				if (isLeaf)
				{
					LeafNodeCount++;
					LeafDepthSum += depth;
					LeafDepthMin = Mathf.Min(LeafDepthMin, depth);
					LeafDepthMax = Mathf.Max(LeafDepthMax, depth);
					TriangleCount += triCount;

					LeafMaxTriCount = Mathf.Max(LeafMaxTriCount, triCount);
					LeafMinTriCount = Mathf.Min(LeafMinTriCount, triCount);
				}
			}


			public override string ToString()
			{
				var sb = new StringBuilder();
				sb.AppendLine($"Time (BVH): {TimeMs} ms");
				sb.AppendLine($"Triangles: {TriangleCount}");
				sb.AppendLine($"Node Count: {TotalNodeCount}");
				sb.AppendLine($"Leaf Count: {LeafNodeCount}");
				sb.AppendLine($"Leaf Depth:");
				sb.AppendLine($" - Min: {LeafDepthMin}");
				sb.AppendLine($" - Max: {LeafDepthMax}");
				sb.AppendLine($" - Mean: {LeafDepthSum / (float)LeafNodeCount:0.####}");
				sb.AppendLine($"Leaf Tris:");
				sb.AppendLine($" - Min: {LeafMinTriCount}");
				sb.AppendLine($" - Max: {LeafMaxTriCount}");
				sb.AppendLine($" - Mean: {TriangleCount / (float)LeafNodeCount:0.####}");
				// sb.AppendLine($"Leaf Nodes:")

				return sb.ToString();
			}
		}


		public readonly struct Triangle
		{
			public readonly Vector3 A;
			public readonly Vector3 B;
			public readonly Vector3 C;

			public readonly Vector3 normalA;
			public readonly Vector3 normalB;
			public readonly Vector3 normalC;

			public Triangle(Vector3 posA, Vector3 posB, Vector3 posC, Vector3 normalA, Vector3 normalB, Vector3 normalC)
			{
				this.A = posA;
				this.B = posB;
				this.C = posC;
				this.normalA = normalA;
				this.normalB = normalB;
				this.normalC = normalC;
			}
		}
	}
}