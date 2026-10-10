using UnityEngine;

/// <summary>Lobideki Silahlık. E ile eşya seçim paneli açılır.</summary>
public class Armory : Interactable
{
    [SerializeField, Tooltip("Açılacak eşya paneli. Boş ise otomatik bulunur.")]
    private ArmoryPanel panel;

    public override string Prompt => "Silahlığı aç";

    public override bool CanInteract => true;

    public override void Interact(PlayerController player)
    {
        if (panel == null) panel = ArmoryPanel.Instance;

        if (panel == null)
        {
            Say("Silahlık paneli bulunamadı.", 2f);
            return;
        }

        panel.Open();
    }
}
