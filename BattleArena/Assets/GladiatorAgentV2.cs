using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody))]
public class GladiatorAgentV2 : Agent
{
    [Header("Assign in the Scene (instances)")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform enemy;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float turnSpeed = 40f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        if (spawnPoint != null)
            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        // Unity 6 safe
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Extra safety (should never be null, but prevents crash spam)
        if (sensor == null) return;

        if (rb == null) rb = GetComponent<Rigidbody>();

        // Forward (2)
        Vector3 forward = transform.forward;
        sensor.AddObservation(forward.x);
        sensor.AddObservation(forward.z);

        // Velocity (2)
        Vector3 vel = (rb != null) ? rb.linearVelocity : Vector3.zero;
        sensor.AddObservation(vel.x);
        sensor.AddObservation(vel.z);

        // Enemy relative (3)
        if (enemy != null)
        {
            Vector3 toEnemy = enemy.position - transform.position;
            sensor.AddObservation(toEnemy.x);
            sensor.AddObservation(toEnemy.z);
            sensor.AddObservation(toEnemy.magnitude);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb == null) return;

        // 2 continuous actions: move, turn
        float move = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float turn = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        rb.AddForce(transform.forward * (move * moveSpeed), ForceMode.Acceleration);
        transform.Rotate(Vector3.up, turn * turnSpeed);

        // small penalty so it doesn't stall forever
        AddReward(-0.0002f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var c = actionsOut.ContinuousActions;
        c[0] = Input.GetKey(KeyCode.W) ? 1f : Input.GetKey(KeyCode.S) ? -1f : 0f;
        c[1] = Input.GetKey(KeyCode.D) ? 1f : Input.GetKey(KeyCode.A) ? -1f : 0f;
    }
}
