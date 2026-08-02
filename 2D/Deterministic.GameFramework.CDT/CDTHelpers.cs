/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Vector2 = Deterministic.GameFramework.Types.Vector2;
using Float = Deterministic.GameFramework.Types.Float;

namespace Deterministic.GameFramework.CDT;

/// <summary>
/// Information about removed duplicated vertices.
/// Contains mapping information and removed duplicate indices.
/// </summary>
/// <remarks>
/// Vertices {0,1,2,3,4} where 0 and 3 are the same will produce mapping
/// {0,1,2,0,3} (to new vertices {0,1,2,3}) and duplicates {3}.
/// </remarks>
public struct DuplicatesInfo
{
    /// <summary>Vertex index mapping from old indices to new indices.</summary>
    public int[] Mapping;

    /// <summary>Indices of duplicate vertices that were removed.</summary>
    public List<int> Duplicates;
}

/// <summary>
/// Public API helper functions for working with CDT triangulation results.
/// Ported from the CDT C++ library.
/// </summary>
public static class CDTHelpers
{
    /// <summary>
    /// Calculate triangles adjacent to each vertex (triangles by vertex index).
    /// </summary>
    /// <param name="triangles">The triangulation triangles.</param>
    /// <param name="verticesSize">Total number of vertices to pre-allocate the output.</param>
    /// <returns>For each vertex index, a list of triangle indices that contain that vertex.</returns>
    public static List<List<int>> CalculateTrianglesByVertex(List<Triangle> triangles, int verticesSize)
    {
        var vertTris = new List<List<int>>(verticesSize);
        for (int i = 0; i < verticesSize; i++)
        {
            vertTris.Add(new List<int>());
        }

        for (int iT = 0; iT < triangles.Count; iT++)
        {
            Triangle t = triangles[iT];
            vertTris[t.V0].Add(iT);
            vertTris[t.V1].Add(iT);
            vertTris[t.V2].Add(iT);
        }

        return vertTris;
    }

    /// <summary>
    /// Find duplicate vertices in the given list.
    /// Duplicates are vertices with exactly the same X and Y coordinates.
    /// </summary>
    /// <param name="vertices">The list of vertices to scan for duplicates.</param>
    /// <returns>
    /// A <see cref="DuplicatesInfo"/> containing the index mapping (old to new)
    /// and the list of duplicate indices.
    /// </returns>
    public static DuplicatesInfo FindDuplicates(List<Vector2> vertices)
    {
        var uniqueVerts = new Dictionary<Vector2, int>();
        var di = new DuplicatesInfo
        {
            Mapping = new int[vertices.Count],
            Duplicates = new List<int>()
        };

        int iOut = 0;
        for (int iIn = 0; iIn < vertices.Count; iIn++)
        {
            Vector2 v = vertices[iIn];
            if (uniqueVerts.TryGetValue(v, out int existingIndex))
            {
                // Found a duplicate
                di.Mapping[iIn] = existingIndex;
                di.Duplicates.Add(iIn);
            }
            else
            {
                uniqueVerts[v] = iOut;
                di.Mapping[iIn] = iOut;
                iOut++;
            }
        }

        return di;
    }

    /// <summary>
    /// Remove elements at the given sorted indices from a list, in-place.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="items">The list to remove elements from.</param>
    /// <param name="sortedIndices">
    /// Sorted unique indices of elements to remove. Must be in ascending order.
    /// </param>
    public static void RemoveAt<T>(List<T> items, List<int> sortedIndices)
    {
        if (sortedIndices.Count == 0)
            return;

        // Walk through chunks of items to keep, shifting them down in-place.
        int destination = sortedIndices[0];
        int ii = 0;

        while (ii < sortedIndices.Count)
        {
            // Advance past consecutive indices-to-remove
            int cur = sortedIndices[ii];
            ii++;
            while (ii < sortedIndices.Count)
            {
                int nxt = sortedIndices[ii];
                if (nxt - cur > 1)
                    break;
                cur = nxt;
                ii++;
            }

            // Move the chunk of elements-to-keep to the destination
            int sourceFirst = sortedIndices[ii - 1] + 1;
            int sourceLast = ii < sortedIndices.Count ? sortedIndices[ii] : items.Count;

            for (int s = sourceFirst; s < sourceLast; s++)
            {
                items[destination] = items[s];
                destination++;
            }
        }

        // Trim the list to the new size
        int newCount = items.Count - sortedIndices.Count;
        items.RemoveRange(newCount, items.Count - newCount);
    }

    /// <summary>
    /// Remove duplicates in-place from a list using the duplicate indices
    /// from <see cref="FindDuplicates"/>.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="items">The list to remove duplicates from.</param>
    /// <param name="duplicateIndices">Sorted indices of duplicate elements to remove.</param>
    public static void RemoveDuplicates<T>(List<T> items, List<int> duplicateIndices)
    {
        RemoveAt(items, duplicateIndices);
    }

    /// <summary>
    /// Remap vertex indices in edges (in-place) using the given vertex-index mapping.
    /// </summary>
    /// <param name="edges">The list of edges to remap.</param>
    /// <param name="mapping">Vertex-index mapping (old index to new index).</param>
    public static void RemapEdges(List<Edge> edges, int[] mapping)
    {
        for (int i = 0; i < edges.Count; i++)
        {
            edges[i] = new Edge(mapping[edges[i].V1], mapping[edges[i].V2]);
        }
    }

    /// <summary>
    /// Find point duplicates, remove them from the vertex list (in-place),
    /// and remap edges (in-place).
    /// Same as a chained call of <see cref="FindDuplicates"/>,
    /// <see cref="RemoveDuplicates{T}"/>, and <see cref="RemapEdges"/>.
    /// </summary>
    /// <param name="vertices">Collection of vertices to remove duplicates from.</param>
    /// <param name="edges">Collection of edges to remap.</param>
    /// <returns>Information about duplicated vertices that were removed.</returns>
    public static DuplicatesInfo RemoveDuplicatesAndRemapEdges(
        List<Vector2> vertices,
        List<Edge> edges)
    {
        DuplicatesInfo di = FindDuplicates(vertices);
        RemoveDuplicates(vertices, di.Duplicates);
        RemapEdges(edges, di.Mapping);
        return di;
    }

    /// <summary>
    /// Extract all edges from the given triangles.
    /// </summary>
    /// <param name="triangles">Triangles to extract edges from.</param>
    /// <returns>An unordered set of all unique edges in the triangulation.</returns>
    public static HashSet<Edge> ExtractEdgesFromTriangles(List<Triangle> triangles)
    {
        var edges = new HashSet<Edge>();
        for (int i = 0; i < triangles.Count; i++)
        {
            Triangle t = triangles[i];
            edges.Add(new Edge(t.V0, t.V1));
            edges.Add(new Edge(t.V1, t.V2));
            edges.Add(new Edge(t.V2, t.V0));
        }
        return edges;
    }

    /// <summary>
    /// Convert piece-to-original-edges mapping to original-edge-to-pieces mapping.
    /// </summary>
    /// <param name="pieceToOriginals">Maps each piece edge to the original edges it belongs to.</param>
    /// <returns>Mapping of original edges to their constituent pieces.</returns>
    public static Dictionary<Edge, List<Edge>> EdgeToPiecesMapping(
        Dictionary<Edge, List<Edge>> pieceToOriginals)
    {
        var originalToPieces = new Dictionary<Edge, List<Edge>>();

        foreach (var kvp in pieceToOriginals)
        {
            Edge piece = kvp.Key;
            List<Edge> originals = kvp.Value;

            for (int i = 0; i < originals.Count; i++)
            {
                Edge original = originals[i];
                if (!originalToPieces.TryGetValue(original, out var pieces))
                {
                    pieces = new List<Edge>();
                    originalToPieces[original] = pieces;
                }
                pieces.Add(piece);
            }
        }

        return originalToPieces;
    }

    /// <summary>
    /// Convert edge-to-pieces mapping into edge-to-split-vertices mapping.
    /// Split vertices are sorted along the edge from start (V1) to end (V2).
    /// </summary>
    /// <param name="edgeToPieces">Edge-to-pieces mapping.</param>
    /// <param name="vertices">Vertex buffer.</param>
    /// <returns>Mapping of each edge to its interior split vertex indices (excluding endpoints).</returns>
    public static Dictionary<Edge, List<int>> EdgeToSplitVertices(
        Dictionary<Edge, List<Edge>> edgeToPieces,
        List<Vector2> vertices)
    {
        var edgeToSplitVerts = new Dictionary<Edge, List<int>>();

        foreach (var kvp in edgeToPieces)
        {
            Edge e = kvp.Key;
            List<Edge> pieces = kvp.Value;

            Float dX = vertices[e.V2].X - vertices[e.V1].X;
            Float dY = vertices[e.V2].Y - vertices[e.V1].Y;
            // Determine which coordinate has the longer span
            bool isX = Float.Abs(dX) >= Float.Abs(dY);
            // Check if the longer coordinate ascends from V1 to V2
            bool isAscending = isX ? dX >= (Float)0 : dY >= (Float)0;

            // Collect all vertices from all pieces with their sort coordinate.
            // size: 2 [ends] + (pieces - 1) [split vertices] = pieces + 1
            var splitVerts = new List<(int vertIndex, Float coord)>(pieces.Count + 1);

            for (int p = 0; p < pieces.Count; p++)
            {
                int v1 = pieces[p].V1;
                int v2 = pieces[p].V2;

                Float c1 = isX ? vertices[v1].X : vertices[v1].Y;
                splitVerts.Add((v1, isAscending ? c1 : -c1));

                Float c2 = isX ? vertices[v2].X : vertices[v2].Y;
                splitVerts.Add((v2, isAscending ? c2 : -c2));
            }

            // Sort by the longest coordinate
            splitVerts.Sort((a, b) => a.coord < b.coord ? -1 : a.coord > b.coord ? 1 : 0);

            // Remove duplicates (same vertex index and coordinate)
            int writePos = 0;
            for (int i = 0; i < splitVerts.Count; i++)
            {
                if (i == 0 ||
                    splitVerts[i].vertIndex != splitVerts[i - 1].vertIndex ||
                    splitVerts[i].coord != splitVerts[i - 1].coord)
                {
                    splitVerts[writePos] = splitVerts[i];
                    writePos++;
                }
            }
            if (writePos < splitVerts.Count)
            {
                splitVerts.RemoveRange(writePos, splitVerts.Count - writePos);
            }

            Debug.Assert(splitVerts.Count > 2, "Expected at least 2 end points plus split vertices");

            // Extract interior split vertices (skip first and last which are endpoints)
            var interior = new List<int>(splitVerts.Count - 2);
            for (int i = 1; i < splitVerts.Count - 1; i++)
            {
                interior.Add(splitVerts[i].vertIndex);
            }

            edgeToSplitVerts[e] = interior;
        }

        return edgeToSplitVerts;
    }
}
