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
    //    yo/u are splitting)

    class Polygon
    {
        public List<Wall> walls;
        public (Polygon, Polygon) SplitPolygon(int a, int b)
        {
            if (a > b) return SplitPolygon(b, a);
            List<Wall> aWalls = walls.GetRange(0, a + 1);
            List<Wall> bWalls = walls.GetRange(a + 1, b - a);
            Vector3 splitPoint1 = aWalls[aWalls.Count - 1].end;
            Vector3 splitPoint2 = bWalls[bWalls.Count - 1].end;
            aWalls.Add(new Wall(splitPoint1, splitPoint2));
            bWalls.Add(new Wall(splitPoint2, splitPoint1));
            aWalls.AddRange(walls.GetRange(b + 1, walls.Count - b - 1));
            return (new Polygon(aWalls), new Polygon(bWalls)); // TODO
        }
        public Polygon(List<Wall> walls)
        {
            this.walls = walls;
        }
    }

    public Graph MakeNavMesh(List<Wall> outline) // We actually can't use a Polygon; Polygons are formed out of the outline lmao
    {
        List<Wall> reflexAngles = new List<Wall>();
        for (int i = 0; i < outline.Count; i++) // Finds all reflexive angles and logs them (todo: add them to a list)
        {
            Wall no1 = outline[i];
            Wall no2 = outline[(i + 1) % outline.Count];
            if (Vector3.Dot(no1.normal, no2.direction) < 0)
            {
                reflexAngles.Add(no1);
                Debug.Log($"Reflex angle found: Dot of {Vector3.Dot(no1.normal, no2.direction)}");
            }
        }

        // Nathan's section notes begins
        // find the non-convex corner <- done on Friday, see above
        // find the second split point
        // split the polygon
        // build the graph
        List<Polygon> polygons = new List<Polygon>();
        Polygon initPolygon = new Polygon(outline);
        polygons.Add(initPolygon);

        bool done = false;
        int iter = 0;

        while (!done)
        {
            if (iter >= polygons.Count) break;
            Polygon currentPolygon = polygons[iter];
            int nonConvexCornerIndex = -1;
            // find the non-convex corner
            nonConvexCornerIndex = findNonConvexCornerIndex(currentPolygon);
            if (nonConvexCornerIndex != -1)
            {
                // find the second split point
                int nextSplitPoint = findNextSplitPoint(currentPolygon, nonConvexCornerIndex);
                // split the polygon
                var (a, b) = currentPolygon.SplitPolygon(nonConvexCornerIndex, nextSplitPoint);
                polygons.RemoveAt(iter);
                polygons.Add(a);
                polygons.Add(b);
            }
            else
            {
                iter++;
            }

            if (iter == polygons.Count) done = true;
        }


        // build the graph
        List<GraphNode> nodes = new List<GraphNode>();
        int idGenerate = 0; // naive approach
        foreach (var p in polygons)
        {
            nodes.Add(new GraphNode(idGenerate, p.walls));
            idGenerate++;
        }
        buildNeighbors(nodes);
        // Graph g = new Graph();
        // g.outline = outline;
        // g.all_nodes = nodes;
        // Nathan's section notes ends

        

        // to do: implement function that links the node with a path line to the node to which would make the angle closest to 90 degrees

        Graph g = new Graph();
        g.all_nodes = new List<GraphNode>();
        return g;
    }

    // 
    static void buildNeighbors(List<GraphNode> nodes)
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

    static int findNonConvexCornerIndex(Polygon p)
    {
        List<Wall> walls = p.walls;
        for (int i = 0; i < walls.Count; i++)
        {
            Wall currentWall = walls[i];
            Wall nextWall = walls[(i + 1) % walls.Count];
            if (Vector3.Dot(currentWall.normal, nextWall.direction) < 0)
            {
                return i;
            }
        }
        return -1;
    }

    static int findNextSplitPoint(Polygon p, int splitPoint)
    {
        if (p.walls.Count >= 3)
        {
            // pseudocode idea: ("Idea" lmao, this is literally the function)
            int offset = p.walls.Count / 2;
            for (int i = 0; i < p.walls.Count; i++)
            {
                int currWallIndex = (i + offset + splitPoint) % p.walls.Count;
                if (Mathf.Abs(currWallIndex - splitPoint) < 2) continue;
                Vector3 newVector = p.walls[currWallIndex].end - p.walls[splitPoint].end;
                if (Vector3.Dot(p.walls[splitPoint].normal, newVector) < 0) continue;
                bool crossed = false;
                foreach (Wall wall in p.walls)
                {
                    crossed = (wall.Crosses(p.walls[currWallIndex].end, p.walls[splitPoint].end)) &&
                              (p.walls[currWallIndex].Crosses(p.walls[splitPoint])); // What exactly does this mean? The dementia might be setting in but I don't get it
                    if (crossed) break;

                }
                if (!crossed) return currWallIndex;
            }
        }
        return -1; // TODO (Why? Seems right that we return an invalid value and check for it)
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
            Debug.Log("got navmesh: " + navmesh.all_nodes.Count);
            EventBus.SetGraph(navmesh);
        }
    }





}
