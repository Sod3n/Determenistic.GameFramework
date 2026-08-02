// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// Contribution of original implementation:
// Andre Fecteau <andre.fecteau1@gmail.com>
// Ported from C++ to C# for deterministic fixed-point math.

using System.Collections.Generic;
using Vector2 = Deterministic.GameFramework.Types.Vector2;
using Float = Deterministic.GameFramework.Types.Float;

namespace Deterministic.GameFramework.CDT
{
    internal enum NodeSplitDirection
    {
        X,
        Y,
    }

    /// <summary>
    /// Simple KD-tree with alternating half-splitting nodes.
    /// Incrementally add points, then query for nearest neighbor.
    /// Does not check for duplicates; expects unique points.
    /// Uses an external point buffer to reduce memory footprint.
    /// </summary>
    internal class KDTree
    {
        private const int NumVerticesInLeaf = 32;
        private const int InitialStackDepth = 32;
        private const int StackDepthIncrement = 32;

        private static readonly Float MaxFloat = Float.FromRaw(long.MaxValue);

        /// <summary>
        /// Stores KD-tree node data.
        /// </summary>
        private struct Node
        {
            /// <summary>Two children indices if not leaf; both 0 if leaf.</summary>
            public int Child0;
            public int Child1;
            /// <summary>Point indices if this is a leaf node.</summary>
            public List<int> Data;

            public static Node CreateLeaf()
            {
                return new Node
                {
                    Child0 = 0,
                    Child1 = 0,
                    Data = new List<int>(NumVerticesInLeaf),
                };
            }

            public void SetChildren(int c1, int c2)
            {
                Child0 = c1;
                Child1 = c2;
            }

            /// <summary>A leaf has no valid children (both are the same index).</summary>
            public bool IsLeaf => Child0 == Child1;

            public int GetChild(int index) => index == 0 ? Child0 : Child1;
        }

        /// <summary>
        /// Task entry for stack-based nearest-neighbor traversal.
        /// </summary>
        private struct NearestTask
        {
            public int Node;
            public Vector2 Min;
            public Vector2 Max;
            public NodeSplitDirection Dir;
            public Float DistSq;

            public NearestTask(int node, Vector2 min, Vector2 max, NodeSplitDirection dir, Float distSq)
            {
                Node = node;
                Min = min;
                Max = max;
                Dir = dir;
                DistSq = distSq;
            }
        }

        private int m_root;
        private readonly List<Node> m_nodes;
        private NodeSplitDirection m_rootDir;
        private Vector2 m_min;
        private Vector2 m_max;
        private int m_size;
        private bool m_isRootBoxInitialized;

        // Pre-allocated stack for nearest-neighbor queries (avoids per-query allocation).
        private NearestTask[] m_tasksStack;

        /// <summary>
        /// Default constructor. Bounding box is lazily initialized when the root
        /// leaf first overflows.
        /// </summary>
        public KDTree()
        {
            m_rootDir = NodeSplitDirection.X;
            m_min = new Vector2(-MaxFloat, -MaxFloat);
            m_max = new Vector2(MaxFloat, MaxFloat);
            m_size = 0;
            m_isRootBoxInitialized = false;
            m_nodes = new List<Node>();
            m_tasksStack = new NearestTask[InitialStackDepth];
            m_root = AddNewNode();
        }

        /// <summary>
        /// Constructor with bounding box known in advance.
        /// </summary>
        public KDTree(Vector2 min, Vector2 max)
        {
            m_rootDir = NodeSplitDirection.X;
            m_min = min;
            m_max = max;
            m_size = 0;
            m_isRootBoxInitialized = true;
            m_nodes = new List<Node>();
            m_tasksStack = new NearestTask[InitialStackDepth];
            m_root = AddNewNode();
        }

        public int Size => m_size;

        /// <summary>
        /// Insert a point into the KD-tree.
        /// </summary>
        /// <param name="iPoint">Index of the point in the external point buffer.</param>
        /// <param name="points">External point buffer.</param>
        public void Insert(int iPoint, List<Vector2> points)
        {
            ++m_size;
            // If point is outside root box, extend tree by adding new roots.
            Vector2 pos = points[iPoint];
            while (!IsInsideBox(pos, m_min, m_max))
            {
                ExtendTree(pos);
            }

            int node = m_root;
            Vector2 min = m_min;
            Vector2 max = m_max;
            NodeSplitDirection dir = m_rootDir;

            NodeSplitDirection newDir = NodeSplitDirection.X;
            Float mid = (Float)0;
            Vector2 newMin = default;
            Vector2 newMax = default;

            while (true)
            {
                Node n = m_nodes[node];
                if (n.IsLeaf)
                {
                    // Add point if capacity is not reached.
                    if (n.Data.Count < NumVerticesInLeaf)
                    {
                        n.Data.Add(iPoint);
                        // Write back since Node is a struct.
                        m_nodes[node] = n;
                        return;
                    }
                    // Initialize bounding box the first time the root capacity is reached.
                    if (!m_isRootBoxInitialized)
                    {
                        InitializeRootBox(points);
                        min = m_min;
                        max = m_max;
                    }
                    // Split a full leaf node.
                    CalcSplitInfo(min, max, dir, out mid, out newDir, out newMin, out newMax);
                    int c1 = AddNewNode();
                    int c2 = AddNewNode();
                    // Re-read node after potential list growth.
                    n = m_nodes[node];
                    n.SetChildren(c1, c2);
                    m_nodes[node] = n;

                    Node c1Node = m_nodes[c1];
                    Node c2Node = m_nodes[c2];
                    // Move node's points to children.
                    List<int> data = n.Data;
                    for (int i = 0; i < data.Count; i++)
                    {
                        int ptIdx = data[i];
                        if (WhichChild(points[ptIdx], mid, dir) == 0)
                            c1Node.Data.Add(ptIdx);
                        else
                            c2Node.Data.Add(ptIdx);
                    }
                    m_nodes[c1] = c1Node;
                    m_nodes[c2] = c2Node;

                    // Clear the leaf data (node is now an internal node).
                    n.Data = new List<int>();
                    m_nodes[node] = n;
                }
                else
                {
                    CalcSplitInfo(min, max, dir, out mid, out newDir, out newMin, out newMax);
                }

                // Add the point to a child.
                int iChild = WhichChild(points[iPoint], mid, dir);
                if (iChild == 0)
                    max = newMax;
                else
                    min = newMin;
                node = m_nodes[node].GetChild(iChild);
                dir = newDir;
            }
        }

        /// <summary>
        /// Query the KD-tree for the nearest neighbor to a given point.
        /// </summary>
        /// <param name="point">Query point position.</param>
        /// <param name="points">External point buffer.</param>
        /// <returns>The position and index of the nearest point.</returns>
        public (Vector2 Point, int Index) Nearest(Vector2 point, List<Vector2> points)
        {
            Vector2 outPoint = default;
            int outIndex = 0;
            int iTask = -1;
            Float minDistSq = MaxFloat;

            EnsureStackCapacity(1);
            m_tasksStack[++iTask] = new NearestTask(
                m_root,
                m_min,
                m_max,
                m_rootDir,
                DistanceSquaredToBox(point, m_min, m_max));

            while (iTask != -1)
            {
                NearestTask t = m_tasksStack[iTask--];
                if (t.DistSq > minDistSq)
                    continue;

                Node n = m_nodes[t.Node];
                if (n.IsLeaf)
                {
                    List<int> data = n.Data;
                    for (int i = 0; i < data.Count; i++)
                    {
                        int ptIdx = data[i];
                        Vector2 p = points[ptIdx];
                        Float distSq = DistanceSquared(point, p);
                        if (distSq < minDistSq)
                        {
                            minDistSq = distSq;
                            outPoint = p;
                            outIndex = ptIdx;
                        }
                    }
                }
                else
                {
                    Float mid = (Float)0;
                    NodeSplitDirection newDir = NodeSplitDirection.X;
                    Vector2 newMin = t.Min;
                    Vector2 newMax = t.Max;
                    Float dSqFarther = MaxFloat;

                    switch (t.Dir)
                    {
                        case NodeSplitDirection.X:
                        {
                            mid = (t.Min.X + t.Max.X) / (Float)2;
                            newDir = NodeSplitDirection.Y;
                            newMin.X = mid;
                            newMax.X = mid;
                            Float dx = point.X - mid;
                            Float dy = Float.Max(
                                Float.Max(t.Min.Y - point.Y, (Float)0),
                                point.Y - t.Max.Y);
                            dSqFarther = dx * dx + dy * dy;
                            break;
                        }
                        case NodeSplitDirection.Y:
                        {
                            mid = (t.Min.Y + t.Max.Y) / (Float)2;
                            newDir = NodeSplitDirection.X;
                            newMin.Y = mid;
                            newMax.Y = mid;
                            Float dx = Float.Max(
                                Float.Max(t.Min.X - point.X, (Float)0),
                                point.X - t.Max.X);
                            Float dy = point.Y - mid;
                            dSqFarther = dx * dx + dy * dy;
                            break;
                        }
                    }

                    EnsureStackCapacity(iTask + 3);

                    // Put the closest node on top of the stack.
                    if (IsAfterSplit(point, mid, t.Dir))
                    {
                        if (dSqFarther <= minDistSq)
                        {
                            m_tasksStack[++iTask] = new NearestTask(
                                n.Child0, t.Min, newMax, newDir, dSqFarther);
                        }
                        m_tasksStack[++iTask] = new NearestTask(
                            n.Child1, newMin, t.Max, newDir, t.DistSq);
                    }
                    else
                    {
                        if (dSqFarther <= minDistSq)
                        {
                            m_tasksStack[++iTask] = new NearestTask(
                                n.Child1, newMin, t.Max, newDir, dSqFarther);
                        }
                        m_tasksStack[++iTask] = new NearestTask(
                            n.Child0, t.Min, newMax, newDir, t.DistSq);
                    }
                }
            }

            return (outPoint, outIndex);
        }

        // -------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------

        /// <summary>Add a new leaf node and return its index.</summary>
        private int AddNewNode()
        {
            int index = m_nodes.Count;
            m_nodes.Add(Node.CreateLeaf());
            return index;
        }

        private static bool IsAfterSplit(Vector2 point, Float split, NodeSplitDirection dir)
        {
            return dir == NodeSplitDirection.X ? point.X > split : point.Y > split;
        }

        /// <summary>
        /// Determine which child a point belongs to after a split.
        /// Returns 0 for the first child, 1 for the second.
        /// </summary>
        private static int WhichChild(Vector2 point, Float split, NodeSplitDirection dir)
        {
            return IsAfterSplit(point, split, dir) ? 1 : 0;
        }

        /// <summary>
        /// Calculate split location, direction, and children boxes.
        /// </summary>
        private static void CalcSplitInfo(
            Vector2 min,
            Vector2 max,
            NodeSplitDirection dir,
            out Float midOut,
            out NodeSplitDirection newDirOut,
            out Vector2 newMinOut,
            out Vector2 newMaxOut)
        {
            newMaxOut = max;
            newMinOut = min;
            switch (dir)
            {
                case NodeSplitDirection.X:
                    midOut = (min.X + max.X) / (Float)2;
                    newDirOut = NodeSplitDirection.Y;
                    newMinOut.X = midOut;
                    newMaxOut.X = midOut;
                    return;
                case NodeSplitDirection.Y:
                default:
                    midOut = (min.Y + max.Y) / (Float)2;
                    newDirOut = NodeSplitDirection.X;
                    newMinOut.Y = midOut;
                    newMaxOut.Y = midOut;
                    return;
            }
        }

        /// <summary>Test if a point is inside an axis-aligned bounding box.</summary>
        private static bool IsInsideBox(Vector2 p, Vector2 min, Vector2 max)
        {
            return p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y;
        }

        /// <summary>
        /// Extend the tree by creating a new root with the old root and a new
        /// empty leaf as children. The bounding box is doubled in size in the
        /// direction opposite to the current root split.
        /// </summary>
        private void ExtendTree(Vector2 point)
        {
            int newRoot = AddNewNode();
            int newLeaf = AddNewNode();
            Node newRootNode = m_nodes[newRoot];

            switch (m_rootDir)
            {
                case NodeSplitDirection.X:
                    m_rootDir = NodeSplitDirection.Y;
                    if (point.Y < m_min.Y)
                    {
                        m_min.Y -= m_max.Y - m_min.Y;
                        newRootNode.SetChildren(newLeaf, m_root);
                    }
                    else
                    {
                        m_max.Y += m_max.Y - m_min.Y;
                        newRootNode.SetChildren(m_root, newLeaf);
                    }
                    break;
                case NodeSplitDirection.Y:
                    m_rootDir = NodeSplitDirection.X;
                    if (point.X < m_min.X)
                    {
                        m_min.X -= m_max.X - m_min.X;
                        newRootNode.SetChildren(newLeaf, m_root);
                    }
                    else
                    {
                        m_max.X += m_max.X - m_min.X;
                        newRootNode.SetChildren(m_root, newLeaf);
                    }
                    break;
            }

            m_nodes[newRoot] = newRootNode;
            m_root = newRoot;
        }

        /// <summary>
        /// Calculate the root bounding box from the points currently stored in the root leaf.
        /// Called once, the first time the root leaf overflows.
        /// </summary>
        private void InitializeRootBox(List<Vector2> points)
        {
            Node rootNode = m_nodes[m_root];
            List<int> data = rootNode.Data;
            m_min = points[data[0]];
            m_max = m_min;
            for (int i = 0; i < data.Count; i++)
            {
                Vector2 p = points[data[i]];
                m_min = new Vector2(Float.Min(m_min.X, p.X), Float.Min(m_min.Y, p.Y));
                m_max = new Vector2(Float.Max(m_max.X, p.X), Float.Max(m_max.Y, p.Y));
            }
            // Ensure bounding box has non-zero size so it can be extended properly.
            Float padding = (Float)1;
            if (m_min.X == m_max.X)
            {
                m_min.X -= padding;
                m_max.X += padding;
            }
            if (m_min.Y == m_max.Y)
            {
                m_min.Y -= padding;
                m_max.Y += padding;
            }
            m_isRootBoxInitialized = true;
        }

        /// <summary>
        /// Squared distance from a point to an axis-aligned bounding box.
        /// Returns zero if the point is inside the box.
        /// </summary>
        private static Float DistanceSquaredToBox(Vector2 p, Vector2 min, Vector2 max)
        {
            Float dx = Float.Max(Float.Max(min.X - p.X, (Float)0), p.X - max.X);
            Float dy = Float.Max(Float.Max(min.Y - p.Y, (Float)0), p.Y - max.Y);
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Squared distance between two points.
        /// </summary>
        private static Float DistanceSquared(Vector2 a, Vector2 b)
        {
            Float dx = a.X - b.X;
            Float dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Ensure the tasks stack can hold at least <paramref name="requiredCapacity"/> entries.
        /// </summary>
        private void EnsureStackCapacity(int requiredCapacity)
        {
            if (requiredCapacity >= m_tasksStack.Length)
            {
                int newSize = m_tasksStack.Length + StackDepthIncrement;
                if (newSize < requiredCapacity + 1)
                    newSize = requiredCapacity + 1;
                var newStack = new NearestTask[newSize];
                System.Array.Copy(m_tasksStack, newStack, m_tasksStack.Length);
                m_tasksStack = newStack;
            }
        }
    }

    /// <summary>
    /// Adapter between KDTree and CDT. Provides the locator pattern interface
    /// (Initialize, AddPoint, NearPoint) used by the triangulation algorithm.
    /// </summary>
    internal class LocatorKDTree
    {
        private KDTree m_kdTree;

        public LocatorKDTree()
        {
            m_kdTree = new KDTree();
        }

        /// <summary>
        /// Initialize the KD-tree with a set of points. Computes the bounding box
        /// and inserts all points.
        /// </summary>
        public void Initialize(List<Vector2> points)
        {
            Vector2 min = points[0];
            Vector2 max = min;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                min = new Vector2(Float.Min(min.X, p.X), Float.Min(min.Y, p.Y));
                max = new Vector2(Float.Max(max.X, p.X), Float.Max(max.Y, p.Y));
            }
            m_kdTree = new KDTree(min, max);
            for (int i = 0; i < points.Count; i++)
            {
                m_kdTree.Insert(i, points);
            }
        }

        /// <summary>
        /// Add a single point to the KD-tree.
        /// </summary>
        public void AddPoint(int i, List<Vector2> points)
        {
            m_kdTree.Insert(i, points);
        }

        /// <summary>
        /// Find the index of the nearest point to the given position.
        /// </summary>
        public int NearPoint(Vector2 pos, List<Vector2> points)
        {
            return m_kdTree.Nearest(pos, points).Index;
        }

        public int Size => m_kdTree.Size;

        public bool Empty => Size == 0;
    }
}
