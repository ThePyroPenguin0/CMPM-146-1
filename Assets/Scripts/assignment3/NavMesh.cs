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
    public Graph MakeNavMesh(List<Wall> outline)
    {
        for (int i = 0; i < outline.Count; i++)
        {
            Wall no1 = outline[i];
            Wall no2 = outline[(i + 1) % outline.Count];
            if(Vector3.Dot(no1.normal, no2.direction) < 0)
            {
                Debug.Log("found reflex angle");
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.position = no1.end;
                sphere.transform.localScale = Vector3.one * 5;
            }
        }

        // to do: implement function that links the node with a path line to the node to which would make the angle closest to 90 degrees

        Graph g = new Graph();
        g.all_nodes = new List<GraphNode>();
        return g;
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
