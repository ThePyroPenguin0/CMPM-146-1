using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class NavMesh : MonoBehaviour
{
    // implement NavMesh generation here:
    //    the outline are Walls in counterclockwise order
    //    iterate over them, and if you find a reflex angle
    //    you have to split the polygon into two
    //    then perform the same operation on both parts
    //    until no more reflex angles are present
    //
    //    when you have a number of polygons, you will have
    //    to convert them into a graph: each polygon is a node
    //    you can find neighbors by finding shared edges between
    //    different polygons (or you can keep track of this while 
    //    you are splitting)

    class Polygon
    {
        public List<Wall> walls;
        public (Polygon, Polygon) SplitPolygon(int a, int b, List<Wall> outline)
        {
            if (a > b)
            {
                return SplitPolygon(b, a, outline);
            }

            List<Wall> aWalls = walls.GetRange(0, a + 1);
            List<Wall> bWalls = walls.GetRange(a + 1, b - a);

            Vector3 splitPoint1 = aWalls[aWalls.Count - 1].end;
            Vector3 splitPoint2 = bWalls[bWalls.Count - 1].end;

            Wall splittingWall = new Wall(splitPoint1, splitPoint2);

            aWalls.Add(new Wall(splitPoint1, splitPoint2));
            bWalls.Add(new Wall(splitPoint2, splitPoint1));
            if (b + 1 < walls.Count) // Check added for wraparound
            {
                aWalls.AddRange(walls.GetRange(b + 1, walls.Count - b - 1));
            }
            return (new Polygon(aWalls), new Polygon(bWalls));
        }
        public Polygon(List<Wall> walls)
        {
            this.walls = walls;
        }
    }


    public Graph MakeNavMesh(List<Wall> outline) // We actually can't use a Polygon; Polygons are formed out of the outline lmao
    {
        List<Polygon> polygons = new List<Polygon>();
        polygons.Add(new Polygon(outline));

        bool containsReflexAngle = true;

        while(containsReflexAngle)
        {
            containsReflexAngle = false;
            List<Polygon> generatedPolygons = new List<Polygon>();
            foreach (Polygon currentPolygon in polygons)
            {
                List<int> reflexAngleIndices = new List<int>();

                // Decided to localize the reflex angle check to the polygon, and to iterate over them instead of the outline and trying to iterate over them from within it
                for (int i = 0; i < currentPolygon.walls.Count; i++)
                {
                    Wall no1 = currentPolygon.walls[i];
                    Wall no2 = currentPolygon.walls[(i + 1) % currentPolygon.walls.Count];
                    if (Vector3.Dot(no1.normal, no2.direction) < 0)
                    {
                        reflexAngleIndices.Add(i);
                    }
                }

                if(reflexAngleIndices.Count > 0)
                {
                    containsReflexAngle = true;
                    int splitPoint = reflexAngleIndices[0];
                    int nextSplitPoint = FindNextSplitPoint(currentPolygon, splitPoint);

                    if (nextSplitPoint != -1)
                    {
                        var (a, b) = currentPolygon.SplitPolygon(splitPoint, nextSplitPoint, currentPolygon.walls);
                        generatedPolygons.Add(a);
                        generatedPolygons.Add(b);
                    }
                    else
                    {
                        generatedPolygons.Add(currentPolygon); // Now, if there is no valid split point, it's actually okay
                    }
                }
                else
                {
                    generatedPolygons.Add(currentPolygon); // see above
                }
            }
            polygons = generatedPolygons;
        }
        Debug.Log($"Finished splitting polygons. Total polygons: {polygons.Count}");
        
        // build the graph
        List<GraphNode> nodes = new List<GraphNode>();
        int idGenerate = 0; // naive approach
        foreach (var p in polygons)
        {
            nodes.Add(new GraphNode(idGenerate, p.walls));
            idGenerate++;
        }
        BuildNeighbors(nodes);

        Graph g = new Graph();
        g.all_nodes = nodes;
        return g;
    }

    // 
    static void BuildNeighbors(List<GraphNode> nodes)
    {
        foreach (GraphNode a in nodes)
        {
            foreach (GraphNode b in nodes)
            {
                if (a.GetID() == b.GetID()) continue;
                List<Wall> aWalls = a.GetPolygon();
                List<Wall> bWalls = b.GetPolygon();
                for (int i = 0; i < aWalls.Count; i++)
                {
                    for (int j = 0; j < bWalls.Count; j++)
                    {
                        if (aWalls[i].Same(bWalls[j]))
                        {
                            a.AddNeighbor(b, i);
                            b.AddNeighbor(a, j);
                        }
                    }
                }
            }
        }
    }

    /*
        Finds a valid split point within the polygon, to be used in order to split the polygon
        Polygon p: the polygon containing a non-convex corner
        int splitPoint: the point within the polygon where the non-convex corner is found

        How it should work:
        1. Check for validity of polygon. This is simply done by making sure it has at least 3 walls i.e. at least a triangle.
        2. Check every index within p and returns the first valid index.
            The index is valid if:
                  i.    It is NOT the splitPoint
                 ii.    It is NOT the points right next to splitPoint
                iii.    The wall that could be build between those two points does NOT intersect any other walls
                            Note: we only need to check that it doesn't intersect the lines of p
                 iv.    The angle created by the new wall is ideally NOT convex
        
        If the program does not find a valid split point (which it should NEVER do), it will return -1
        Otherwise, returns an int which represents the index of the valid split point
    */
    static int FindNextSplitPoint(Polygon p, int splitPoint)
    {
        if (p.walls.Count >= 3)
        {
            int offset = p.walls.Count / 2;
            for (int i = 0; i < p.walls.Count; i++) {
                int currIndex = (i + splitPoint + offset) % p.walls.Count; 
                
                if (Mathf.Abs(currIndex - splitPoint) < 2) continue; // i is either splitPoint or right next to the split point

                Vector3 newVector = p.walls[currIndex].end - p.walls[splitPoint].end;
                if (Vector3.Dot(p.walls[splitPoint].normal, newVector) < 0) continue;

                bool crossed = false;
                foreach (Wall wall in p.walls)
                {
                    if (wall.Same(p.walls[currIndex]) || wall.Same(p.walls[splitPoint])) continue;

                    int wallIndex = p.walls.IndexOf(wall);
                    if (wallIndex == (currIndex + 1) % p.walls.Count || wallIndex == (splitPoint + 1) % p.walls.Count) continue;

                    crossed = wall.Crosses(p.walls[currIndex].end, p.walls[splitPoint].end) || p.walls[currIndex].Crosses(p.walls[splitPoint]);
                    if (crossed) break;
                }
                if (!crossed) return currIndex;
            }

        }

        Debug.Log("findNextSplitPoint returned -1 after exhausting all attempts");
        return -1; // Return -1 if no valid split point is found after all attempts
    }

    List<Wall> outline;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EventBus.OnSetMap += SetMap;
    }

    // Update is called once per frame
    void Update()
    {


    }

    public void SetMap(List<Wall> outline)
    {
        Graph navmesh = MakeNavMesh(outline);
        if (navmesh != null)
        {
            // Debug.Log("got navmesh: " + navmesh.all_nodes.Count);
            EventBus.SetGraph(navmesh);
        }
    }
}