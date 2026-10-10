using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Düşman canı ve görsel geri bildirimi. Hasar alınca beyaz yanıp söner,
/// saldırıya hazırlanırken uyarı rengine döner, ölünce parçacık efektiyle yok olur.
///
/// Boss için iki ek durum var: öfke (kalıcı renk değişimi) ve sersemleme
/// (duvara çarpınca, zar atma fırsatı).
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("Can")]
    [SerializeField, Tooltip("Azami can.")]
    private int maxHealth = 80;

    [Header("Konum")]
    [SerializeField, Tooltip("Zarın oturacağı baş yüksekliği (yerel birim).")]
    private float headHeight = 2.3f;

    [Header("Görsel")]
    [SerializeField, Tooltip("Rengi değiştirilecek görseller.")]
    private Renderer[] bodyRenderers;
    [SerializeField, Tooltip("Normal gövde rengi.")]
    private Color baseColor = new Color(0.62f, 0.12f, 0.12f);
    [SerializeField, Tooltip("Saldırıya hazırlanırken alınan uyarı rengi.")]
    private Color telegraphColor = new Color(1f, 0.75f, 0.12f);
    [SerializeField, Tooltip("Hasar alınca kısa süre alınan renk.")]
    private Color blinkColor = Color.white;
    [SerializeField, Tooltip("Öfkeliyken (boss) gövde rengi.")]
    private Color enragedColor = new Color(1f, 0.18f, 0.1f);
    [SerializeField, Tooltip("Sersemlemişken (boss) gövde rengi.")]
    private Color stunnedColor = new Color(0.45f, 0.5f, 0.78f);
    [SerializeField, Tooltip("Hasar yanıp sönme süresi (saniye).")]
    private float blinkDuration = 0.12f;
    [SerializeField, Tooltip("Emisyon gücü; düşmanlar hafif parlasın diye.")]
    private float emissionStrength = 0.18f;

    [Header("Boss")]
    [SerializeField, Tooltip("Bu düşman boss mu? Ölümünde büyük patlama ve zaman yavaşlaması olur.")]
    private bool isBoss;
    [SerializeField, Tooltip("Boss ölürken zaman ölçeği.")]
    private float bossDeathTimeScale = 0.15f;
    [SerializeField, Tooltip("Boss ölürken zaman yavaşlaması süresi (gerçek saniye).")]
    private float bossDeathSlowTime = 0.7f;

    [Header("Yanma (Kor Taşı)")]
    [SerializeField, Tooltip("Alev efektinin gövde üzerindeki yüksekliği (birim).")]
    private float flameHeight = 1.1f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    /// <summary>Sahnedeki yaşayan tüm düşmanlar. Patlama, Zincir, Güdüm ve HUD bunu kullanır.</summary>
    public static readonly List<EnemyHealth> All = new List<EnemyHealth>();

    private Material[] materials;
    private Coroutine blinkRoutine;
    private Coroutine burnRoutine;
    private GameObject flame;
    private int current;
    private bool dead;
    private bool blinking;
    private bool telegraphing;
    private bool enraged;
    private bool stunned;

    public int Current => current;
    public int Max => maxHealth;
    public bool IsAlive => !dead;
    public Color BaseColor => baseColor;
    public bool IsBoss => isBoss;
    public bool IsStunned => stunned;

    /// <summary>Şu anda yanıyor mu?</summary>
    public bool IsBurning => burnRoutine != null;

    /// <summary>Zarın üzerinde döneceği nokta.</summary>
    public Vector3 HeadPoint => transform.position + Vector3.up * headHeight;

    /// <summary>Düşman öldüğünde tetiklenir. Oda yöneticisi bunu dinler.</summary>
    public event Action<EnemyHealth> OnDeath;

    private void Awake()
    {
        current = maxHealth;
        CacheMaterials();
        ApplyColor();
    }

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void CacheMaterials()
    {
        List<Material> list = new List<Material>();

        if (bodyRenderers == null || bodyRenderers.Length == 0) bodyRenderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            Renderer r = bodyRenderers[i];
            if (r == null) continue;

            // renderer.materials her düşman için ayrı örnek üretir; renk değişimi diğerlerini etkilemez.
            Material[] mats = r.materials;
            for (int j = 0; j < mats.Length; j++)
            {
                if (mats[j] != null) list.Add(mats[j]);
            }
        }

        materials = list.ToArray();
    }

    public void TakeDamage(int amount)
    {
        if (dead || amount <= 0) return;

        current -= amount;

        if (current <= 0)
        {
            Die();
            return;
        }

        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        blinkRoutine = StartCoroutine(BlinkRoutine());
    }

    /// <summary>
    /// Kor Taşı etkisi: düşmanı belirli süre boyunca düzenli aralıklarla yakar
    /// ve üzerine alev efekti bırakır. Tekrar uygulanırsa süre baştan başlar.
    /// </summary>
    public void ApplyBurn(int damagePerTick, float interval, float duration)
    {
        if (dead || damagePerTick <= 0 || duration <= 0f) return;

        if (burnRoutine != null) StopCoroutine(burnRoutine);
        burnRoutine = StartCoroutine(BurnRoutine(damagePerTick, Mathf.Max(0.1f, interval), duration));
    }

    private IEnumerator BurnRoutine(int damagePerTick, float interval, float duration)
    {
        if (flame == null && Fx.Instance != null)
        {
            flame = Fx.Instance.SpawnFlame(transform, Vector3.up * flameHeight);
        }

        float elapsed = 0f;
        while (elapsed < duration && !dead)
        {
            yield return new WaitForSeconds(interval);
            elapsed += interval;

            if (dead) break;

            // Yanma hasarı ayrı renkte, küçük sayılarla gösterilir.
            if (Fx.Instance != null) Fx.Instance.ShowSideDamage(HeadPoint, damagePerTick, SideDamageKind.Burn);
            TakeDamage(damagePerTick);
        }

        StopFlame();
        burnRoutine = null;
    }

    private void StopFlame()
    {
        if (flame == null) return;
        Destroy(flame);
        flame = null;
    }

    /// <summary>Saldırı hazırlığı uyarısını açar/kapatır.</summary>
    public void SetTelegraph(bool on)
    {
        if (dead) return;
        telegraphing = on;
        ApplyColor();
    }

    /// <summary>Boss öfkelendi: kalıcı renk değişimi.</summary>
    public void SetEnraged(bool on)
    {
        if (dead) return;
        enraged = on;
        ApplyColor();
    }

    /// <summary>Boss duvara çarpıp sersemledi.</summary>
    public void SetStunned(bool on)
    {
        if (dead) return;
        stunned = on;
        ApplyColor();
    }

    private IEnumerator BlinkRoutine()
    {
        blinking = true;
        ApplyColor();
        yield return new WaitForSeconds(Mathf.Max(0.01f, blinkDuration));
        blinking = false;
        ApplyColor();
        blinkRoutine = null;
    }

    private void Die()
    {
        dead = true;
        current = 0;

        StopFlame();

        if (Fx.Instance != null)
        {
            Vector3 burstAt = transform.position + Vector3.up * (headHeight * 0.5f);

            if (isBoss)
            {
                Fx.Instance.BossDeath(burstAt, baseColor);
                Fx.Instance.HitStop(bossDeathTimeScale, bossDeathSlowTime);
            }
            else
            {
                Fx.Instance.DeathBurst(burstAt, baseColor);
            }
        }

        OnDeath?.Invoke(this);
        Destroy(gameObject);
    }

    private void ApplyColor()
    {
        if (materials == null) return;

        // Öncelik sırası: yanıp sönme > uyarı > sersemleme > öfke > normal.
        Color c = baseColor;
        if (enraged) c = enragedColor;
        if (stunned) c = stunnedColor;
        if (telegraphing) c = telegraphColor;
        if (blinking) c = blinkColor;

        for (int i = 0; i < materials.Length; i++)
        {
            Material m = materials[i];
            if (m == null) continue;

            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(LegacyColorId)) m.SetColor(LegacyColorId, c);
            if (m.HasProperty(EmissionColorId)) m.SetColor(EmissionColorId, c * emissionStrength);
        }
    }
}
