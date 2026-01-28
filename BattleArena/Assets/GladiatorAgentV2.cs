using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BehaviorParameters))]
public class GladiatorAgentV2 : Agent
{
    [Header("Optional manual links (scene instances)")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform enemy;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 25f;
    [SerializeField] private float turnSpeed = 220f;

    [Header("Episode")]
    [SerializeField] private int maxEpisodeSteps = 2000;

    private Rigidbody rb;
    private BehaviorParameters bp;
    private GladiatorWeapon weapon;
    private GladiatorHealth myHealth;

    private float prevDist;

    public override void Initialize()
    {
        CacheComponents();
        MaxStep = maxEpisodeSteps;

        AutoAssignSpawn();
        AutoFindEnemy();
    }

    private void CacheComponents()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (bp == null) bp = GetComponent<BehaviorParameters>();
        if (weapon == null) weapon = GetComponentInChildren<GladiatorWeapon>();
        if (myHealth == null) myHealth = GetComponent<GladiatorHealth>();
    }

    private void AutoAssignSpawn()
    {
        if (spawnPoint != null || bp == null) return;
        string spawnName = (bp.TeamId == 0) ? "Spawn_A" : "Spawn_B";
        var sp = GameObject.Find(spawnName);
        if (sp != null) spawnPoint = sp.transform;
    }

    private void AutoFindEnemy()
    {
        if (enemy != null) return;

        var all = FindObjectsOfType<GladiatorAgentV2>();
        foreach (var a in all)
        {
            if (a != this)
            {
                enemy = a.transform;
                break;
            }
        }
    }

    public override void OnEpisodeBegin()
    {
        CacheComponents();
        AutoAssignSpawn();
        AutoFindEnemy();

        if (spawnPoint != null)
            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (myHealth != null) myHealth.ResetHP();
        if (weapon != null) weapon.ResetWeapon();

        prevDist = (enemy != null) ? Vector3.Distance(transform.position, enemy.position) : 0f;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        CacheComponents();
        AutoFindEnemy();

        if (sensor == null || rb == null)
        {
            sensor?.AddObservation(0f); sensor?.AddObservation(0f);
            sensor?.AddObservation(0f); sensor?.AddObservation(0f);
            sensor?.AddObservation(0f); sensor?.AddObservation(0f); sensor?.AddObservation(0f);
            sensor?.AddObservation(1f);
            return;
        }

        Vector3 f = transform.forward;
        sensor.AddObservation(f.x);
        sensor.AddObservation(f.z);

        Vector3 vLocal = transform.InverseTransformDirection(rb.linearVelocity);
        sensor.AddObservation(vLocal.x);
        sensor.AddObservation(vLocal.z);

        if (enemy != null)
        {
            Vector3 toEnemy = enemy.position - transform.position;
            Vector3 dirLocal = transform.InverseTransformDirection(toEnemy.normalized);
            sensor.AddObservation(dirLocal.x);
            sensor.AddObservation(dirLocal.z);
            sensor.AddObservation(toEnemy.magnitude);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        sensor.AddObservation(myHealth != null ? myHealth.HPNormalized : 1f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        CacheComponents();
        AutoFindEnemy();
        if (rb == null) return;

        float move = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float turn = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        rb.AddForce(transform.forward * (move * moveSpeed), ForceMode.Acceleration);
        transform.Rotate(Vector3.up, turn * turnSpeed * Time.fixedDeltaTime);

        // Attack (Discrete 0/1)
        if (actions.DiscreteActions.Length > 0 && weapon != null)
            weapon.TryAttack(actions.DiscreteActions[0] == 1);

        // Reward shaping to stop spinning-only
        AddReward(-0.0002f);                    // time penalty
        AddReward(0.00015f * Mathf.Abs(move));  // encourage moving input
        AddReward(-0.00025f * Mathf.Abs(turn)); // discourage constant spinning
        AddReward(-0.00015f * Mathf.Abs(rb.angularVelocity.y));

        if (enemy != null)
        {
            Vector3 toEnemy = enemy.position - transform.position;
            float d = toEnemy.magnitude;
            Vector3 dir = (d > 0.001f) ? toEnemy / d : transform.forward;

            AddReward(0.004f * (prevDist - d)); // reward getting closer
            prevDist = d;

            float towardSpeed = Vector3.Dot(rb.linearVelocity, dir);
            AddReward(0.0015f * towardSpeed);

            AddReward(0.0007f * Vector3.Dot(transform.forward, dir));
        }

        if (transform.position.y < -2f)
        {
            AddReward(-1f);
            EndEpisode();
        }
    }

    // Manual test: WASD move, Space attack
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var c = actionsOut.ContinuousActions;
        c[0] = Input.GetKey(KeyCode.W) ? 1f : Input.GetKey(KeyCode.S) ? -1f : 0f;
        c[1] = Input.GetKey(KeyCode.D) ? 1f : Input.GetKey(KeyCode.A) ? -1f : 0f;

        if (actionsOut.DiscreteActions.Length > 0)
        {
            var d = actionsOut.DiscreteActions;
            d[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
        }
    }

    // Rewards from combat
    public void OnGotHit() => AddReward(-0.1f);
    public void OnHitEnemy() => AddReward(+0.1f);

    public void Win() { AddReward(+1f); EndEpisode(); }
    public void Lose() { AddReward(-1f); EndEpisode(); }
}
