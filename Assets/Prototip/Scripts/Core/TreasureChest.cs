using UnityEngine;

/// <summary>
/// Odanın sandığı. Oda BAŞLARKEN yoktur; tüm düşmanlar ölünce sandık noktasında
/// küçük bir belirme efektiyle ortaya çıkar. Oyuncu almadan kapıdan geçebilir;
/// odadan çıkınca alınmamış sandık silinir.
/// </summary>
public class TreasureChest : Interactable
{
    [Header("Görsel")]
    [SerializeField, Tooltip("Hafifçe süzülen gövde.")]
    private Transform body;
    [SerializeField, Tooltip("Süzülme yüksekliği (birim).")]
    private float bobHeight = 0.08f;
    [SerializeField, Tooltip("Süzülme hızı.")]
    private float bobSpeed = 1.6f;
    [SerializeField, Tooltip("Dönme hızı (derece/saniye).")]
    private float spinSpeed = 18f;

    [Header("Belirme")]
    [SerializeField, Tooltip("Yerden yükselip büyüme süresi (saniye).")]
    private float appearDuration = 0.35f;
    [SerializeField, Tooltip("Belirirken yükselinecek mesafe (birim).")]
    private float appearRise = 0.7f;

    private Vector3 bodyBasePosition;
    private Vector3 spawnPosition;
    private float bobTimer;
    private float appearTimer;
    private bool consumed;

    public override string Prompt => "Sandığı aç";

    public override bool CanInteract => !consumed && appearTimer >= appearDuration;

    private void Awake()
    {
        if (body != null) bodyBasePosition = body.localPosition;

        spawnPosition = transform.position;
        transform.position = spawnPosition - Vector3.up * appearRise;
        transform.localScale = Vector3.one * 0.2f;
    }

    private void Update()
    {
        if (GamePause.Paused) return;

        float dt = Time.deltaTime;

        // Belirme: yerden yükselerek büyür.
        if (appearTimer < appearDuration)
        {
            appearTimer += dt;

            float t = Mathf.Clamp01(appearTimer / Mathf.Max(0.01f, appearDuration));
            transform.position = Vector3.Lerp(spawnPosition - Vector3.up * appearRise, spawnPosition, t);
            transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, t);
            return;
        }

        transform.position = spawnPosition;
        transform.localScale = Vector3.one;

        if (body == null) return;

        body.Rotate(Vector3.up, spinSpeed * dt, Space.Self);

        bobTimer += dt * bobSpeed;
        body.localPosition = bodyBasePosition + Vector3.up * (Mathf.Sin(bobTimer) * bobHeight);
    }

    public override void Interact(PlayerController player)
    {
        if (consumed || !CanInteract) return;

        ChestPanel panel = ChestPanel.Instance;
        if (panel == null)
        {
            Say("Sandık paneli bulunamadı.", 2f);
            return;
        }

        if (panel.Open(this)) return;

        Say("Sandıkta sana verebileceği bir şey kalmamış.", 2.2f);
    }

    /// <summary>Panelde seçim yapıldı: sandık tükenir ve kaybolur.</summary>
    public void Consume()
    {
        if (consumed) return;

        consumed = true;
        Destroy(gameObject);
    }
}
