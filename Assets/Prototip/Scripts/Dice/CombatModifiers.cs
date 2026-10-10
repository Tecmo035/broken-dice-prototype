using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Bir atışın hasar dökümü. HUD'un "son atış" satırı bunu okur.
/// </summary>
public class ShotBreakdown
{
    public int Result;
    public int FaceCount;
    public int Total;
    public bool Crit;

    /// <summary>"Zar 4 × 10 × 1.2 (Hançer) × 2 (Yıkım) = 96" biçiminde tam yazı.</summary>
    public string Text = string.Empty;
}

/// <summary>
/// Bütün hasar, hız ve süre hesabının tek adresi. Zar hiçbir çarpanı kendi
/// hesaplamaz; buradan okur.
///
/// HASAR ZİNCİRİ (sıra önemlidir, Resolve içinde de bu sırada):
///   1. temel hasar
///   2. × sonuç etkeni        (GetResultFactor — ileride formül değişebilir)
///   3. × eşya çarpanları     (Paslı Hançer, Sıradan Kılıç)
///   4. × buff çarpanları     (Yıkım)
///   5. × kritik çarpanı
///   6. düşmana uygula        (sonuç göstergesi düşmana değdiğinde)
///   7. yan etkiler           (Patlama, Zincir, Kor Taşı yanması)
///
/// Yüz sayısı Inspector'dan ayarlanır (4-20). Kritik eşiği, İkinci Şans'ın
/// tetiklendiği sayı ve eşya koşulları sabit sayılara değil yüz sayısına bağlı.
/// </summary>
public class CombatModifiers : MonoBehaviour
{
    /// <summary>Sahnedeki tek örnek. Kullanmadan önce null kontrolü yapılmalı.</summary>
    public static CombatModifiers Instance { get; private set; }

    [Header("Zar")]
    [SerializeField, Range(4, 20), Tooltip("Zarın yüz sayısı. Kritik eşiği ve tüm koşullar buna göre hesaplanır.")]
    private int faceCount = 6;
    [SerializeField, Tooltip("Sonuç etkeniyle çarpılacak temel hasar.")]
    private int baseDamage = 10;
    [SerializeField, Tooltip("Kritik vuruşta hasar bu kadar katlanır.")]
    private int critMultiplier = 2;

    [Header("Buff: Çabuk El")]
    [SerializeField, Tooltip("Dönme süresi çarpanı. 0.6 = %40 kısalır.")]
    private float quickHandRollFactor = 0.6f;

    [Header("Buff: Patlama")]
    [SerializeField, Tooltip("Patlamanın yarıçapı (birim).")]
    private float blastRadius = 3f;
    [SerializeField, Tooltip("Diğer düşmanların aldığı hasar oranı. 0.5 = %50.")]
    private float blastDamageFactor = 0.5f;

    [Header("Buff: Güdüm")]
    [SerializeField, Tooltip("Hedef aranan koninin yarı açısı (derece). Dar olmalı.")]
    private float homingAngle = 32f;
    [SerializeField, Tooltip("Dönüş hızı (derece/saniye). Düşük tutulursa köşeyi dönmez.")]
    private float homingTurnSpeed = 150f;
    [SerializeField, Tooltip("Hedef aranan azami mesafe (birim).")]
    private float homingRange = 14f;

    [Header("Buff: Sekme")]
    [SerializeField, Tooltip("Zarın bir atışta kaç kez sekebileceği.")]
    private int bounceCount = 1;

    [Header("Buff: Zincir")]
    [SerializeField, Tooltip("Zarın sıçrayabileceği azami mesafe (birim).")]
    private float chainRange = 6f;
    [SerializeField, Tooltip("Bir atışta kaç kez sıçrayabilir.")]
    private int chainJumps = 1;

    [Header("Buff: Yıkım")]
    [SerializeField, Tooltip("Yıkım buff'ının hasar çarpanı.")]
    private float ruinMultiplier = 2f;

    [Header("Eşya: Paslı Hançer")]
    [SerializeField, Tooltip("Tüm hasara uygulanan çarpan. 1.2 = +%20.")]
    private float daggerMultiplier = 1.2f;

    [Header("Eşya: Sıradan Kılıç")]
    [SerializeField, Tooltip("Sonuç en yüksek yüzün bir altı olduğunda çarpan. 1.5 = +%50.")]
    private float swordMultiplier = 1.5f;

    [Header("Eşya: Çevik Eldiven")]
    [SerializeField, Tooltip("Gidiş ve dönüş hızına eklenen oran. 0.4 = +%40.")]
    private float gauntletSpeedBonus = 0.4f;

    [Header("Eşya: Kor Taşı")]
    [SerializeField, Tooltip("Yanma başına hasar.")]
    private int burnDamagePerTick = 10;
    [SerializeField, Tooltip("Yanma hasarı aralığı (saniye).")]
    private float burnInterval = 1f;
    [SerializeField, Tooltip("Yanmanın toplam süresi (saniye).")]
    private float burnDuration = 3f;

    // Patlama sırasında EnemyHealth.All değişeceği için geçici kopya.
    private readonly List<EnemyHealth> blastBuffer = new List<EnemyHealth>();
    private readonly StringBuilder breakdownBuilder = new StringBuilder();

    /// <summary>En son çözülen atışın dökümü; HUD gösterir.</summary>
    public ShotBreakdown LastShot { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // --- Zar tanımı ------------------------------------------------------

    /// <summary>Zarın yüz sayısı (4-20 arası çalışır).</summary>
    public int FaceCount => Mathf.Clamp(faceCount, 4, 20);

    /// <summary>En düşük yüz. İkinci Şans bu sayıda tetiklenir.</summary>
    public int LowestFace => 1;

    /// <summary>En yüksek yüz. Kritik eşiğinin tabanı.</summary>
    public int HighestFace => FaceCount;

    /// <summary>En yüksek yüzün bir altı. Kılıç ve Şans Tılsımı bunu kullanır.</summary>
    public int SecondHighestFace => Mathf.Max(LowestFace + 1, FaceCount - 1);

    /// <summary>1 ile yüz sayısı arasında rastgele bir sonuç.</summary>
    public int RollFace()
    {
        return Random.Range(1, FaceCount + 1);
    }

    /// <summary>
    /// Sonuç etkeni. Şimdilik gelen sayının kendisi; formül ileride burada
    /// değişecek (örnek: karesi, eğri, basamaklı tablo).
    /// </summary>
    public float GetResultFactor(int result, int faces)
    {
        return result;
    }

    // --- Buff okumaları --------------------------------------------------

    private bool HasBuff(BuffId id)
    {
        RunState run = RunState.Instance;
        return run != null && run.Has(id);
    }

    public bool HasBounce => HasBuff(BuffId.Bounce);
    public bool HasGhost => HasBuff(BuffId.Ghost);
    public bool HasBlast => HasBuff(BuffId.Blast);
    public bool HasHoming => HasBuff(BuffId.Homing);
    public bool HasSecondChance => HasBuff(BuffId.SecondChance);
    public bool HasChain => HasBuff(BuffId.Chain);
    public bool HasRuin => HasBuff(BuffId.Ruin);

    public float HomingAngle => homingAngle;
    public float HomingTurnSpeed => homingTurnSpeed;
    public float HomingRange => homingRange;
    public float ChainRange => chainRange;

    /// <summary>Zarın bu atışta kaç kez sekebileceği.</summary>
    public int BounceAllowance => HasBounce ? Mathf.Max(1, bounceCount) : 0;

    /// <summary>Zarın bu atışta kaç kez sıçrayabileceği.</summary>
    public int ChainAllowance => HasChain ? Mathf.Max(1, chainJumps) : 0;

    // --- Eşya okumaları --------------------------------------------------

    private bool HasItem(ItemId id)
    {
        PlayerLoadout loadout = PlayerLoadout.Instance;
        return loadout != null && loadout.Has(id);
    }

    /// <summary>Çevik Eldiven: zar gidiş/dönüş hızı çarpanı.</summary>
    public float SpeedMultiplier => HasItem(ItemId.SwiftGauntlet) ? 1f + gauntletSpeedBonus : 1f;

    /// <summary>Şans Tılsımı: kritik eşiğini bir yüz aşağı indirir.</summary>
    public int CritThresholdReduction => HasItem(ItemId.LuckCharm) ? 1 : 0;

    /// <summary>Kor Taşı kuşanılmış mı?</summary>
    public bool BurnEnabled => HasItem(ItemId.EmberStone);

    // --- Hesaplar --------------------------------------------------------

    /// <summary>Dönme süresi çarpanı: Çabuk El.</summary>
    public float RollDurationMultiplier
    {
        get
        {
            float factor = 1f;
            if (HasBuff(BuffId.QuickHand)) factor *= Mathf.Clamp(quickHandRollFactor, 0.1f, 1f);
            return factor;
        }
    }

    /// <summary>Kritik eşiği: en yüksek yüz, Şans Tılsımı varsa bir altı.</summary>
    public int CritThreshold => Mathf.Max(LowestFace + 1, HighestFace - CritThresholdReduction);

    public bool IsCrit(int result)
    {
        return result >= CritThreshold;
    }

    /// <summary>
    /// Hasar zinciri. Sıra sınıf açıklamasındaki gibidir ve her adım
    /// HUD'da gösterilecek döküme yazılır.
    /// </summary>
    public ShotBreakdown Resolve(int result)
    {
        int faces = FaceCount;
        bool crit = IsCrit(result);

        ShotBreakdown shot = new ShotBreakdown
        {
            Result = result,
            FaceCount = faces,
            Crit = crit
        };

        breakdownBuilder.Length = 0;

        // 1 + 2. temel hasar × sonuç etkeni
        float factor = GetResultFactor(result, faces);
        float damage = baseDamage * factor;

        breakdownBuilder.Append("Zar ").Append(result).Append(" × ").Append(baseDamage);

        // 3. eşya çarpanları
        if (HasItem(ItemId.RustyDagger))
        {
            damage *= daggerMultiplier;
            AppendFactor(daggerMultiplier, "Hançer");
        }

        if (HasItem(ItemId.PlainSword) && result == SecondHighestFace)
        {
            damage *= swordMultiplier;
            AppendFactor(swordMultiplier, "Kılıç");
        }

        // 4. buff çarpanları
        if (HasRuin)
        {
            damage *= ruinMultiplier;
            AppendFactor(ruinMultiplier, "Yıkım");
        }

        // 5. kritik
        if (crit)
        {
            damage *= Mathf.Max(1, critMultiplier);
            AppendFactor(critMultiplier, "KRİTİK");
        }

        shot.Total = Mathf.Max(1, Mathf.RoundToInt(damage));

        breakdownBuilder.Append(" = ").Append(shot.Total);
        shot.Text = breakdownBuilder.ToString();

        LastShot = shot;
        return shot;
    }

    private void AppendFactor(float value, string label)
    {
        breakdownBuilder.Append(" × ").Append(value.ToString("0.##")).Append(" (").Append(label).Append(")");
    }

    /// <summary>
    /// 7. adım: hasar uygulandıktan SONRAKİ yan etkiler.
    /// Patlama buff'ı ve Kor Taşı yanması burada işlenir.
    /// Zincir sıçraması zarın kendi işi olduğu için orada ele alınır.
    /// </summary>
    public void ApplySideEffects(EnemyHealth target, int damage, Vector3 at)
    {
        if (BurnEnabled && target != null && target.IsAlive)
        {
            target.ApplyBurn(burnDamagePerTick, burnInterval, burnDuration);
        }

        if (HasBlast) Blast(at, target, damage);
    }

    /// <summary>Yarıçap içindeki DİĞER düşmanlara vuruş hasarının bir oranı.</summary>
    private void Blast(Vector3 position, EnemyHealth exclude, int sourceDamage)
    {
        int damage = Mathf.Max(1, Mathf.RoundToInt(sourceDamage * Mathf.Clamp01(blastDamageFactor)));

        if (Fx.Instance != null) Fx.Instance.BlastBurst(position, blastRadius);

        // Hasar verirken liste değişeceği için önce kopyalanıyor.
        blastBuffer.Clear();
        for (int i = 0; i < EnemyHealth.All.Count; i++)
        {
            EnemyHealth candidate = EnemyHealth.All[i];
            if (candidate == null || candidate == exclude || !candidate.IsAlive) continue;

            Vector3 d = candidate.transform.position - position;
            d.y = 0f;
            if (d.sqrMagnitude > blastRadius * blastRadius) continue;

            blastBuffer.Add(candidate);
        }

        for (int i = 0; i < blastBuffer.Count; i++)
        {
            EnemyHealth victim = blastBuffer[i];
            if (victim == null || !victim.IsAlive) continue;

            if (Fx.Instance != null) Fx.Instance.ShowSideDamage(victim.HeadPoint, damage, SideDamageKind.Blast);
            victim.TakeDamage(damage);
        }

        blastBuffer.Clear();
    }

    /// <summary>Zincir buff'ı: menzil içindeki en yakın BAŞKA düşmanı bulur.</summary>
    public EnemyHealth FindChainTarget(Vector3 from, EnemyHealth exclude)
    {
        EnemyHealth best = null;
        float bestSqr = chainRange * chainRange;

        for (int i = 0; i < EnemyHealth.All.Count; i++)
        {
            EnemyHealth candidate = EnemyHealth.All[i];
            if (candidate == null || candidate == exclude || !candidate.IsAlive) continue;

            Vector3 d = candidate.transform.position - from;
            d.y = 0f;

            float sqr = d.sqrMagnitude;
            if (sqr > bestSqr) continue;

            bestSqr = sqr;
            best = candidate;
        }

        return best;
    }
}
