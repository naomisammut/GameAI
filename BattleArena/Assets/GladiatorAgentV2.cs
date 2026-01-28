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
    [SerializeField] private Transform spawnPoint; // optional (auto: Spawn_A / Spawn_B)
    [SerializeField] private Transform enemy;      // optional (auto-find other agent)

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
        GameObject sp = GameObject.Find(spawnName);
        if (sp != null) spawnPoint = sp.transform;
    }

    private void AutoFindEnemy()
    {
        if (enemy != null) return;

        GladiatorAgentV2[] all = FindObjectsOfType<GladiatorAgentV2>();
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

        // Reset pose
        if (spawnPoint != null)
            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        // Reset physics (Unity 6)
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Reset health + weapon
        if (myHealth != null) myHealth.ResetHP();
        if (weapon != null) weapon.ResetWeapon();

        prevDist = (enemy != null) ? Vector3.Distance(transform.position, enemy.position) : 0f;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        CacheComponents();
        AutoFindEnemy();

        // Hard safety
        if (sensor == null || rb == null)
        {
            // Output something consistent-ish and return (prevents spam crashes)
            sensor?.AddObservation(0f); sensor?.AddObservation(0f);
            sensor?.AddObservation(0f); sensor?.AddObservation(0f);
            sensor?.AddObservation(0f); sensor?.AddObservation(0f); sensor?.AddObservation(0f);
            sensor?.AddObservation(1f);
            return;
        }

        // Self forward (2)
        Vector3 f = transform.forward;
        sensor.AddObservation(f.x);
        sensor.AddObservation(f.z);

        // Self velocity LOCAL (2)
        Vector3 vLocal = transform.InverseTransformDirection(rb.linearVelocity);
        sensor.AddObservation(vLocal.x);
        sensor.AddObservation(vLocal.z);

        // Enemy direction LOCAL + distance (3)
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

        // Health (1)
        sensor.AddObservation(myHealth != null ? myHealth.HPNormalized : 1f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        CacheComponents();
        AutoFindEnemy();
        if (rb == null) return;

        // Continuous: move, turn
        float move = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float turn = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        rb.AddForce(transform.forward * (move * moveSpeed), ForceMode.Acceleration);
        transform.Rotate(Vector3.up, turn * turnSpeed * Time.fixedDeltaTime);

        // Discrete: attack (0/1)
        if (actions.DiscreteActions.Length > 0 && weapon != null)
            weapon.TryAttack(actions.DiscreteActions[0] == 1);

        // --- Reward shaping ---

        // Time penalty
        AddReward(-0.0002f);

        // Encourage some movement input
        AddReward(0.00015f * Mathf.Abs(move));

        // Penalize excessive turning (breaks infinite spin)
        AddReward(-0.00025f * Mathf.Abs(turn));
        if (rb != null) AddReward(-0.00015f * Mathf.Abs(rb.angularVelocity.y));

        // Reward moving toward enemy
        if (enemy != null && rb != null)
        {
            Vector3 toEnemy = enemy.position - transform.position;
            float d = toEnemy.magnitude;
            Vector3 dir = (d > 0.001f) ? toEnemy / d : transform.forward;

            // progress
            AddReward(0.004f * (prevDist - d));
            prevDist = d;

            // velocity toward enemy
            float towardSpeed = Vector3.Dot(rb.linearVelocity, dir);
            AddReward(0.0015f * towardSpeed);

            // facing enemy
            AddReward(0.0007f * Vector3.Dot(transform.forward, dir));
        }

        // Fall off arena => lose
        if (transform.position.y < -2f)
        {
            AddReward(-1f);
            EndEpisode();
        }
    }

    // Called by Health when taking damage
    public void OnGotHit() => AddReward(-0.1f);

    // Called by Health when we successfully hit enemy
    public void OnHitEnemy() => AddReward(+0.1f);

    public void Win()
    {
        AddReward(+1f);
        EndEpisode();
    }

    public void Lose()
    {
        AddReward(-1f);
        EndEpisode();
    }
}
