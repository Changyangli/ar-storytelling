using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// New implementation using NavMesh
public class ShortestPath : MonoBehaviour
{
    public string sceneName;
    public GameObject cellPrefab;
    // public GameObject startObj;
    // public GameObject endObj;
    public Vector2 startPos;
    public Vector2 endPos;

    private NavMeshPath path;

    public ShortestPath(string sceneName){
        path = new NavMeshPath();
    }

    // public void Start(){
    //     path = new NavMeshPath();

    //     //test
    //     //FindShortestPath(FindNearestPoint(startPos), FindNearestPoint(endPos), true);
    // }

    public List<Vector2> FindShortestPath(Vector3 start, Vector3 end, bool vis = false){
        NavMesh.CalculatePath(FindNearestPoint(start), FindNearestPoint(end), NavMesh.AllAreas, path);
        
        if (vis){
            foreach (Vector3 p in path.corners){
                Instantiate(cellPrefab, new Vector3(p.x, p.y + 0.05f, p.z), Quaternion.Euler(0, 0, 0));
            }   
        }

        List<Vector2> shortestPath = new List<Vector2>();
        foreach (Vector3 p in path.corners){
            shortestPath.Add(new Vector2(p.x, p.z));
        }
        Debug.Log(shortestPath.Count);
        return shortestPath;
    }

    public List<Vector2> FindShortestPath(Vector2 start, Vector2 end, bool vis = false)
    {
        return FindShortestPath(new Vector3(start.x, 0.0f, start.y), new Vector3(end.x, 0.0f, end.y), vis);
    }

    public static Vector3 FindNearestPoint(Vector3 p){
        NavMeshHit hit;
        NavMesh.SamplePosition(p, out hit, 1.0f, NavMesh.AllAreas);
        
        return hit.position;
    }
}