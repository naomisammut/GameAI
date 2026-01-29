using UnityEngine;

public class GladiatorHealth : MonoBehaviour
{
    [SerializeField] private int maxHP = 5;
    [SerializeField] private bool debugLogs = true;

    private int hp;
    private GladiatorAgentV2 myAgent;

    public float HPNormalized => maxHP > 0 ? (float)hp / maxHP : 0f;

    private void Awake()
    {
        myAgent = GetComponent<GladiatorAgentV2>();
        ResetHP();
    }

    public void ResetHP()
    {
        hp = maxHP;
    }

    public void TakeHit(GladiatorAgentV2 attacker)
    {
        hp--;

        if (debugLogs)
            Debug.Log($"{name} got HIT. HP now = {hp}");

        if (myAgent != null) myAgent.OnGotHit();
        if (attacker != null) attacker.OnHitEnemy();

        if (hp <= 0)
        {
            if (debugLogs)
                Debug.Log($"{name} DIED. Winner: {(attacker != null ? attacker.name : "unknown")}");

            if (myAgent != null) myAgent.Lose();
            if (attacker != null) attacker.Win();
        }
    }
}
