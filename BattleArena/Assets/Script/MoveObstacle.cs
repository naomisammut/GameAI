using UnityEngine;

public class MoveObstacle : MonoBehaviour
{
    public Vector3 pointA;
    public Vector3 pointB;
    public float speed = 2f;

    void Start()
    {
        pointA = transform.position;
        pointB = transform.position + new Vector3(6f, 0f, 0f); // move 6 units sideways
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
        transform.position = Vector3.Lerp(pointA, pointB, t);
    }
}
