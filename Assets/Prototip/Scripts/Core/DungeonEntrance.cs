using UnityEngine;

/// <summary>Lobideki zindan girişi. E'ye basınca yeni bir koşu başlar.</summary>
public class DungeonEntrance : Interactable
{
    [SerializeField, Tooltip("Koşuyu başlatacak zindan yöneticisi.")]
    private DungeonManager dungeon;

    public override string Prompt => "Zindana gir";

    public override bool CanInteract => dungeon != null && !dungeon.InDungeon;

    public override void Interact(PlayerController player)
    {
        if (dungeon == null)
        {
            Say("Zindan yöneticisi bağlanmamış.", 2f);
            return;
        }

        dungeon.StartRun();
    }
}
