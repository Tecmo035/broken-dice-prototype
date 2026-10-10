using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Oyuncunun yanındaki en yakın etkileşimi bulur ve E'ye basıldığında tetikler.
/// HUD ipucu yazısını buradan okur.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField, Tooltip("Etkileşim aranırken yarıçapa eklenen pay (birim).")]
    private float extraReach = 0.3f;

    private PlayerController player;

    /// <summary>Şu anda menzildeki en yakın etkileşim; yoksa null.</summary>
    public Interactable Current { get; private set; }

    private void Awake()
    {
        player = GetComponent<PlayerController>();
    }

    private void Update()
    {
        // Panel açıkken ipucu da etkileşim de olmasın.
        if (GamePause.Paused)
        {
            Current = null;
            return;
        }

        Current = FindNearest();

        if (Current == null) return;

        // Panel bu karede kapandıysa onu kapatan E basımı burada tekrar
        // görünür ve paneli anında yeniden açardı.
        if (GamePause.ChangedThisFrame) return;

        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;

        Current.Interact(player);
    }

    private Interactable FindNearest()
    {
        Interactable nearest = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < Interactable.All.Count; i++)
        {
            Interactable candidate = Interactable.All[i];
            if (candidate == null) continue;

            Vector3 d = candidate.transform.position - transform.position;
            d.y = 0f;

            float reach = candidate.Radius + extraReach;
            float sqr = d.sqrMagnitude;

            if (sqr > reach * reach || sqr >= bestSqr) continue;

            bestSqr = sqr;
            nearest = candidate;
        }

        return nearest;
    }
}
