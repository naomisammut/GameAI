using UnityEngine;

public class GladiatorHealth : MonoBehaviour
{
    [SerializeField] private int maxHP = 5;

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

    // Attacker passed in so we can reward the hitter and end both episodes correctly.
    public void TakeHit(GladiatorAgentV2 attacker)
    {
        hp--;

        if (myAgent != null) myAgent.OnGotHit();
        if (attacker != null) attacker.OnHitEnemy();

        if (hp <= 0)
        {
            if (myAgent != null) myAgent.Lose();
            if (attacker != null) attacker.Win();
        }
    }
}
