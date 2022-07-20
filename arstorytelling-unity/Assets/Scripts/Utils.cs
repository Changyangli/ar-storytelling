using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using System;
using UnityEngine;

[Serializable]
public struct SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public SerializableVector3(float rX, float rY, float rZ)
    {
        x = rX;
        y = rY;
        z = rZ;
    }
    
    /// Returns a string representation of the object
    public override string ToString()
    {
        return String.Format("[{0}, {1}, {2}]", x, y, z);
    }

    public static SerializableVector3 operator -(SerializableVector3 a, SerializableVector3 b)
        =>new SerializableVector3(a.x - b.x, a.y - b.y, a.z - b.z);
    
    /// Automatic conversion from SerializableVector3 to Vector3
    public static implicit operator Vector3(SerializableVector3 rValue)
    {
        return new Vector3(rValue.x, rValue.y, rValue.z);
    }
    
    /// Automatic conversion from Vector3 to SerializableVector3
    public static implicit operator SerializableVector3(Vector3 rValue)
    {
        return new SerializableVector3(rValue.x, rValue.y, rValue.z);
    }
}

public class ForwardDirectionMono : MonoBehaviour{
    public static Vector2 GetForward2D(float eulerRot){
        GameObject go = new GameObject();
        go.transform.eulerAngles = new Vector3(0.0f, eulerRot, 0.0f);
        Vector2 forward = new Vector2(go.transform.forward.x, go.transform.forward.z);
        
        Destroy(go);
        return forward;
    }

    public static Vector3 GetForward3D(float eulerRot){
        GameObject go = new GameObject();
        go.transform.eulerAngles = new Vector3(0.0f, eulerRot, 0.0f);
        Vector3 forward = go.transform.forward;
        
        Destroy(go);
        return forward;
    }
}

/* https://stackoverflow.com/questions/36239705/serialize-and-deserialize-json-and-json-array-in-unity */
public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
        return wrapper.Instances;
    }

    [Serializable]
    private class Wrapper<T>
    {
        public T[] Instances;
    }
}

/* https://www.delftstack.com/howto/csharp/shuffle-a-list-in-csharp/ */
static class ExtensionsClass
{
    private static System.Random rng = new System.Random();

    public static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}

/* https://stackoverflow.com/questions/57453943/how-to-sort-all-polygon-points-by-clockwise-anticlockwise-direction */
class CheckFOV
{
    class Point {
        public float x;
        public float y;
        public float angle;

        //default consructor sets point to 0,0
        public Point() {
            x = 0.0f; 
            y=0.0f; 
            angle = 0.0f; 
        }

        //manually assign point
        public Point(float xin, float yin) { 
            x = xin; 
            y = yin; 
            angle = 0.0f; 
        }

        public void AddPoint(Point p){
            x += p.x;
            y += p.y;
        }

        //get angle between this point and another
        public float GetAngle(Point p) {
            //check to make sure the angle won't be "0"
            if(Mathf.Abs(p.y - y) < 0.000001f) { 
                if (p.x > 0) return 0.0f;
                else return Mathf.PI;
            }

            float tmpAngle = Mathf.Atan((p.y - y) / (p.x - x));
            if (p.y - y > 0.0f && p.x - x < 0.0f) tmpAngle += Mathf.PI;
            if (p.y - y < 0.0f && p.x - x < 0.0f) tmpAngle += Mathf.PI;
            if (p.y - y < 0.0f && p.x - x > 0.0f) tmpAngle += Mathf.PI * 2;

            return tmpAngle;
        }
    };

    public static bool CheckVecInsideFOV(Vector3 vec1, Vector3 vec2, Vector3 source2target){
        if (Vector3.Dot(source2target, vec1) < 0 || Vector3.Dot(source2target, vec2) < 0) return false;

        Vector3 cross1 = Vector3.Cross(source2target, vec1);
        Vector3 cross2 = Vector3.Cross(source2target, vec2);

        if (cross1.y * cross2.y <= 0.0f) return true;
        else return false;
    }

    public static bool CheckVecFacingPolygonInside(List<SpatialGraphNode> characters, SpatialGraphNode target, bool debug = false){
        bool flag = false;

        List<Point> points = new List<Point>();
        Point center = new Point();

        for (int i = 0; i < characters.Count; i++){
            Point newPoint = new Point(characters[i].pos.x, characters[i].pos.z);
            center.AddPoint(newPoint);
            points.Add(newPoint);
        }

        center.x = center.x / points.Count;
        center.y = center.y / points.Count;
        //Point center = points[1];

        foreach (Point p in points){
            p.angle = center.GetAngle(p);
        }
        List<Point> polygon = points.OrderBy(o => o.angle).ToList();

        //if (debug) Debug.Log("\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\");

        Vector3 targetPos = new Vector3(target.pos.x, 0.0f, target.pos.z);
        Vector3 targetForward = ForwardDirectionMono.GetForward3D(target.eulerRot + target.headRotOffset);
        for (int i = 0; i < polygon.Count; i++){
            Vector3 vec3P = new Vector3(polygon[i].x, 0.0f, polygon[i].y);
            int last = (i - 1 + polygon.Count) % polygon.Count;
            int next = (i + 1) % polygon.Count;

            Vector3 cur2last = new Vector3(polygon[last].x - polygon[i].x, 0.0f, polygon[last].y - polygon[i].y);
            Vector3 cur2next = new Vector3(polygon[next].x - polygon[i].x, 0.0f, polygon[next].y - polygon[i].y);

            if (debug) {
                //Debug.Log(polygon[i].x + ", " + polygon[i].y + ", " + polygon[i].angle);
                Debug.DrawRay(vec3P + new Vector3(0.0f, 2.0f, 0.0f), cur2last, Color.red);
            }

            if (Vector3.Distance(vec3P, targetPos) < 0.01f){
                flag = CheckVecInsideFOV(cur2last, cur2next, targetForward);
                //break;
            }
        }

        //if (debug) Debug.Log("\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\");

        return flag;
    }
}

public class Utils : MonoBehaviour{}
