using UnityEngine;

public class GladiatorWeapon : MonoBehaviour
{
    [SerializeField] private Collider hitTrigger;   // Sphere/Box collider on Weapon
    [SerializeField] private bool debugLogs = true;

    [Header("Timing")]
    [SerializeField] private float activeTime = 0.25f;
    [SerializeField] private float cooldown = 0.35f;

    [Header("Backup hit check (works even if already overlapping)")]
    [SerializeField] private float overlapRadius = 0.45f;

    private float activeLeft;
    private float cooldownLeft;
    private bool hitThisSwing;

    private GladiatorAgentV2 ownerAgent;
    private GladiatorHealth ownerHealth;

    private bool SwingActive => activeLeft > 0f;

    private void Awake()
    {
        if (hitTrigger == null) hitTrigger = GetComponent<Collider>();

        ownerAgent = GetComponentInParent<GladiatorAgentV2>();
        ownerHealth = GetComponentInParent<GladiatorHealth>();

        // Keep collider always enabled, only apply damage while SwingActive
        if (hitTrigger != null)
        {
            hitTrigger.enabled = true;
            hitTrigger.isTrigger = true;
        }

        ResetWeapon();
    }

    public void ResetWeapon()
    {
        activeLeft = 0f;
        cooldownLeft = 0f;
        hitThisSwing = false;
    }

    private void Update()
    {
        if (cooldownLeft > 0f) cooldownLeft -= Time.deltaTime;

        if (activeLeft > 0f)
        {
            activeLeft -= Time.deltaTime;
            if (activeLeft < 0f) activeLeft = 0f;
        }
    }

    public void TryAttack(bool wantAttack)
    {
        if (!wantAttack) return;
        if (cooldownLeft > 0f) return;

        hitThisSwing = false;
        activeLeft = activeTime;
        cooldownLeft = cooldown;

        if (debugLogs) Debug.Log($"{ownerAgent?.name} ATTACK");

        // Immediate check in case we're already overlapping
        TryHitFromOverlaps();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!SwingActive || hitThisSwing) return;
        TryHit(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!SwingActive || hitThisSwing) return;
        TryHit(other);
    }

    private void TryHit(Collider other)
    {
        GladiatorHealth otherHealth = other.GetComponentInParent<GladiatorHealth>();
        if (otherHealth == null) return;
        if (otherHealth == ownerHealth) return;

        hitThisSwing = true;

        if (debugLogs) Debug.Log($"{ownerAgent?.name} HIT {otherHealth.name}");

        otherHealth.TakeHit(ownerAgent);
    }

    private void TryHitFromOverlaps()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, overlapRadius);
        for (int i = 0; i < hits.Length && !hitThisSwing; i++)
            TryHit(hits[i]);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, overlapRadius);
    }
}
