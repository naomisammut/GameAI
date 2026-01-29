using UnityEngine;
using UnityEngine.AI;

public class StrategistAI : MonoBehaviour
{
    [Header("Stats")]
    public float maxHealth = 100f;
    public float health = 100f;
    public int maxAmmo = 30;
    public int ammo = 10;

    [Header("Decision")]
    public float decisionInterval = 0.5f;

    [Tooltip("If health% <= this, Survival overrides utility.")]
    public float lowHealthThreshold = 0.25f;

    [Tooltip("Once an action is picked, stick with it for this many seconds.")]
    public float actionCommitTime = 2.0f;

    [Tooltip("Only switch actions if the new one is better by this much.")]
    public float switchMargin = 0.15f;

    [Tooltip("Small randomness to avoid repeating identical sequences.")]
    public float scoreNoise = 0.03f;

    [Header("Threat")]
    public Transform threat;

    enum ActionType { Heal, Ammo, Hide, Flee }
    ActionType currentAction = ActionType.Hide;

    NavMeshAgent agent;
    float nextDecisionTime;
    float actionLockUntil;

    Vector3 lastDest;
    bool hasDest;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (Time.time < nextDecisionTime) return;
        nextDecisionTime = Time.time + decisionInterval;

        Decide();
    }

    void Decide()
    {
        // 1) Survival override: always allowed to interrupt
        if ((health / maxHealth) <= lowHealthThreshold)
        {
            // try heal, else hide, else flee
            if (TryHeal()) SetNewAction(ActionType.Heal, lockAction: true);
            else if (TryHide()) SetNewAction(ActionType.Hide, lockAction: true);
            else { FleeFromThreat(); SetNewAction(ActionType.Flee, lockAction: true); }
            return;
        }

        // 2) If we’re still committed to current action, do nothing (prevents flip-flop)
        if (Time.time < actionLockUntil) return;

        // 3) Score all actions (utility)
        float healScore = ScoreHeal() + Random.Range(0f, scoreNoise);
        float ammoScore = ScoreAmmo() + Random.Range(0f, scoreNoise);
        float hideScore = ScoreHide() + Random.Range(0f, scoreNoise);

        // 4) Decide best
        ActionType best = ActionType.Hide;
        float bestScore = hideScore;

        if (healScore > bestScore) { best = ActionType.Heal; bestScore = healScore; }
        if (ammoScore > bestScore) { best = ActionType.Ammo; bestScore = ammoScore; }

        // 5) Switch margin: only switch if clearly better than current
        float currentScore = ScoreFor(currentAction);
        if (best == currentAction || bestScore <= currentScore + switchMargin)
        {
            // keep current action (reduces twitching)
            ContinueCurrentAction();
            return;
        }

        // 6) Commit to new action
        if (best == ActionType.Heal) TryHeal();
        else if (best == ActionType.Ammo) TryGetAmmo();
        else TryHide();

        SetNewAction(best, lockAction: true);
    }

    float ScoreFor(ActionType a)
    {
        return a switch
        {
            ActionType.Heal => ScoreHeal(),
            ActionType.Ammo => ScoreAmmo(),
            ActionType.Hide => ScoreHide(),
            ActionType.Flee => 0f,
            _ => 0f
        };
    }

    float ScoreHeal()
    {
        Transform hp = FindNearestWithTag("HealthPack");
        if (!hp) return 0f;

        float hpNeed = 1f - (health / maxHealth);
        float dist = Vector3.Distance(transform.position, hp.position);
        float distFactor = Mathf.Clamp01(1f - (dist / 20f));

        // If threat is close, healing out in the open is worse
        float danger = Danger01(10f);
        float dangerPenalty = Mathf.Lerp(1f, 0.6f, danger);

        return (hpNeed * 0.8f + distFactor * 0.2f) * dangerPenalty;
    }

    float ScoreAmmo()
    {
        Transform am = FindNearestWithTag("AmmoCrate");
        if (!am) return 0f;

        float ammoNeed = 1f - (float)ammo / maxAmmo;
        float dist = Vector3.Distance(transform.position, am.position);
        float distFactor = Mathf.Clamp01(1f - (dist / 20f));

        float danger = Danger01(10f);
        float dangerPenalty = Mathf.Lerp(1f, 0.2f, danger);

        return (ammoNeed * 0.7f + distFactor * 0.3f) * dangerPenalty;
    }

    float ScoreHide()
    {
        float danger = Danger01(15f);
        return Mathf.Max(0.25f, danger);
    }

    float Danger01(float maxDist)
    {
        if (!threat) return 0f;
        float d = Vector3.Distance(transform.position, threat.position);
        return Mathf.Clamp01(1f - (d / maxDist));
    }

    void ContinueCurrentAction()
    {
        // Keep moving toward whatever we were doing without re-setting destinations constantly
    }

    void SetNewAction(ActionType a, bool lockAction)
    {
        currentAction = a;
        if (lockAction) actionLockUntil = Time.time + actionCommitTime;
    }

    bool TryHeal()
    {
        Transform hp = FindNearestWithTag("HealthPack");
        if (!hp) return false;
        SetDestinationSafe(hp.position);
        return true;
    }

    bool TryGetAmmo()
    {
        Transform am = FindNearestWithTag("AmmoCrate");
        if (!am) return false;
        SetDestinationSafe(am.position);
        return true;
    }

    bool TryHide()
    {
        Transform cover = FindNearestWithTag("Cover");
        if (!cover) return false;
        SetDestinationSafe(cover.position);
        return true;
    }

    void FleeFromThreat()
    {
        if (!threat) return;
        Vector3 dir = (transform.position - threat.position).normalized;
        Vector3 fleeTarget = transform.position + dir * 8f;
        SetDestinationSafe(fleeTarget);
    }

    void SetDestinationSafe(Vector3 dest)
    {
        if (!hasDest || Vector3.Distance(lastDest, dest) > 0.5f)
        {
            agent.SetDestination(dest);
            lastDest = dest;
            hasDest = true;
        }
    }

    Transform FindNearestWithTag(string tag)
    {
        GameObject[] objs = GameObject.FindGameObjectsWithTag(tag);
        if (objs == null || objs.Length == 0) return null;

        Transform best = null;
        float bestDist = float.MaxValue;

        foreach (var o in objs)
        {
            float d = Vector3.Distance(transform.position, o.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = o.transform;
            }
        }
        return best;
    }

    public void AddHealth(float amount) => health = Mathf.Clamp(health + amount, 0f, maxHealth);
    public void AddAmmo(int amount) => ammo = Mathf.Clamp(ammo + amount, 0, maxAmmo);
}
