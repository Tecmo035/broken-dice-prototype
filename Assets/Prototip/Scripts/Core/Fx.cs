using System.Collections;
using UnityEngine;

/// <summary>Yan hasar türleri; her biri ayrı renkte ve küçük sayılarla gösterilir.</summary>
public enum SideDamageKind
{
    Blast,
    Chain,
    Burn
}

/// <summary>
/// Tüm "his" efektlerinin tek kapısı: hasar yazıları, parçacık patlamaları,
/// kamera sarsıntısı ve kritikteki kısa zaman yavaşlaması.
/// Prefab referansları burada bir kez bağlanır; zar ve düşmanlar sadece haber verir.
/// </summary>
public class Fx : MonoBehaviour
{
    /// <summary>Sahnedeki tek Fx örneği. Kullanmadan önce null kontrolü yapılmalı.</summary>
    public static Fx Instance { get; private set; }

    [Header("Prefablar")]
    [SerializeField, Tooltip("Dünyada süzülen hasar yazısı prefabı.")]
    private GameObject damageTextPrefab;
    [SerializeField, Tooltip("Normal vuruş parçacık patlaması.")]
    private GameObject hitBurstPrefab;
    [SerializeField, Tooltip("Kritik vuruş parçacık patlaması.")]
    private GameObject critBurstPrefab;
    [SerializeField, Tooltip("Düşman ölüm parçacık patlaması.")]
    private GameObject deathBurstPrefab;
    [SerializeField, Tooltip("Yanma alevi (döngülü, düşmana bağlanır).")]
    private GameObject burnFlamePrefab;
    [SerializeField, Tooltip("Patlama buff'ının patlaması.")]
    private GameObject blastBurstPrefab;
    [SerializeField, Tooltip("Boss ölümünün büyük patlaması.")]
    private GameObject bossDeathPrefab;
    [SerializeField, Tooltip("Sandık belirme efekti.")]
    private GameObject chestAppearPrefab;

    [Header("Referanslar")]
    [SerializeField, Tooltip("Sarsıntı uygulanacak izometrik kamera. Boş ise otomatik bulunur.")]
    private IsometricCamera targetCamera;

    [Header("Hasar Yazısı")]
    [SerializeField, Tooltip("Normal vuruş hasar rengi (kırmızı-turuncu).")]
    private Color hitDamageColor = new Color(1f, 0.32f, 0.18f);
    [SerializeField, Tooltip("Kritik vuruş hasar rengi.")]
    private Color critDamageColor = new Color(1f, 0.6f, 0.1f);
    [SerializeField, Tooltip("Normal vuruş hasar yazısı büyüklüğü.")]
    private float hitTextSize = 1.25f;
    [SerializeField, Tooltip("Kritik vuruş hasar yazısı büyüklüğü.")]
    private float critTextSize = 2.1f;
    [SerializeField, Tooltip("Yazının çıkacağı dikey kayma.")]
    private float textHeightOffset = 0.4f;

    [Header("Yan Hasar Yazıları")]
    [SerializeField, Tooltip("Patlama hasarı rengi.")]
    private Color blastDamageColor = new Color(1f, 0.72f, 0.3f);
    [SerializeField, Tooltip("Zincir hasarı rengi.")]
    private Color chainDamageColor = new Color(0.45f, 0.95f, 1f);
    [SerializeField, Tooltip("Yanma hasarı rengi.")]
    private Color burnDamageColor = new Color(1f, 0.5f, 0.12f);
    [SerializeField, Tooltip("Yan hasar yazısı büyüklüğü (ana hasardan küçük).")]
    private float sideTextSize = 0.7f;

    [Header("Oyuncu Hasarı")]
    [SerializeField, Tooltip("Oyuncu hasar alınca kamera sarsıntı şiddeti.")]
    private float hurtShake = 0.3f;
    [SerializeField, Tooltip("Oyuncu hasar alınca sarsıntı süresi (saniye).")]
    private float hurtShakeTime = 0.2f;

    [Header("Boss Ölümü")]
    [SerializeField, Tooltip("Boss ölümünde kamera sarsıntı şiddeti.")]
    private float bossDeathShake = 1.1f;
    [SerializeField, Tooltip("Boss ölümünde sarsıntı süresi (saniye).")]
    private float bossDeathShakeTime = 0.7f;

    private Coroutine hitStopRoutine;

    private void Awake()
    {
        Instance = this;

        // Önceki oyundan takılı kalmış bir zaman yavaşlaması olmasın.
        GamePause.ForceResume();

        if (targetCamera == null) targetCamera = FindFirstObjectByType<IsometricCamera>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        GamePause.ForceResume();
    }

    // --- Yazılar ---------------------------------------------------------

    /// <summary>
    /// Vuruşun ana hasar sayısı: çerçevesiz, kırmızı-turuncu, başında eksi olan.
    /// Sonuç sayısından (DiceResultDisplay) kasıtlı olarak farklı renk ve boyutta.
    /// </summary>
    public void ShowHitDamage(Vector3 position, int amount, bool crit)
    {
        ShowText(position, "-" + amount,
            crit ? critDamageColor : hitDamageColor,
            crit ? critTextSize : hitTextSize);
    }

    /// <summary>Patlama, Zincir ve yanma gibi yan hasarlar: ayrı renk, küçük sayı.</summary>
    public void ShowSideDamage(Vector3 position, int amount, SideDamageKind kind)
    {
        Color color;
        switch (kind)
        {
            case SideDamageKind.Blast: color = blastDamageColor; break;
            case SideDamageKind.Chain: color = chainDamageColor; break;
            default: color = burnDamageColor; break;
        }

        ShowText(position, "-" + amount, color, sideTextSize);
    }

    /// <summary>Geriye dönük uyumluluk: ana hasar yazısı.</summary>
    public void ShowDamage(Vector3 position, int amount, bool crit)
    {
        ShowHitDamage(position, amount, crit);
    }

    /// <summary>Dünyada serbest yazı (ör. "KRİTİK!" ya da "1 — tekrar!").</summary>
    public void ShowText(Vector3 position, string value, Color color, float size)
    {
        if (damageTextPrefab == null) return;

        GameObject go = Instantiate(damageTextPrefab, position + Vector3.up * textHeightOffset, Quaternion.identity);
        if (!go.TryGetComponent(out DamageText text)) return;

        text.Setup(value, color, size);
    }

    // --- Parçacıklar -----------------------------------------------------

    /// <summary>Zar sonucu belirlendiğinde ya da bir şey çarptığında çıkan kıvılcım.</summary>
    public void HitBurst(Vector3 position, bool crit)
    {
        GameObject prefab = crit ? critBurstPrefab : hitBurstPrefab;
        Spawn(prefab, position, null);
    }

    /// <summary>Düşman öldüğünde çıkan küçük patlama; düşmanın rengini alır.</summary>
    public void DeathBurst(Vector3 position, Color color)
    {
        Spawn(deathBurstPrefab, position, color);
    }

    /// <summary>Boss ölümünün büyük patlaması.</summary>
    public void BossDeath(Vector3 position, Color color)
    {
        GameObject prefab = bossDeathPrefab != null ? bossDeathPrefab : critBurstPrefab;
        Spawn(prefab, position, color);

        ShakeCamera(bossDeathShake, bossDeathShakeTime);
    }

    /// <summary>Oda temizlenince sandığın belirme efekti.</summary>
    public void ChestAppear(Vector3 position)
    {
        GameObject prefab = chestAppearPrefab != null ? chestAppearPrefab : hitBurstPrefab;
        Spawn(prefab, position + Vector3.up * 0.3f, new Color(1f, 0.85f, 0.4f));
    }

    /// <summary>Patlama buff'ının patlaması. Yarıçap görsel ölçeğe yansıtılır.</summary>
    public void BlastBurst(Vector3 position, float radius)
    {
        if (blastBurstPrefab == null) return;

        GameObject go = Instantiate(blastBurstPrefab, position, Quaternion.identity);
        go.transform.localScale = Vector3.one * Mathf.Max(0.2f, radius / 3f);
    }

    /// <summary>
    /// Yanan düşmanın üzerine bağlanan döngülü alev. Yanma bitince çağıran
    /// nesne döndürülen örneği yok eder.
    /// </summary>
    public GameObject SpawnFlame(Transform parent, Vector3 localOffset)
    {
        if (burnFlamePrefab == null || parent == null) return null;

        GameObject go = Instantiate(burnFlamePrefab, parent);
        go.transform.localPosition = localOffset;
        go.transform.localRotation = Quaternion.identity;
        return go;
    }

    // --- Kamera ve zaman -------------------------------------------------

    /// <summary>Oyuncu hasar aldığında: kamera sarsıntısı ve ekran kenarı kırmızı parlaması.</summary>
    public void PlayerHurt()
    {
        ShakeCamera(hurtShake, hurtShakeTime);
        if (PrototipHud.Instance != null) PrototipHud.Instance.FlashHurt();
    }

    public void ShakeCamera(float amplitude, float duration)
    {
        if (targetCamera == null) targetCamera = FindFirstObjectByType<IsometricCamera>();
        if (targetCamera != null) targetCamera.Shake(amplitude, duration);
    }

    /// <summary>Kritikte ve boss ölümünde kısa zaman yavaşlaması (hit-stop).</summary>
    public void HitStop(float scale, float duration)
    {
        if (duration <= 0f) return;

        // Sandık ya da Silahlık paneli oyunu durdurmuşsa hit-stop hiç başlamaz.
        if (GamePause.Paused) return;

        if (hitStopRoutine != null) StopCoroutine(hitStopRoutine);
        hitStopRoutine = StartCoroutine(HitStopRoutine(scale, duration));
    }

    private IEnumerator HitStopRoutine(float scale, float duration)
    {
        Time.timeScale = Mathf.Clamp(scale, 0.01f, 1f);
        yield return new WaitForSecondsRealtime(duration);

        // Bu arada oyun duraklatıldıysa timeScale'e dokunmuyoruz; yoksa panel
        // açıkken oyun kendiliğinden devam ederdi.
        if (!GamePause.Paused) Time.timeScale = 1f;
        hitStopRoutine = null;
    }

    private void Spawn(GameObject prefab, Vector3 position, Color? color)
    {
        if (prefab == null) return;

        GameObject go = Instantiate(prefab, position, Quaternion.identity);
        if (color.HasValue && go.TryGetComponent(out OneShotParticle burst)) burst.SetColor(color.Value);
    }
}
