using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody))]
public class GladiatorAgent : Agent
{
    // Keep a list of all agents for nearest-enemy lookup (multi-agent)
    static readonly List<GladiatorAgent> All = new List<GladiatorAgent>();

    [Header("Arena")]
    public Transform arenaCenter;
    public Vector3 arenaHalfExtents = new Vector3(8f, 1f, 8f); // x,z size of arena

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float turnSpeed = 180f;

    [Header("Combat")]
    public float maxHealth = 100f;
    public float attackRange = 1.6f;
    public float attackCooldown = 0.6f;
    public float attackDamage = 20f;

    [Header("Reward Tuning")]
    public float stepPenalty = -0.0005f;        // discourages idling forever
    public float dealDamageReward = 0.05f;      // reward per hit (scaled)
    public float takeDamagePenalty = -0.05f;    // penalty per hit (scaled)
    public float winReward = 1.0f;
    public float losePenalty = -1.0f;

    Rigidbody rb;
    float health;
    float nextAttackTime;
    float lastEnemyDist;
    GladiatorAgent nearestEnemy;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        health = maxHealth;
    }

    public override void OnEpisodeBegin()
    {
        health = maxHealth;
        nextAttackTime = 0f;

        // Reset physics
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Random spawn inside arena
        Vector3 c = arenaCenter ? arenaCenter.position : Vector3.zero;
        float x = Random.Range(-arenaHalfExtents.x, arenaHalfExtents.x);
        float z = Random.Range(-arenaHalfExtents.z, arenaHalfExtents.z);
        transform.position = c + new Vector3(x, 0.5f, z);

        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        // Track distance shaping
        nearestEnemy = FindNearestEnemy();
        lastEnemyDist = nearestEnemy ? Vector3.Distance(transform.position, nearestEnemy.transform.position) : 0f;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        nearestEnemy = FindNearestEnemy();

        // Own state
        sensor.AddObservation(health / maxHealth);               // 0..1
        Vector3 vLocal = transform.InverseTransformDirection(rb.linearVelocity);
        sensor.AddObservation(vLocal.x);
        sensor.AddObservation(vLocal.z);

        // Enemy info (nearest)
        if (nearestEnemy != null)
        {
            Vector3 toEnemy = nearestEnemy.transform.position - transform.position;
            Vector3 toEnemyLocal = transform.InverseTransformDirection(toEnemy);

            float dist = toEnemy.magnitude;
            float distNorm = Mathf.Clamp01(dist / 20f);

            sensor.AddObservation(toEnemyLocal.normalized.x);
            sensor.AddObservation(toEnemyLocal.normalized.z);
            sensor.AddObservation(distNorm);
            sensor.AddObservation(nearestEnemy.health / nearestEnemy.maxHealth);
        }
        else
        {
            // No enemy (shouldn’t happen in multi-agent scene)
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(1f);
            sensor.AddObservation(1f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Small step penalty so it doesn’t stand still forever
        AddReward(stepPenalty);

        // Continuous actions
        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float turn = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);

        // Move in local space
        Vector3 moveLocal = new Vector3(moveX, 0f, moveZ);
        Vector3 moveWorld = transform.TransformDirection(moveLocal) * moveSpeed;

        // Apply movement (simple)
        rb.AddForce(new Vector3(moveWorld.x, 0f, moveWorld.z), ForceMode.Acceleration);

        // Turn
        transform.Rotate(0f, turn * turnSpeed * Time.fixedDeltaTime, 0f);

        // Discrete action: attack
        int attack = actions.DiscreteActions[0]; // 0/1
        if (attack == 1) TryAttack();

        // Distance shaping: tiny reward for closing distance to enemy
        if (nearestEnemy != null)
        {
            float d = Vector3.Distance(transform.position, nearestEnemy.transform.position);
            float delta = lastEnemyDist - d; // positive if you got closer
            AddReward(delta * 0.001f);
            lastEnemyDist = d;
        }

        // Out of bounds penalty (keeps them in arena)
        if (IsOutOfBounds())
        {
            AddReward(-0.01f);
            // push back toward center a bit
            Vector3 c = arenaCenter ? arenaCenter.position : Vector3.zero;
            Vector3 dir = (c - transform.position).normalized;
            rb.AddForce(new Vector3(dir.x, 0f, dir.z) * 10f, ForceMode.Acceleration);
        }
    }

    void TryAttack()
    {
        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + attackCooldown;

        GladiatorAgent enemy = FindNearestEnemy();
        if (enemy == null) return;

        Vector3 toEnemy = enemy.transform.position - transform.position;
        float dist = toEnemy.magnitude;
        if (dist > attackRange) return;

        // Optional: require enemy roughly in front
        float forwardDot = Vector3.Dot(transform.forward, toEnemy.normalized);
        if (forwardDot < 0.2f) return;

        // Deal damage
        enemy.TakeDamage(attackDamage, this);
    }

    public void TakeDamage(float dmg, GladiatorAgent attacker)
    {
        float before = health;
        health = Mathf.Max(0f, health - dmg);

        float frac = (before - health) / maxHealth; // 0..1

        // Attacker reward, victim penalty
        if (attacker != null) attacker.AddReward(dealDamageReward * frac);
        AddReward(takeDamagePenalty * frac);

        if (health <= 0f)
        {
            // I lost
            AddReward(losePenalty);
            EndEpisode();

            // Attacker wins this fight
            if (attacker != null)
                attacker.AddReward(winReward);
        }
    }

    GladiatorAgent FindNearestEnemy()
    {
        GladiatorAgent best = null;
        float bestD = float.MaxValue;

        for (int i = 0; i < All.Count; i++)
        {
            var a = All[i];
            if (a == null || a == this) continue;

            float d = Vector3.Distance(transform.position, a.transform.position);
            if (d < bestD)
            {
                bestD = d;
                best = a;
            }
        }
        return best;
    }

    bool IsOutOfBounds()
    {
        Vector3 c = arenaCenter ? arenaCenter.position : Vector3.zero;
        Vector3 p = transform.position - c;
        return Mathf.Abs(p.x) > arenaHalfExtents.x || Mathf.Abs(p.z) > arenaHalfExtents.z;
    }

    // Optional: for keyboard testing (not used in training)
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var c = actionsOut.ContinuousActions;
        c[0] = Input.GetAxis("Horizontal");
        c[1] = Input.GetAxis("Vertical");
        c[2] = (Input.GetKey(KeyCode.Q) ? -1f : 0f) + (Input.GetKey(KeyCode.E) ? 1f : 0f);

        var d = actionsOut.DiscreteActions;
        d[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }
}
