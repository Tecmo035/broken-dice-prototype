using UnityEngine;

/// <summary>
/// Lobideki stat sunağı. Harcanmamış ödül puanı varken E'ye basılırsa
/// bağlı olduğu stat kalıcı olarak 1 artar.
/// </summary>
public class StatAltar : Interactable
{
    [SerializeField, Tooltip("Bu sunağın arttırdığı stat.")]
    private StatType stat = StatType.Strength;

    public StatType Stat => stat;

    public override string Prompt
    {
        get
        {
            PlayerProgress progress = PlayerProgress.Instance;
            if (progress == null) return PlayerProgress.StatName(stat);

            int current = progress.GetStat(stat);

            if (progress.RewardPoints <= 0)
            {
                return PlayerProgress.StatName(stat) + " " + current + " — ödül puanı yok";
            }

            return PlayerProgress.StatName(stat) + " " + current + " → " + (current + 1);
        }
    }

    public override bool CanInteract => PlayerProgress.Instance != null && PlayerProgress.Instance.RewardPoints > 0;

    public override void Interact(PlayerController player)
    {
        PlayerProgress progress = PlayerProgress.Instance;
        if (progress == null) return;

        if (!progress.SpendPoint(stat))
        {
            Say("Ödül puanın yok. Zindanı bitirerek kazan.", 2f);
            return;
        }

        Say(PlayerProgress.StatName(stat) + " kalıcı olarak " + progress.GetStat(stat) + " oldu!", 2.2f);
    }
}
