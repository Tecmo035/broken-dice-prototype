using UnityEngine;

/// <summary>
/// Zarın sonucunu gösteren dünya nesnesi. Oyuncunun "zar şu geldi, hasar da şu
/// oldu" bağını okuyabilmesi için dört evreden geçer:
///
///   1. Dönme   : üstünde hızla değişen sayı (slot makinesi), son anlarda yavaşlar
///   2. Asılma  : sonuç kare çerçeve içinde beyaz ve büyüyerek belirir
///   3. İniş    : çerçeveli sayı düşmana doğru küçülerek iner
///   4. Değme   : hasar UYGULANIR ve düşmandan çerçevesiz kırmızı "-48" fırlar
///
/// Asılma ve iniş birlikte 0,3 sn'yi geçmemeli.
/// </summary>
public class DiceResultDisplay : MonoBehaviour
{
    private enum Phase
    {
        Spinning,
        Holding,
        Descending,
        Done
    }

    [Header("Parçalar")]
    [SerializeField, Tooltip("Sonucu yazan 3B yazı.")]
    private TextMesh text;
    [SerializeField, Tooltip("Zar yüzü görünümü veren kare çerçeve.")]
    private Renderer frameRenderer;
    [SerializeField, Tooltip("Çerçeve ve yazının ölçekleneceği kök.")]
    private Transform visual;

    [Header("Dönme (slot makinesi)")]
    [SerializeField, Tooltip("Sayının en hızlı değişme aralığı (saniye).")]
    private float spinFastInterval = 0.04f;
    [SerializeField, Tooltip("Sayının en yavaş değişme aralığı (saniye).")]
    private float spinSlowInterval = 0.17f;
    [SerializeField, Tooltip("Dönme sırasındaki ölçek.")]
    private float spinScale = 0.72f;

    [Header("Asılma")]
    [SerializeField, Tooltip("Sonucun havada asılı kaldığı süre (saniye).")]
    private float holdDuration = 0.15f;
    [SerializeField, Tooltip("Belirirken aşılan ölçek.")]
    private float holdScale = 1.35f;
    [SerializeField, Tooltip("Belirme (büyüme) süresi (saniye).")]
    private float popDuration = 0.07f;

    [Header("İniş")]
    [SerializeField, Tooltip("Düşmana inme süresi (saniye).")]
    private float descendDuration = 0.14f;
    [SerializeField, Tooltip("İnişin sonundaki ölçek.")]
    private float landScale = 0.5f;

    [Header("Renkler")]
    [SerializeField, Tooltip("Dönerken çerçeve rengi.")]
    private Color spinColor = new Color(0.55f, 0.55f, 0.62f);
    [SerializeField, Tooltip("Normal sonuç çerçeve rengi.")]
    private Color resultColor = Color.white;
    [SerializeField, Tooltip("Kritik sonuç çerçeve rengi (altın).")]
    private Color critColor = new Color(1f, 0.8f, 0.25f);
    [SerializeField, Tooltip("Dönerken yazı rengi.")]
    private Color spinTextColor = new Color(0.8f, 0.8f, 0.85f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Material[] frameMaterials;
    private Transform cameraTransform;

    private Phase phase = Phase.Spinning;
    private Vector3 anchor;
    private Vector3 impactPoint;
    private int faceCount = 6;
    private float phaseTimer;
    private float spinTimer;
    private float spinTotal;
    private float spinElapsed;

    private EnemyHealth target;
    private int damage;
    private bool crit;
    private bool applied;

    private void Awake()
    {
        if (text == null) text = GetComponentInChildren<TextMesh>();
        if (visual == null) visual = transform;

        if (frameRenderer != null) frameMaterials = frameRenderer.materials;

        ApplyFrameColor(spinColor);
        if (text != null) text.color = spinTextColor;
    }

    /// <summary>Dönme evresini başlatır. Süre, zarın dönme süresidir.</summary>
    public void Begin(Vector3 anchorPoint, int faces, float rollDuration)
    {
        anchor = anchorPoint;
        faceCount = Mathf.Max(2, faces);
        spinTotal = Mathf.Max(0.05f, rollDuration);
        spinElapsed = 0f;
        spinTimer = 0f;
        phase = Phase.Spinning;

        transform.position = anchor;
        if (visual != null) visual.localScale = Vector3.one * spinScale;

        ApplyFrameColor(spinColor);
        if (text != null)
        {
            text.color = spinTextColor;
            text.text = Random.Range(1, faceCount + 1).ToString();
        }
    }

    /// <summary>Zar düşmanı takip ettiği için dayanak noktası her kare tazelenir.</summary>
    public void UpdateAnchor(Vector3 anchorPoint)
    {
        anchor = anchorPoint;
    }

    /// <summary>
    /// Sonuç belli oldu: asılma evresine geçilir. Hasar, iniş bittiğinde
    /// gösterge düşmana değdiği anda uygulanır.
    /// </summary>
    public void Resolve(int result, bool isCrit, EnemyHealth enemy, int finalDamage, Vector3 landPoint)
    {
        phase = Phase.Holding;
        phaseTimer = 0f;

        crit = isCrit;
        target = enemy;
        damage = finalDamage;
        impactPoint = landPoint;

        if (text != null)
        {
            text.text = result.ToString();
            text.color = Color.white;
        }

        ApplyFrameColor(crit ? critColor : resultColor);

        if (visual != null) visual.localScale = Vector3.one * (spinScale * 0.6f);

        if (crit && Fx.Instance != null)
        {
            Fx.Instance.ShowText(anchor + Vector3.up * 0.75f, "KRİTİK!", critColor, 1.5f);
        }
    }

    /// <summary>Düşman öldü ya da atış iptal oldu: sessizce kaybolur.</summary>
    public void Cancel()
    {
        phase = Phase.Done;
        Destroy(gameObject);
    }

    private void LateUpdate()
    {
        if (GamePause.Paused) return;

        float dt = Time.deltaTime;

        switch (phase)
        {
            case Phase.Spinning:
                UpdateSpinning(dt);
                break;
            case Phase.Holding:
                UpdateHolding(dt);
                break;
            case Phase.Descending:
                UpdateDescending(dt);
                break;
        }

        FaceCamera();
    }

    private void UpdateSpinning(float dt)
    {
        transform.position = anchor;

        spinElapsed += dt;
        spinTimer -= dt;

        if (spinTimer > 0f) return;

        // Dönme sonuna yaklaştıkça sayı daha yavaş değişir.
        float t = Mathf.Clamp01(spinElapsed / spinTotal);
        spinTimer = Mathf.Lerp(spinFastInterval, spinSlowInterval, t * t);

        if (text != null) text.text = Random.Range(1, faceCount + 1).ToString();
    }

    private void UpdateHolding(float dt)
    {
        transform.position = anchor;
        phaseTimer += dt;

        // Kısa bir büyüme, sonra asılı kalma.
        float pop = Mathf.Max(0.01f, popDuration);
        float scale = phaseTimer < pop
            ? Mathf.Lerp(spinScale * 0.6f, holdScale, phaseTimer / pop)
            : holdScale;

        if (visual != null) visual.localScale = Vector3.one * scale;

        if (phaseTimer < holdDuration + pop) return;

        phase = Phase.Descending;
        phaseTimer = 0f;
    }

    private void UpdateDescending(float dt)
    {
        phaseTimer += dt;

        float duration = Mathf.Max(0.01f, descendDuration);
        float t = Mathf.Clamp01(phaseTimer / duration);

        // Hedef hareket ediyorsa iniş noktası da onu takip etsin.
        Vector3 destination = target != null && target.IsAlive ? target.HeadPoint : impactPoint;

        transform.position = Vector3.Lerp(anchor, destination, t * t);
        if (visual != null) visual.localScale = Vector3.one * Mathf.Lerp(holdScale, landScale, t);

        if (t < 1f) return;

        Impact(destination);
    }

    /// <summary>Gösterge düşmana değdi: hasar burada uygulanır.</summary>
    private void Impact(Vector3 point)
    {
        phase = Phase.Done;

        if (applied)
        {
            Destroy(gameObject);
            return;
        }

        applied = true;

        if (target != null && target.IsAlive)
        {
            // Çerçevesiz, kırmızı-turuncu, başında eksi olan hasar sayısı.
            if (Fx.Instance != null) Fx.Instance.ShowHitDamage(target.HeadPoint, damage, crit);

            target.TakeDamage(damage);

            CombatModifiers mods = CombatModifiers.Instance;
            if (mods != null) mods.ApplySideEffects(target, damage, point);
        }

        Destroy(gameObject);
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

    private void ApplyFrameColor(Color color)
    {
        if (frameMaterials == null) return;

        for (int i = 0; i < frameMaterials.Length; i++)
        {
            Material m = frameMaterials[i];
            if (m == null) continue;

            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, color);
            if (m.HasProperty(LegacyColorId)) m.SetColor(LegacyColorId, color);
            if (m.HasProperty(EmissionColorId)) m.SetColor(EmissionColorId, color * 1.4f);
        }
    }
}
