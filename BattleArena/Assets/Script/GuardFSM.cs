using UnityEngine;
using UnityEngine.AI;

public class GuardFSM : MonoBehaviour
{
    enum State { Patrol, Chase, Search }
    State state = State.Patrol;

    [Header("References")]
    public Transform[] patrolPoints;
    public Transform target;
    public Transform eyes;

    [Header("Vision")]
    public float viewDistance = 10f;
    public LayerMask obstacleMask;

    [Header("Speeds")]
    public float patrolSpeed = 3.5f;
    public float chaseSpeed = 6f;

    [Header("Search")]
    public float searchTime = 3f;

    NavMeshAgent agent;
    int patrolIndex = 0;
    Vector3 lastKnownTargetPos;
    float searchTimer;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!eyes) eyes = transform;
    }

    void Start()
    {
        SetState(State.Patrol);
    }

    void Update()
    {
        bool sees = CanSeeTarget();

        if (state == State.Patrol)
        {
            if (sees) { SetState(State.Chase); return; }

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                agent.SetDestination(patrolPoints[patrolIndex].position);
            }
        }
        else if (state == State.Chase)
        {
            if (sees)
            {
                lastKnownTargetPos = target.position;
                agent.SetDestination(lastKnownTargetPos);
            }
            else
            {
                SetState(State.Search);
            }
        }
        else if (state == State.Search)
        {
            if (sees) { SetState(State.Chase); return; }

            searchTimer -= Time.deltaTime;

            // once reached last known spot, just wait/look for a bit
            if (searchTimer <= 0f)
                SetState(State.Patrol);
        }
    }

    void SetState(State newState)
    {
        state = newState;

        if (state == State.Patrol)
        {
            agent.speed = patrolSpeed;
            patrolIndex = Mathf.Clamp(patrolIndex, 0, patrolPoints.Length - 1);
            agent.SetDestination(patrolPoints[patrolIndex].position);
        }
        else if (state == State.Chase)
        {
            agent.speed = chaseSpeed;
        }
        else if (state == State.Search)
        {
            agent.speed = patrolSpeed;
            searchTimer = searchTime;
            agent.SetDestination(lastKnownTargetPos);
        }
    }

    bool CanSeeTarget()
    {
        if (!target) return false;

        Vector3 toTarget = target.position - eyes.position;
        float dist = toTarget.magnitude;
        if (dist > viewDistance) return false;

        // line of sight check (blocked by walls)
        Vector3 dir = toTarget.normalized;
        if (Physics.Raycast(eyes.position, dir, dist, obstacleMask))
            return false;

        return true;
    }
}
