using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Linq;

public class AStarEntry
{
    public GraphNode curr;
    public AStarEntry parent;

    public float g; // distance from the start node to the current node
    public float h; // heuristic value (distance from the current node to the target node)

    public AStarEntry(GraphNode curr, AStarEntry parent, float g, float h)
    {
        this.curr = curr;
        this.parent = parent;
        this.g = g;
        this.h = h;
    }
}


// PriorityQueue provided by GitHub Copilot after finding out that Unity for some reason doesn't have a built-in priority queue
public class PriorityQueue<TElement, TPriority> : IEnumerable<(TElement Element, TPriority Priority)>
    where TPriority : IComparable<TPriority>
{
    private SortedDictionary<TPriority, Queue<TElement>> _dictionary = new SortedDictionary<TPriority, Queue<TElement>>();

    public int Count { get; private set; }

    public void Enqueue(TElement element, TPriority priority)
    {
        if (!_dictionary.TryGetValue(priority, out var queue))
        {
            queue = new Queue<TElement>();
            _dictionary[priority] = queue;
        }
        queue.Enqueue(element);
        Count++;
    }

    public TElement Dequeue()
    {
        if (Count == 0)
            throw new InvalidOperationException("The priority queue is empty.");

        var firstPair = _dictionary.First();
        var element = firstPair.Value.Dequeue();
        if (firstPair.Value.Count == 0)
        {
            _dictionary.Remove(firstPair.Key);
        }
        Count--;
        return element;
    }

    public IEnumerator<(TElement Element, TPriority Priority)> GetEnumerator()
    {
        foreach (var pair in _dictionary)
        {
            foreach (var element in pair.Value)
            {
                yield return (element, pair.Key);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

public class PathFinder : MonoBehaviour
{
    // Assignment 2: Implement AStar
    //
    // DO NOT CHANGE THIS SIGNATURE (parameter types + return type)
    // AStar will be given the start node, destination node and the target position, and should return 
    // a path as a list of positions the agent has to traverse to reach its destination, as well as the
    // number of nodes that were expanded to find this path
    // The last entry of the path will be the target position, and you can also use it to calculate the heuristic
    // value of nodes you add to your search frontier; the number of expanded nodes tells us if your search was
    // efficient
    //
    // Take a look at StandaloneTests.cs for some test cases
    public static (List<Vector3>, int) AStar(GraphNode start, GraphNode destination, Vector3 target)
    {
        List<Vector3> path = new List<Vector3>() { target};
        int expanded = 0;

        if (start.GetID() == destination.GetID())
        {
            return (new List<Vector3> { target }, 0);
        }

        PriorityQueue<AStarEntry, float> open = new PriorityQueue<AStarEntry, float>();
        HashSet<GraphNode> closed = new HashSet<GraphNode>();

        open.Enqueue(new AStarEntry(start, null, 0, Vector3.Distance(target, start.GetCenter())), 0);

        while (open.Count > 0)
        {
            AStarEntry q = open.Dequeue();
            closed.Add(q.curr);

            if (q.curr.GetID() == destination.GetID())
            {
                return (ReconstructPath(q, target), expanded);
            }

            foreach (GraphNeighbor neighbor in q.curr.GetNeighbors())
            {
                GraphNode successor = neighbor.GetNode();
                if (closed.Contains(successor)) continue;

                float g2 = q.g + 1; // Assuming uniform cost for edges
                float h2 = Vector3.Distance(target, successor.GetCenter());
                float f2 = g2 + h2;

                bool inOpen = false;
                foreach (var entry in open)
                {
                    if (entry.Element.curr.GetID() == successor.GetID())
                    {
                        inOpen = true;
                        if (g2 < entry.Element.g)
                        {
                            entry.Element.g = g2;
                            entry.Element.h = h2;
                            entry.Element.parent = q;
                        }
                        break;
                    }
                }

                if (!inOpen)
                {
                    open.Enqueue(new AStarEntry(successor, q, g2, h2), f2);
                }
            }

            expanded++;
        }

        return (path, expanded);
    }

    // ReconstructPath given by GitHub Copilot with some slight edits by Nathan
    private static List<Vector3> ReconstructPath(AStarEntry entry, Vector3 target)
    {
        List<Vector3> path = new List<Vector3>();
        while (entry != null)
        {
            path.Add(entry.curr.GetCenter());
            entry = entry.parent;
        }
        path.Reverse();
        path.Add(target); // Add the target position at the end of the path
        return path;
    }

    public Graph graph;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EventBus.OnTarget += PathFind;
        EventBus.OnSetGraph += SetGraph;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetGraph(Graph g)
    {
        graph = g;
    }

    // entry point
    public void PathFind(Vector3 target)
    {
        if (graph == null) return;

        // find start and destination nodes in graph
        GraphNode start = null;
        GraphNode destination = null;
        foreach (var n in graph.all_nodes)
        {
            if (Util.PointInPolygon(transform.position, n.GetPolygon()))
            {
                start = n;
            }
            if (Util.PointInPolygon(target, n.GetPolygon()))
            {
                destination = n;
            }
        }
        if (destination != null)
        {
            // only find path if destination is inside graph
            EventBus.ShowTarget(target);
            (List<Vector3> path, int expanded) = PathFinder.AStar(start, destination, target);

            Debug.Log("found path of length " + path.Count + " expanded " + expanded + " nodes, out of: " + graph.all_nodes.Count);
            EventBus.SetPath(path);
        }
        

    }
}
