using UnityEngine;

/// <summary>Silahlıktaki kalıcı eşyalar.</summary>
public enum ItemId
{
    RustyDagger,
    PlainSword,
    SwiftGauntlet,
    SpareDiceBag,
    EmberStone,
    LuckCharm
}

/// <summary>
/// Bir eşyanın tanımı. Her eşyanın TEK bir stat şartı vardır:
/// bir stat alanı ve bir seviye alanı. Veri yapısı iki stata bağlanmayı engeller.
/// </summary>
public class ItemDefinition
{
    public readonly ItemId Id;
    public readonly string Name;
    public readonly StatType RequiredStat;
    public readonly int RequiredLevel;
    public readonly string Effect;
    public readonly Color Color;

    public ItemDefinition(ItemId id, string name, StatType stat, int level, string effect, Color color)
    {
        Id = id;
        Name = name;
        RequiredStat = stat;
        RequiredLevel = level;
        Effect = effect;
        Color = color;
    }

    /// <summary>"Güç 5" biçiminde kısa şart yazısı.</summary>
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

    /// <summary>"Güç 5 gerekli (sende 3)" biçiminde kilit açıklaması.</summary>
    public string LockText => PlayerProgress.RequirementText(RequiredStat, RequiredLevel);
}

/// <summary>
/// Eşya kataloğu. Prototip olduğu için liste kodda sabit; ScriptableObject yok.
/// Eşyalar zindanda bulunmaz, lobideki Silahlık'ta kuşanılır ve kalıcıdır.
/// </summary>
public static class ItemCatalog
{
    public static readonly ItemDefinition[] All =
    {
        new ItemDefinition(
            ItemId.RustyDagger, "Paslı Hançer",
            StatType.Strength, 3,
            "Tüm zar hasarı +%20",
            new Color(1f, 0.45f, 0.32f)),

        new ItemDefinition(
            ItemId.PlainSword, "Sıradan Kılıç",
            StatType.Strength, 5,
            "Sonuç, en yüksek yüzün bir altıysa +%50 hasar",
            new Color(1f, 0.62f, 0.3f)),

        new ItemDefinition(
            ItemId.SwiftGauntlet, "Çevik Eldiven",
            StatType.Agility, 3,
            "Zar gidiş ve dönüş hızı +%40",
            new Color(0.45f, 1f, 0.6f)),

        new ItemDefinition(
            ItemId.SpareDiceBag, "Yedek Zar Kesesi",
            StatType.Agility, 4,
            "Zar sayısı +1",
            new Color(0.5f, 0.9f, 1f)),

        new ItemDefinition(
            ItemId.EmberStone, "Kor Taşı",
            StatType.Intellect, 3,
            "Vurulan düşman 3 sn boyunca saniyede 10 yanar",
            new Color(1f, 0.58f, 0.15f)),

        new ItemDefinition(
            ItemId.LuckCharm, "Şans Tılsımı",
            StatType.Intellect, 4,
            "En yüksek yüzün bir altı da kritik sayılır",
            new Color(0.82f, 0.6f, 1f))
    };

    public static ItemDefinition Get(ItemId id)
    {
        for (int i = 0; i < All.Length; i++)
        {
            if (All[i].Id == id) return All[i];
        }
        return All[0];
    }
}
