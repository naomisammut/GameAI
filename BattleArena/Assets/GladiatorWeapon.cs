using UnityEngine;

public class GladiatorWeapon : MonoBehaviour
{
    [SerializeField] private Collider hitTrigger; // should be trigger collider on this object
    [SerializeField] private float activeTime = 0.15f;
    [SerializeField] private float cooldown = 0.35f;

    private float activeLeft;
    private float cooldownLeft;
    private bool hitThisSwing;

    private GladiatorAgentV2 ownerAgent;
    private GladiatorHealth ownerHealth;

    private void Awake()
    {
        if (hitTrigger == null) hitTrigger = GetComponent<Collider>();

        ownerAgent = GetComponentInParent<GladiatorAgentV2>();
        ownerHealth = GetComponentInParent<GladiatorHealth>();

        // collider must be trigger
        if (hitTrigger != null) hitTrigger.isTrigger = true;

        SetHitActive(false);
    }

    public void ResetWeapon()
    {
        activeLeft = 0f;
        cooldownLeft = 0f;
        hitThisSwing = false;
        SetHitActive(false);
    }

    private void Update()
    {
        if (cooldownLeft > 0f) cooldownLeft -= Time.deltaTime;

        if (activeLeft > 0f)
        {
            activeLeft -= Time.deltaTime;
            if (activeLeft <= 0f) SetHitActive(false);
        }
    }

    public void TryAttack(bool wantAttack)
    {
        if (!wantAttack) return;
        if (cooldownLeft > 0f) return;

        hitThisSwing = false;
        activeLeft = activeTime;
        cooldownLeft = cooldown;

        SetHitActive(true);
    }

    private void SetHitActive(bool on)
    {
        if (hitTrigger != null) hitTrigger.enabled = on;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hitThisSwing) return;
        if (hitTrigger == null || !hitTrigger.enabled) return;

        // Hit any OTHER GladiatorHealth (not ourselves)
        GladiatorHealth otherHealth = other.GetComponentInParent<GladiatorHealth>();
        if (otherHealth == null) return;
        if (otherHealth == ownerHealth) return;

        hitThisSwing = true;

        // Apply damage + rewards via TakeHit(attacker)
        otherHealth.TakeHit(ownerAgent);
    }
}
