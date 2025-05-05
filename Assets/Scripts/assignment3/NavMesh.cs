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

    Polygon FindPolygonContainingWall(Wall targetWall, List<Polygon> polygons)
    {
        foreach (Polygon polygon in polygons)
        {
            foreach (Wall wall in polygon.walls)
            {
                if ((wall.start == targetWall.start && wall.end == targetWall.end) || (wall.start == targetWall.end && wall.end == targetWall.start))
                {
                    return polygon;
                }
            }
        }
        Debug.Log("null returned in FindPolygonContainingWall");
        return null;
    }

    public Graph MakeNavMesh(List<Wall> outline) // We actually can't use a Polygon; Polygons are formed out of the outline lmao
    {
        List<Wall> reflexAngles = new List<Wall>();
        
        for (int i = 0; i < outline.Count; i++) // Finds all reflexive angles and logs them
        {
            Wall no1 = outline[i];
            Wall no2 = outline[(i + 1) % outline.Count];
            if (Vector3.Dot(no1.normal, no2.direction) < 0)
            {
                reflexAngles.Add(no1);
            }
        }

        // find the non-convex corner <- done on Friday, see above
        // find the second split point
        // split the polygon
        // build the graph
        List<Polygon> polygons = new List<Polygon>();
        //Polygon initPolygon = new Polygon(outline);
        Polygon currPolygon = new Polygon(outline);

        foreach (Wall rAngle in reflexAngles)
        {
            // iterate through every wall in a polygon using the wall.same method to find which polygon an rAngle belongs to
            int index = outline.IndexOf(rAngle);
            if (index > -1)
            {
                Polygon findPoly = FindPolygonContainingWall(rAngle, polygons);
                if (findPoly != null) currPolygon = findPoly;
                index = currPolygon.walls.IndexOf(rAngle);
                if (index > -1) {
                    int nextSplitPoint = FindNextSplitPoint(currPolygon, index);
                    if (nextSplitPoint != -1)
                    {
                        var (a, b) = currPolygon.SplitPolygon(index, nextSplitPoint, currPolygon.walls);
                        polygons.Remove(currPolygon);
                        polygons.Add(a);
                        polygons.Add(b);
                    }
                }
            }
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
        Finds the first non-convex corner inside of a polygon p, and returns the index that corner is found at.
        Polygon p: the polygon containing a non-convex corner

        How it should work:
        1. Iterate through every wall in the polygon
        2. For each wall, check if the angle between the current wall and the next wall is greater than 180 degrees.
            if it is, then it is a non-convex corner
            Basically, if you are going counter-clockwise around the graph and at any point you have to turn
            left even by a tiny amount, that is a non-convex corner.
        3. Return the first found non-convex corner
        4. If no non-convex corners are found, return -1
    */
    static int FindNonConvexCornerIndex(Polygon p)
    {
        List<Wall> walls = p.walls;
        for (int i = 0; i < walls.Count; i++)
        {
            Wall currentWall = walls[i];
            Wall nextWall = walls[(i + 1) % walls.Count];
            if (Vector3.Dot(currentWall.normal, nextWall.direction) < 0)
            {
                Debug.Log($"Index {i} has dot product of {Vector3.Dot(currentWall.normal, nextWall.direction)}");
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.position = nextWall.start;
                sphere.transform.localScale *= 5;
                return i;
            }
        }
        //Debug.Log("findNonConvexCornerIndex returned -1");
        return -1;
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

                Debug.Log($"currIndex = {currIndex}");
                Debug.Log($"splitPoint = {splitPoint}");
                Vector3 newVector = p.walls[currIndex].end - p.walls[splitPoint].end;
                if (Vector3.Dot(p.walls[splitPoint].normal, newVector) < 0) continue;

                bool crossed = false;
                foreach (Wall wall in p.walls)
                {
                    if (wall.Same(p.walls[currIndex]) || wall.Same(p.walls[splitPoint])) continue;

                    int wallIndex = p.walls.IndexOf(wall);
                    if (wallIndex == (currIndex + 1) % p.walls.Count || wallIndex == (splitPoint + 1) % p.walls.Count) continue;

                    crossed = wall.Crosses(p.walls[currIndex].end, p.walls[splitPoint].end);
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