using System.Collections.Generic;
using UnityEngine;

/// <summary>Sandıklardan çıkan, koşuya özel zar buff'ları.</summary>
public enum BuffId
{
    QuickHand,
    Homing,
    Blast,
    Bounce,
    SecondChance,
    Ghost,
    Chain,
    Ruin
}

/// <summary>
/// Bir buff'ın tanımı. Her buff'ın TEK bir stat şartı vardır: bir stat alanı ve
/// bir seviye alanı. Veri yapısı iki stata bağlanmayı engeller.
/// Dayanıklılık buff şartı olarak kullanılmaz.
/// </summary>
public class BuffDefinition
{
    public readonly BuffId Id;
    public readonly string Name;
    public readonly StatType RequiredStat;
    public readonly int RequiredLevel;
    public readonly string Effect;
    public readonly Color Color;

    public BuffDefinition(BuffId id, string name, StatType stat, int level, string effect, Color color)
    {
        Id = id;
        Name = name;
        RequiredStat = stat;
        RequiredLevel = level;
        Effect = effect;
        Color = color;
    }

    /// <summary>"Güç 10" biçiminde kısa şart yazısı.</summary>
    public string Requirement => PlayerProgress.StatName(RequiredStat) + " " + RequiredLevel;

    /// <summary>Oyuncunun statı yeterli mi?</summary>
    public bool Unlocked
    {
        get
        {
            PlayerProgress progress = PlayerProgress.Instance;
            return progress != null && progress.Meets(RequiredStat, RequiredLevel);
        }
    }

    /// <summary>"Güç 10 gerekli (sende 3)" biçiminde kilit açıklaması.</summary>
    public string LockText => PlayerProgress.RequirementText(RequiredStat, RequiredLevel);
}

/// <summary>
/// Buff havuzu. Buff'lar ORTAKTIR: alınan buff bütün zarlara işler.
/// Aynı buff ikinci kez alınamaz, şartı tutmayan buff hiç alınamaz.
/// </summary>
public static class BuffCatalog
{
    public static readonly BuffDefinition[] All =
    {
        new BuffDefinition(
            BuffId.QuickHand, "Çabuk El",
            StatType.Agility, 2,
            "Dönme süresi %40 kısalır",
            new Color(1f, 0.9f, 0.4f)),

        new BuffDefinition(
            BuffId.Homing, "Güdüm",
            StatType.Intellect, 2,
            "Zar uçarken en yakın düşmana hafifçe yönelir",
            new Color(0.5f, 1f, 0.7f)),

        new BuffDefinition(
            BuffId.Blast, "Patlama",
            StatType.Strength, 3,
            "Sonuçta 3 birimdeki diğer düşmanlar hasarın %50'sini alır",
            new Color(1f, 0.5f, 0.2f)),

        new BuffDefinition(
            BuffId.Bounce, "Sekme",
            StatType.Agility, 4,
            "Zar duvardan bir kez sekip yoluna devam eder",
            new Color(0.55f, 0.85f, 1f)),

        new BuffDefinition(
            BuffId.SecondChance, "İkinci Şans",
            StatType.Intellect, 4,
            "En düşük yüz gelirse zar bir kez daha döner",
            new Color(0.85f, 0.6f, 1f)),

        new BuffDefinition(
            BuffId.Ghost, "Hayalet Zar",
            StatType.Intellect, 6,
            "Zar duvarların içinden geçer",
            new Color(0.7f, 0.75f, 1f)),

        new BuffDefinition(
            BuffId.Chain, "Zincir",
            StatType.Agility, 8,
            "Hasardan sonra zar 6 birimdeki başka düşmana sıçrar",
            new Color(0.4f, 0.95f, 0.95f)),

        new BuffDefinition(
            BuffId.Ruin, "Yıkım",
            StatType.Strength, 10,
            "Tüm zar hasarı ×2",
            new Color(1f, 0.3f, 0.35f))
    };

    public static BuffDefinition Get(BuffId id)
    {
        for (int i = 0; i < All.Length; i++)
        {
            if (All[i].Id == id) return All[i];
        }
        return All[0];
    }

    /// <summary>
    /// Sekme ve Hayalet Zar birlikte alınamaz: biri duvardan sektirir, diğeri
    /// duvarı hiç görmez. Birine sahipken sandık diğerini teklif etmemeli.
    /// </summary>
    public static bool ConflictsWith(BuffId candidate, IReadOnlyList<BuffId> owned)
    {
        if (owned == null) return false;

        BuffId rakip;
        if (candidate == BuffId.Bounce) rakip = BuffId.Ghost;
        else if (candidate == BuffId.Ghost) rakip = BuffId.Bounce;
        else return false;

        for (int i = 0; i < owned.Count; i++)
        {
            if (owned[i] == rakip) return true;
        }

        return false;
    }
}
