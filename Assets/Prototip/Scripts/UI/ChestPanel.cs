using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Sandıktan çıkan tek bir seçenek.</summary>
public class ChestOffer
{
    public enum Kind
    {
        Buff,
        ExtraDice,
        Heal
    }

    public readonly Kind Type;
    public readonly BuffId Buff;

    /// <summary>Stat şartı tutmuyorsa kart kilitli olur ve seçilemez.</summary>
    public readonly bool Locked;

    private ChestOffer(Kind type, BuffId buff, bool locked)
    {
        Type = type;
        Buff = buff;
        Locked = locked;
    }

    public static ChestOffer FromBuff(BuffId buff, bool locked)
    {
        return new ChestOffer(Kind.Buff, buff, locked);
    }

    public static ChestOffer ExtraDice()
    {
        return new ChestOffer(Kind.ExtraDice, BuffId.Bounce, false);
    }

    public static ChestOffer Heal()
    {
        return new ChestOffer(Kind.Heal, BuffId.Bounce, false);
    }

    public string Title
    {
        get
        {
            switch (Type)
            {
                case Kind.ExtraDice: return "Ek Zar";
                case Kind.Heal: return "Can Dolgusu";
                default: return BuffCatalog.Get(Buff).Name;
            }
        }
    }

    public string Effect
    {
        get
        {
            switch (Type)
            {
                case Kind.ExtraDice: return "Zar sayın 1 artar (slot tutmaz)";
                case Kind.Heal: return "1 can geri kazanırsın";
                default: return BuffCatalog.Get(Buff).Effect;
            }
        }
    }

    /// <summary>Kilitli kartta gösterilen neden: "Güç 10 gerekli (sende 3)".</summary>
    public string LockText => Type == Kind.Buff ? BuffCatalog.Get(Buff).LockText : string.Empty;

    /// <summary>Kilitli olmayan kartta gösterilen şart: "Güç 3".</summary>
    public string Requirement => Type == Kind.Buff ? BuffCatalog.Get(Buff).Requirement : string.Empty;

    public Color Color
    {
        get
        {
            switch (Type)
            {
                case Kind.ExtraDice: return new Color(1f, 0.88f, 0.45f);
                case Kind.Heal: return new Color(0.95f, 0.35f, 0.4f);
                default: return BuffCatalog.Get(Buff).Color;
            }
        }
    }
}

/// <summary>
/// Sandık seçim paneli (OnGUI). Açılınca oyun durur, üç seçenek çıkar;
/// fareyle tıklanabilir ya da 1-2-3 tuşlarıyla seçilebilir.
///
/// Teklif kuralı: öncelik oyuncunun şartını karşıladığı buff'lardadır.
/// En fazla 1 kart kilitli olabilir ve kilitli kart seçilemez. Her sandıkta
/// en az 2 seçilebilir seçenek olur; yeterli buff yoksa boşluk Ek Zar ile,
/// o da mümkün değilse Can Dolgusu ile tamamlanır.
///
/// Duraklatma GamePause üzerinden yapılır, böylece hit-stop timeScale'i bozmaz.
/// </summary>
public class ChestPanel : MonoBehaviour
{
    /// <summary>Sahnedeki tek örnek. Kullanmadan önce null kontrolü yapılmalı.</summary>
    public static ChestPanel Instance { get; private set; }

    [Header("Teklif")]
    [SerializeField, Tooltip("Sandıkta kaç seçenek gösterilsin.")]
    private int offerCount = 3;
    [SerializeField, Tooltip("En fazla kaç kart kilitli olabilir.")]
    private int maxLockedOffers = 1;
    [SerializeField, Tooltip("Her sandıkta en az kaç seçilebilir seçenek olsun.")]
    private int minUnlockedOffers = 2;
    [SerializeField, Tooltip("Can Dolgusu seçeneğinin verdiği can.")]
    private int healAmount = 1;

    [Header("Görünüm")]
    [SerializeField, Tooltip("Bir seçenek kartının genişliği (piksel).")]
    private float cardWidth = 265f;
    [SerializeField, Tooltip("Bir seçenek kartının yüksekliği (piksel).")]
    private float cardHeight = 205f;
    [SerializeField, Tooltip("Kartlar arası boşluk (piksel).")]
    private float cardGap = 22f;

    private readonly List<ChestOffer> offers = new List<ChestOffer>();
    private TreasureChest source;

    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle headerStyle;
    private GUIStyle effectStyle;
    private GUIStyle keyStyle;
    private GUIStyle lockStyle;

    public bool IsOpen => source != null;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // Panel açıkken sahne değişirse oyun donmuş kalmasın.
        if (IsOpen) GamePause.ForceResume();
    }

    /// <summary>
    /// Paneli açar. Teklif edilecek hiçbir şey yoksa false döner.
    /// </summary>
    public bool Open(TreasureChest chest)
    {
        if (IsOpen) return false;

        BuildOffers();
        if (offers.Count == 0) return false;

        source = chest;
        GamePause.Set(true);
        return true;
    }

    private void BuildOffers()
    {
        offers.Clear();

        RunState run = RunState.Instance;
        DiceManager manager = DiceManager.Instance;

        List<ChestOffer> unlocked = new List<ChestOffer>();
        List<ChestOffer> locked = new List<ChestOffer>();

        if (run != null)
        {
            for (int i = 0; i < BuffCatalog.All.Length; i++)
            {
                BuffDefinition def = BuffCatalog.All[i];

                // Slot dolu, zaten alınmış ya da çakışan buff hiç teklif edilmez.
                if (run.SlotsFull) break;
                if (run.Has(def.Id)) continue;
                if (BuffCatalog.ConflictsWith(def.Id, run.Buffs)) continue;

                if (def.Unlocked) unlocked.Add(ChestOffer.FromBuff(def.Id, false));
                else locked.Add(ChestOffer.FromBuff(def.Id, true));
            }
        }

        Shuffle(unlocked);
        Shuffle(locked);

        int total = Mathf.Max(1, offerCount);
        int wantUnlocked = Mathf.Max(1, minUnlockedOffers);

        // 1) Şartı tutan buff'lar önce gelir.
        for (int i = 0; i < unlocked.Count && offers.Count < total; i++) offers.Add(unlocked[i]);

        // 2) Seçilebilir sayı yetmiyorsa Ek Zar, sonra Can Dolgusu ile tamamlanır.
        while (offers.Count < wantUnlocked)
        {
            if (manager != null && manager.CanAddDice && !HasKind(ChestOffer.Kind.ExtraDice))
            {
                offers.Add(ChestOffer.ExtraDice());
                continue;
            }

            if (!HasKind(ChestOffer.Kind.Heal))
            {
                offers.Add(ChestOffer.Heal());
                continue;
            }

            break;
        }

        // 3) Kalan yerlere en fazla maxLockedOffers kadar kilitli kart.
        int lockedAdded = 0;
        for (int i = 0; i < locked.Count && offers.Count < total && lockedAdded < Mathf.Max(0, maxLockedOffers); i++)
        {
            offers.Add(locked[i]);
            lockedAdded++;
        }

        // 4) Hâlâ yer varsa Ek Zar / Can Dolgusu ile doldur.
        while (offers.Count < total)
        {
            if (manager != null && manager.CanAddDice && !HasKind(ChestOffer.Kind.ExtraDice))
            {
                offers.Add(ChestOffer.ExtraDice());
                continue;
            }

            if (!HasKind(ChestOffer.Kind.Heal))
            {
                offers.Add(ChestOffer.Heal());
                continue;
            }

            break;
        }

        Shuffle(offers);
    }

    private bool HasKind(ChestOffer.Kind kind)
    {
        for (int i = 0; i < offers.Count; i++)
        {
            if (offers[i].Type == kind) return true;
        }
        return false;
    }

    private static void Shuffle(List<ChestOffer> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void Update()
    {
        if (!IsOpen) return;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Choose(0);
        else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Choose(1);
        else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Choose(2);
    }

    private void Choose(int index)
    {
        if (!IsOpen || index < 0 || index >= offers.Count) return;

        ChestOffer offer = offers[index];

        // Kilitli kart seçilemez: ne tıklama ne tuş çalışır.
        if (offer.Locked)
        {
            if (PrototipHud.Instance != null) PrototipHud.Instance.ShowMessage(offer.LockText, 2f);
            return;
        }

        string message;

        switch (offer.Type)
        {
            case ChestOffer.Kind.ExtraDice:
            {
                RunState run = RunState.Instance;
                DiceManager manager = DiceManager.Instance;

                if (run != null) run.AddBonusDice(1);
                if (manager != null) manager.Rebuild();

                int total = manager != null ? manager.TotalDice : 0;
                message = "Ek Zar! Artık " + total + " zarın var.";
                break;
            }

            case ChestOffer.Kind.Heal:
            {
                PlayerHealth health = FindFirstObjectByType<PlayerHealth>();
                int healed = health != null ? health.Heal(Mathf.Max(1, healAmount)) : 0;
                message = healed > 0 ? "+" + healed + " can" : "Canın zaten dolu";
                break;
            }

            default:
            {
                RunState run = RunState.Instance;
                if (run != null) run.AddBuff(offer.Buff);

                BuffDefinition def = BuffCatalog.Get(offer.Buff);
                message = def.Name + " — " + def.Effect;
                break;
            }
        }

        TreasureChest chest = source;
        source = null;
        offers.Clear();

        GamePause.Set(false);

        if (chest != null) chest.Consume();
        if (PrototipHud.Instance != null) PrototipHud.Instance.ShowMessage(message, 2.4f);
    }

    private void OnGUI()
    {
        if (!IsOpen) return;

        BuildStyles();

        // Arka planı karart.
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.74f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;

        float centerY = Screen.height * 0.5f;

        GUI.Label(new Rect(0f, centerY - cardHeight * 0.5f - 88f, Screen.width, 44f), "SANDIK", titleStyle);
        GUI.Label(new Rect(0f, centerY - cardHeight * 0.5f - 48f, Screen.width, 30f),
            "Bir tane seç  ·  fareyle tıkla ya da 1-2-3", subtitleStyle);

        int count = offers.Count;
        float totalWidth = count * cardWidth + (count - 1) * cardGap;
        float x = (Screen.width - totalWidth) * 0.5f;
        float y = centerY - cardHeight * 0.5f;

        for (int i = 0; i < count; i++)
        {
            DrawCard(offers[i], i, new Rect(x + i * (cardWidth + cardGap), y, cardWidth, cardHeight));
        }
    }

    private void DrawCard(ChestOffer offer, int index, Rect rect)
    {
        Color previous = GUI.color;

        // Kilitli kart soluk görünür ve tıklanamaz.
        if (offer.Locked)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            GUI.Box(rect, GUIContent.none);
        }
        else
        {
            if (GUI.Button(rect, GUIContent.none)) Choose(index);
        }

        GUI.color = offer.Color;
        if (offer.Locked) GUI.color = new Color(offer.Color.r, offer.Color.g, offer.Color.b, 0.4f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 5f), Texture2D.whiteTexture);
        GUI.color = previous;

        Rect inner = new Rect(rect.x + 16f, rect.y + 16f, rect.width - 32f, rect.height - 32f);

        GUI.Label(new Rect(inner.x, inner.y, inner.width, 24f),
            offer.Locked ? "KİLİTLİ" : "[" + (index + 1) + "]", keyStyle);

        GUI.color = offer.Locked
            ? new Color(offer.Color.r, offer.Color.g, offer.Color.b, 0.55f)
            : offer.Color;
        GUI.Label(new Rect(inner.x, inner.y + 26f, inner.width, 34f), offer.Title, headerStyle);
        GUI.color = previous;

        if (offer.Locked)
        {
            GUI.Label(new Rect(inner.x, inner.y + 64f, inner.width, 40f), offer.LockText, lockStyle);
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            GUI.Label(new Rect(inner.x, inner.y + 104f, inner.width, inner.height - 104f), offer.Effect, effectStyle);
            GUI.color = previous;
            return;
        }

        if (!string.IsNullOrEmpty(offer.Requirement))
        {
            GUI.Label(new Rect(inner.x, inner.y + 62f, inner.width, 22f), "Şart: " + offer.Requirement, keyStyle);
        }

        GUI.Label(new Rect(inner.x, inner.y + 86f, inner.width, inner.height - 86f), offer.Effect, effectStyle);
    }

    private void BuildStyles()
    {
        // GUIStyle yalnızca OnGUI içinde güvenle kurulabilir.
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(1f, 0.86f, 0.45f);
        }

        if (subtitleStyle == null)
        {
            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            subtitleStyle.normal.textColor = new Color(0.72f, 0.72f, 0.7f);
        }

        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            headerStyle.normal.textColor = Color.white;
        }

        if (effectStyle == null)
        {
            effectStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            effectStyle.normal.textColor = new Color(0.82f, 0.82f, 0.8f);
        }

        if (keyStyle == null)
        {
            keyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            keyStyle.normal.textColor = new Color(0.6f, 0.6f, 0.58f);
        }

        if (lockStyle == null)
        {
            lockStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            lockStyle.normal.textColor = new Color(1f, 0.5f, 0.45f);
        }
    }
}
