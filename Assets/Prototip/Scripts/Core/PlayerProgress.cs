using UnityEngine;

/// <summary>Dört kalıcı stat.</summary>
public enum StatType
{
    Strength,
    Agility,
    Intellect,
    Endurance
}

/// <summary>
/// Kalıcı ilerleme: dört stat ve harcanmamış ödül puanı PlayerPrefs'te tutulur.
///
/// Güç, Çeviklik ve Zekâ zarı doğrudan etkilemez; yalnızca hangi eşyanın
/// kuşanılabileceğini ve hangi buff'ın alınabileceğini belirler.
/// Dayanıklılık ise doğrudan etkilidir: azami can = 5 + Dayanıklılık.
/// </summary>
public class PlayerProgress : MonoBehaviour
{
    /// <summary>Sahnedeki tek örnek. Kullanmadan önce null kontrolü yapılmalı.</summary>
    public static PlayerProgress Instance { get; private set; }

    [Header("Başlangıç Değerleri")]
    [SerializeField, Tooltip("Kayıt yokken başlangıç Güç değeri.")]
    private int startStrength = 3;
    [SerializeField, Tooltip("Kayıt yokken başlangıç Çeviklik değeri.")]
    private int startAgility = 2;
    [SerializeField, Tooltip("Kayıt yokken başlangıç Zekâ değeri.")]
    private int startIntellect = 2;
    [SerializeField, Tooltip("Kayıt yokken başlangıç Dayanıklılık değeri.")]
    private int startEndurance = 0;

    private const string Prefix = "ZarPrototip_";
    private const string KeyStrength = Prefix + "Guc";
    private const string KeyAgility = Prefix + "Ceviklik";
    private const string KeyIntellect = Prefix + "Zeka";
    private const string KeyEndurance = Prefix + "Dayaniklilik";
    private const string KeyReward = Prefix + "Odul";

    public int Strength { get; private set; }
    public int Agility { get; private set; }
    public int Intellect { get; private set; }
    public int Endurance { get; private set; }

    /// <summary>Harcanmamış ödül puanı.</summary>
    public int RewardPoints { get; private set; }

    /// <summary>Statlar değişince tetiklenir (azami can ve HUD bunu dinler).</summary>
    public event System.Action OnStatsChanged;

    private void Awake()
    {
        Instance = this;
        Load();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public int GetStat(StatType stat)
    {
        switch (stat)
        {
            case StatType.Strength: return Strength;
            case StatType.Agility: return Agility;
            case StatType.Intellect: return Intellect;
            default: return Endurance;
        }
    }

    /// <summary>Verilen stat şartı karşılanıyor mu? (eşya ve buff kilitleri bunu kullanır)</summary>
    public bool Meets(StatType stat, int required)
    {
        return GetStat(stat) >= required;
    }

    /// <summary>Ödül puanı varsa ilgili statı kalıcı olarak 1 arttırır.</summary>
    public bool SpendPoint(StatType stat)
    {
        if (RewardPoints <= 0) return false;

        RewardPoints--;

        switch (stat)
        {
            case StatType.Strength: Strength++; break;
            case StatType.Agility: Agility++; break;
            case StatType.Intellect: Intellect++; break;
            default: Endurance++; break;
        }

        Save();
        OnStatsChanged?.Invoke();
        return true;
    }

    public void AddReward(int amount)
    {
        if (amount <= 0) return;
        RewardPoints += amount;
        Save();
    }

    /// <summary>Statları ve ödül puanını başlangıç değerlerine döndürür (F9).</summary>
    public void ResetAll()
    {
        Strength = startStrength;
        Agility = startAgility;
        Intellect = startIntellect;
        Endurance = startEndurance;
        RewardPoints = 0;
        Save();
        OnStatsChanged?.Invoke();
    }

    private void Load()
    {
        Strength = PlayerPrefs.GetInt(KeyStrength, startStrength);
        Agility = PlayerPrefs.GetInt(KeyAgility, startAgility);
        Intellect = PlayerPrefs.GetInt(KeyIntellect, startIntellect);
        Endurance = PlayerPrefs.GetInt(KeyEndurance, startEndurance);
        RewardPoints = PlayerPrefs.GetInt(KeyReward, 0);
    }

    private void Save()
    {
        PlayerPrefs.SetInt(KeyStrength, Strength);
        PlayerPrefs.SetInt(KeyAgility, Agility);
        PlayerPrefs.SetInt(KeyIntellect, Intellect);
        PlayerPrefs.SetInt(KeyEndurance, Endurance);
        PlayerPrefs.SetInt(KeyReward, RewardPoints);
        PlayerPrefs.Save();
    }

    /// <summary>Statın Türkçe adı (HUD, sunak ve kilit mesajları için).</summary>
    public static string StatName(StatType stat)
    {
        switch (stat)
        {
            case StatType.Strength: return "Güç";
            case StatType.Agility: return "Çeviklik";
            case StatType.Intellect: return "Zekâ";
            default: return "Dayanıklılık";
        }
    }

    /// <summary>"Güç 10 gerekli (sende 3)" biçiminde kilit açıklaması.</summary>
    public static string RequirementText(StatType stat, int required)
    {
        int have = Instance != null ? Instance.GetStat(stat) : 0;
        return StatName(stat) + " " + required + " gerekli (sende " + have + ")";
    }
}
