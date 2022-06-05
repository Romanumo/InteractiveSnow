#define DebugVisualMode

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SideDirection { Left, Right, None, Middle };
public class SnowInteractive : MonoBehaviour
{
    [SerializeField, Range(1, 255)] private int vertexSubdivision = 100;
    [SerializeField, Range(0.1f, 100)] private float vertexSize = 1;
    [SerializeField, Range(0.1f, 100)] private float maxSnowDrop = 5;
    [SerializeField] private float snowDropSpeed = 0.3f;

    GameObject meshObj;
    MeshFilter meshFilter;

    Mesh mesh;
    Vector3[] vertices;
    int[] triangles;
    int triangleIndex = 0;

    //Code of creating simple plane mesh
    #region MeshCreating
    void Start()
    {
        CreatePlane();
    }

    void CreatePlane()
    {
        mesh = CreateMesh();
        meshObj = new GameObject();
        meshObj.transform.position = this.transform.position;
        meshObj.AddComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
        meshFilter = meshObj.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        MeshCollider collider = meshObj.AddComponent<MeshCollider>();
        collider.convex = true;
        collider.isTrigger = true;
        meshObj.AddComponent<SnowMesh>().snowManager = this;
    }

    Mesh CreateMesh()
    {
        vertices = new Vector3[vertexSubdivision * vertexSubdivision];
        triangles = new int[(vertexSubdivision - 1) * (vertexSubdivision - 1) * 6];
        triangleIndex = 0;

        for (int y = 0; y < vertexSubdivision; y++)
        {
            for (int x = 0; x < vertexSubdivision; x++)
            {
                int vertexIndex = y * vertexSubdivision + x;
                vertices[vertexIndex] = new Vector3(x, 0, y) * vertexSize;

                if (x < vertexSubdivision - 1 && y < vertexSubdivision - 1)
                {
                    WriteTriangle(vertexIndex + vertexSubdivision + 1, vertexIndex + 1, vertexIndex);
                    WriteTriangle(vertexIndex + vertexSubdivision, vertexIndex + vertexSubdivision + 1, vertexIndex);
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    void WriteTriangle(int a, int b, int c)
    {
        triangles[triangleIndex] = a;
        triangles[triangleIndex + 1] = b;
        triangles[triangleIndex + 2] = c;
        triangleIndex += 3;
    }
    #endregion

    public void DeformSnow(GameObject obj)
    {
        ShapeInfo shape = new ShapeInfo(GetSnowPrintVertices(obj));
        if(!CheckShapeInfo(shape))
            return;
        shape = SnapShapeBoundry(shape);
        LeaveTrail(shape, snowDropSpeed);
    }

    //Change from local transform to world one and return mesh vertices that directly under snow level
    public List<Vector3> GetSnowPrintVertices(GameObject obj)
    {
        Mesh objMesh = obj.GetComponent<MeshFilter>().mesh;
        List<Vector3> convexPoints = new List<Vector3>();

        foreach (Vector3 vertex in objMesh.vertices)
        {
            Vector3 worldVertex = obj.transform.TransformPoint(vertex);
            if (worldVertex.y < meshObj.transform.position.y && !convexPoints.Contains(worldVertex))
            {
                convexPoints.Add(worldVertex);
            }
        }

        List<Vector3> pressedShapePoints = AdaptateConvexVertices(convexPoints);
        return pressedShapePoints;
    }

    void LeaveTrail(ShapeInfo shape, float amount)
    {
        if (shape.shapeVertex.Count < 0)
            return;

        List<int> convexIndexes = GetInnerShapeVertexes(shape);
        float objOverSnowDist = GetOverSnowDist(shape.shapeVertex);
        foreach (int index in convexIndexes)
        {
            if (vertices[index].y > objOverSnowDist && vertices[index].y > meshObj.transform.position.y - maxSnowDrop)
                vertices[index].y -= amount;
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
    }

    float GetOverSnowDist(List<Vector3> positions)
    {
        float y = 0;
        foreach (Vector3 pos in positions)
        {
            y += pos.y;
        }
        y /= positions.Count;
        return y;
    }

    List<Vector3> innerShapeDebugPoints;
    ShapeInfo shapeDebug;

    List<int> GetInnerShapeVertexes(ShapeInfo shape)
    {
        shapeDebug = shape;
        innerShapeDebugPoints = new List<Vector3>(shape.shapeVertex);

        List<int> convexIndexes = new List<int>();
        int convexPointsN = shape.shapeVertex.Count;

        for (int y = shape.minCorner.y; y < shape.maxCorner.y; y++) 
        {
            for (int x = shape.minCorner.x; x < shape.maxCorner.x; x++) 
            {
                int index = y * vertexSubdivision + x;
                Vector3 point = vertices[index] * vertexSize;
                if (IsPointInsideConvex(convexPointsN, shape.shapeVertex, Transform2D(point)))
                {
                    innerShapeDebugPoints.Add(point);
                    convexIndexes.Add(index);
                }
            }
        }

        return convexIndexes;
    }

    bool IsPointInsideConvex(int convexPointsN, List<Vector3> convexPoints, Vector2 point)
    {
        if (convexPointsN <= 2)
            return false;

        SideDirection previousDir = SideDirection.None;
        for (int i = 0; i < convexPointsN; i++)
        {
            SideDirection currentDir;
            Vector2 pointA = Transform2D(convexPoints[i] * vertexSize);
            Vector2 pointB = (i + 1 == convexPointsN) ? Transform2D(convexPoints[0]) : Transform2D(convexPoints[i + 1]);
            pointB *= vertexSize;

            Vector2 side = pointB - pointA;
            Vector2 toPoint = point - pointA;

            currentDir = GetSideDirection(side, toPoint);

            if (previousDir == SideDirection.None)
                previousDir = currentDir;
            else if (previousDir != currentDir)
                return false;
            else if (currentDir == SideDirection.None)
                return false;
        }
        return true;
    }

    SideDirection GetSideDirection(Vector2 side, Vector2 toPoint)
    {
        //To get direction we use dotProduct or cosine between side and a to point, if cos is negative then it is on the left side, if it is plus then on the right
        float x = Vector2.Dot(side, toPoint);
        if (x < 0)
            return SideDirection.Left;
        else if (x > 0)
            return SideDirection.Right;
        else if (x == 0)
            return SideDirection.Middle;
        return SideDirection.None;
    }

    //Return array in a rearranged convex list
    List<Vector3> AdaptateConvexVertices(List<Vector3> convexVertices)
    {
        if (convexVertices.Count < 1)
            return null;

        List<Vector3> result = new List<Vector3>();
        Vector3 head = convexVertices[0];
        for (int i = 0; i < convexVertices.Count; i++)
        {
            float nearestMagnitude = float.PositiveInfinity;
            Vector3 nextVertex = new Vector3(0, -90, 0);
            foreach (Vector3 vertex in convexVertices)
            {
                if (vertex == head || result.Contains(vertex))
                    continue;

                float magnitude = (head - vertex).sqrMagnitude;
                if (magnitude < nearestMagnitude)
                {
                    nearestMagnitude = magnitude;
                    nextVertex = vertex;
                }
            }

            if (nextVertex == new Vector3(0, -90, 0))
                Debug.LogError("No nearest vertex found");

            result.Add(nextVertex);
            head = nextVertex;
        }
        return result;
    }

    //Set shapes boundry
    ShapeInfo SnapShapeBoundry(ShapeInfo shape)
    {
        ShapeInfo shapeResult = shape;
        shapeResult.maxCorner = SnapToGrid(shapeResult.maxCorner);
        shapeResult.minCorner = SnapToGrid(shapeResult.minCorner);
        return shapeResult;
    }

    Vector2Int SnapToGrid(Vector2 coordinate)
    {
        Vector2Int vector = new Vector2Int();
        vector.x = Mathf.CeilToInt(coordinate.x / vertexSize);
        vector.y = Mathf.CeilToInt(coordinate.y / vertexSize);
        return vector;
    }

    public static Vector2 Transform2D(Vector3 a)
    {
        return new Vector2(a.x, a.z);
    }

    bool CheckShapeInfo(ShapeInfo shape)
    {
        if (shape.shapeVertex == null)
        {
            Debug.Log("There are no shape vertex that touch snow");
            return false;
        }

        if (shape.shapeVertex.Count == 0)
        {
            Debug.Log("There are no vertex that touches snow");
            return false;
        }

        return true;
    }

#if DebugVisualMode
    private void OnDrawGizmos()
    {
        if (innerShapeDebugPoints != null && innerShapeDebugPoints.Count > 0)
        {
            //Debug.Log(innerShapeDebugPoints.Count);

            foreach (Vector3 point in innerShapeDebugPoints)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawCube(point / vertexSize, Vector3.one * 0.3f);
            }
        }

        if (shapeDebug != null && shapeDebug.shapeVertex.Count > 0)
        {
            //Debug.Log(shapeDebugVertex.Count);
            for (int i = 0; i < shapeDebug.shapeVertex.Count; i++)
            {
                Vector3 pointA = shapeDebug.shapeVertex[i];
                Vector3 pointB;
                if (i + 1 >= shapeDebug.shapeVertex.Count)
                    pointB = shapeDebug.shapeVertex[0];
                else
                    pointB = shapeDebug.shapeVertex[i + 1];
                Vector3 side = (pointB - pointA).normalized;

                Vector3 rightSide = Vector3.Cross(side, Vector3.up);
                Gizmos.color = Color.Lerp(Color.red, Color.blue, Mathf.InverseLerp(shapeDebug.shapeVertex.Count, 0, i));
                Gizmos.DrawCube(pointA, Vector3.one * 0.7f);
                Gizmos.color = Color.black;
                Gizmos.DrawCube(rightSide + pointA, Vector3.one * 0.3f);
                Gizmos.DrawLine(rightSide + pointA, pointA);
                Gizmos.DrawLine(pointA, pointB);
            }

            Gizmos.color = Color.green;
            Gizmos.DrawCube(new Vector3(shapeDebug.maxCorner.x, 0, shapeDebug.maxCorner.y) * vertexSize, Vector3.one * 1.7f);
            Gizmos.color = Color.blue;
            Gizmos.DrawCube(new Vector3(shapeDebug.minCorner.x, 0, shapeDebug.minCorner.y) * vertexSize, Vector3.one * 1.7f);
        }
    }
    #endif

    public class ShapeInfo
    {
        public Vector2Int minCorner;
        public Vector2Int maxCorner;
        public List<Vector3> shapeVertex;

        public ShapeInfo(List<Vector3> shapeVertex)
        {
            this.shapeVertex = shapeVertex;
            minCorner = Vector2Int.one * 999999;
            maxCorner = Vector2Int.one * -999999;
            CheckForBoundry();
        }

        public void CheckForBoundry()
        {
            foreach(Vector3 vertex in shapeVertex)
            {
                Vector2 vertex2D = SnowInteractive.Transform2D(vertex);
                vertex2D.x = Mathf.RoundToInt(vertex2D.x);
                vertex2D.y = Mathf.RoundToInt(vertex2D.y);

                if (vertex2D.x < minCorner.x)
                    minCorner.x = (int)vertex2D.x;
                if (vertex2D.x > maxCorner.x)
                    maxCorner.x = (int)vertex2D.x;

                if (vertex2D.y < minCorner.y)
                    minCorner.y = (int)vertex2D.y;
                if (vertex2D.y > maxCorner.y)
                    maxCorner.y = (int)vertex2D.y;
            }
        }
    }
}
