using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// E ile etkileşilebilen her şeyin ortak temeli (zindan girişi, sunaklar, eşyalar).
/// Oyuncunun yanındaki en yakın etkileşimi PlayerInteractor bulur.
/// Özel tag ya da layer kullanılmaz; kayıt statik listeyle yapılır.
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    [Header("Etkileşim")]
    [SerializeField, Tooltip("Oyuncunun bu mesafeye girmesi gerekir (birim).")]
    private float radius = 2.6f;

    [SerializeField, Tooltip("İpucu yazısının çıkacağı yükseklik (birim).")]
    private float promptHeight = 1.6f;

    /// <summary>Sahnedeki etkin tüm etkileşimler.</summary>
    public static readonly List<Interactable> All = new List<Interactable>();

    public float Radius => radius;
    public Vector3 PromptPoint => transform.position + Vector3.up * promptHeight;

    /// <summary>HUD'da gösterilecek yazı, örn. "Zindana gir".</summary>
    public abstract string Prompt { get; }

    /// <summary>Şu anda kullanılabilir mi? Kullanılamıyorsa yazı uyarı rengiyle çıkar.</summary>
    public virtual bool CanInteract => true;

    /// <summary>E'ye basıldığında çağrılır.</summary>
    public abstract void Interact(PlayerController player);

    protected virtual void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    protected virtual void OnDisable()
    {
        All.Remove(this);
    }

    protected static void Say(string message, float duration = 2f)
    {
        if (PrototipHud.Instance != null) PrototipHud.Instance.ShowMessage(message, duration);
    }
}
