using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Oyuncu canı. Hasar alınca kısa dokunulmazlık ve görsel yanıp sönme verir.
/// Kaçınma hamlesi sırasında da hasar alınmaz.
///
/// Azami can = temel can + Dayanıklılık statı. Dayanıklılık artınca azami can
/// hemen güncellenir ve fark kadar can eklenir.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Can")]
    [SerializeField, Tooltip("Dayanıklılık 0 iken azami can.")]
    private int baseMaxHealth = 5;

    [Header("Dokunulmazlık")]
    [SerializeField, Tooltip("Hasar sonrası dokunulmazlık süresi (saniye).")]
    private float invulnerabilityDuration = 0.6f;
    [SerializeField, Tooltip("Yanıp sönme aralığı (saniye).")]
    private float blinkInterval = 0.06f;

    [Header("Referanslar")]
    [SerializeField, Tooltip("Yanıp sönerken kapatılıp açılacak görseller.")]
    private Renderer[] bodyRenderers;
    [SerializeField, Tooltip("Kaçınma durumunu okumak için oyuncu kontrolü.")]
    private PlayerController playerController;

    private int current;
    private int cachedMax;
    private float invulnerabilityLeft;
    private Coroutine blinkRoutine;

    public int Current => current;

    /// <summary>Azami can: temel can + Dayanıklılık statı.</summary>
    public int Max
    {
        get
        {
            PlayerProgress progress = PlayerProgress.Instance;
            int bonus = progress != null ? progress.Endurance : 0;
            return Mathf.Max(1, baseMaxHealth + bonus);
        }
    }

    public bool IsAlive => current > 0;

    /// <summary>Şu anda hasar alınamaz mı? (dokunulmazlık ya da kaçınma)</summary>
    public bool IsInvulnerable => invulnerabilityLeft > 0f || (playerController != null && playerController.IsDashing);

    /// <summary>Hasar alındığında tetiklenir.</summary>
    public event Action OnDamaged;

    /// <summary>Can sıfırlandığında tetiklenir.</summary>
    public event Action OnDeath;

    private void Awake()
    {
        if (playerController == null) playerController = GetComponent<PlayerController>();

        cachedMax = Max;
        current = cachedMax;
    }

    private void OnEnable()
    {
        PlayerProgress progress = PlayerProgress.Instance;
        if (progress != null) progress.OnStatsChanged += HandleStatsChanged;
    }

    private void OnDisable()
    {
        PlayerProgress progress = PlayerProgress.Instance;
        if (progress != null) progress.OnStatsChanged -= HandleStatsChanged;
    }

    private void Start()
    {
        // PlayerProgress bizden sonra Awake olmuş olabilir; aboneliği ve canı tazeliyoruz.
        PlayerProgress progress = PlayerProgress.Instance;
        if (progress != null)
        {
            progress.OnStatsChanged -= HandleStatsChanged;
            progress.OnStatsChanged += HandleStatsChanged;
        }

        cachedMax = Max;
        current = cachedMax;
    }

    /// <summary>Dayanıklılık arttıysa azami can büyür ve fark kadar can eklenir.</summary>
    private void HandleStatsChanged()
    {
        int newMax = Max;
        int delta = newMax - cachedMax;

        cachedMax = newMax;

        if (delta > 0) current = Mathf.Min(newMax, current + delta);
        else current = Mathf.Min(current, newMax);
    }

    private void Update()
    {
        if (invulnerabilityLeft > 0f) invulnerabilityLeft -= Time.deltaTime;
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || !IsAlive || IsInvulnerable) return;

        current = Mathf.Max(0, current - amount);
        OnDamaged?.Invoke();

        // Kamera sarsıntısı ve ekran kenarı kırmızı parlaması.
        if (Fx.Instance != null) Fx.Instance.PlayerHurt();

        if (current <= 0)
        {
            StopBlink();
            OnDeath?.Invoke();
            return;
        }

        invulnerabilityLeft = invulnerabilityDuration;
        StopBlink();
        blinkRoutine = StartCoroutine(BlinkRoutine(invulnerabilityDuration));
    }

    /// <summary>Can ekler; azami canı geçmez. Kaç can eklendiğini döner.</summary>
    public int Heal(int amount)
    {
        if (amount <= 0 || !IsAlive) return 0;

        int max = Max;
        int before = current;
        current = Mathf.Min(max, current + amount);
        return current - before;
    }

    /// <summary>Canı doldurur, dokunulmazlığı ve yanıp sönmeyi temizler.</summary>
    public void ResetHealth()
    {
        cachedMax = Max;
        current = cachedMax;
        invulnerabilityLeft = 0f;
        StopBlink();
    }

    private void StopBlink()
    {
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }
        SetVisible(true);
    }

    private IEnumerator BlinkRoutine(float duration)
    {
        float t = 0f;
        bool visible = false;
        float step = Mathf.Max(0.01f, blinkInterval);

        while (t < duration)
        {
            SetVisible(visible);
            visible = !visible;
            yield return new WaitForSeconds(step);
            t += step;
        }

        SetVisible(true);
        blinkRoutine = null;
    }

    private void SetVisible(bool visible)
    {
        if (bodyRenderers == null) return;
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] != null) bodyRenderers[i].enabled = visible;
        }
    }
}
