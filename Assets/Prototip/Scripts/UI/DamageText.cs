using UnityEngine;

/// <summary>
/// Dünyada süzülen hasar yazısı (3B TextMesh). Yukarı çıkar, küçülür,
/// saydamlaşır ve her zaman kameraya bakar.
/// </summary>
[RequireComponent(typeof(TextMesh))]
public class DamageText : MonoBehaviour
{
    [Header("Süzülme")]
    [SerializeField, Tooltip("Yazının ekranda kalma süresi (saniye).")]
    private float lifetime = 0.9f;
    [SerializeField, Tooltip("Yukarı doğru süzülme hızı (birim/saniye).")]
    private float riseSpeed = 1.8f;

    [Header("Ölçek")]
    [SerializeField, Tooltip("Başlangıç ölçeği.")]
    private float startScale = 1f;
    [SerializeField, Tooltip("Bitiş ölçeği.")]
    private float endScale = 0.45f;
    [SerializeField, Tooltip("Doğarken aşılan ölçek çarpanı (vurgu için).")]
    private float popScale = 1.5f;
    [SerializeField, Tooltip("Aşırı büyüme süresi (saniye).")]
    private float popDuration = 0.1f;

    private TextMesh text;
    private Transform cameraTransform;
    private Color baseColor = Color.white;
    private float sizeMultiplier = 1f;
    private float age;

    private void Awake()
    {
        if (text == null) text = GetComponent<TextMesh>();
    }

    /// <summary>Yazıyı, rengini ve büyüklük çarpanını ayarlar.</summary>
    public void Setup(string value, Color color, float size)
    {
        if (text == null) text = GetComponent<TextMesh>();

        baseColor = color;
        sizeMultiplier = Mathf.Max(0.01f, size);

        if (text != null)
        {
            text.text = value;
            text.color = baseColor;
        }

        transform.localScale = Vector3.one * (startScale * sizeMultiplier);
    }

    private void LateUpdate()
    {
        // Kritikte zaman yavaşladığı için yazı unscaled süreyle akıyor.
        float dt = Time.unscaledDeltaTime;
        age += dt;

        float t = lifetime <= 0f ? 1f : Mathf.Clamp01(age / lifetime);

        transform.position += Vector3.up * (riseSpeed * dt);

        float s = CurrentScale() * sizeMultiplier;
        transform.localScale = new Vector3(s, s, s);

        if (text != null)
        {
            Color c = baseColor;
            c.a = 1f - t * t;
            text.color = c;
        }

        FaceCamera();

        if (age >= lifetime) Destroy(gameObject);
    }

    /// <summary>
    /// Yazı önce hedef boyutunu aşarak "patlar", sonra süzülürken küçülür.
    /// Vuruşun vurgusu bu kısa aşırı büyümeden geliyor.
    /// </summary>
    private float CurrentScale()
    {
        float pop = Mathf.Max(0.01f, popDuration);
        float peak = startScale * Mathf.Max(1f, popScale);

        if (age < pop) return Mathf.Lerp(startScale * 0.35f, peak, age / pop);

        float after = Mathf.Clamp01((age - pop) / Mathf.Max(0.01f, lifetime - pop));
        return Mathf.Lerp(peak, endScale, after);
    }

    private void FaceCamera()
    {
        if (cameraTransform == null)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            cameraTransform = cam.transform;
        }

        transform.rotation = cameraTransform.rotation;
    }
}
