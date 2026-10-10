using UnityEngine;

/// <summary>
/// Prototip HUD'u. Canvas ve TextMeshPro gerektirmemesi için tamamen OnGUI (IMGUI).
///
/// Sol üst   : can, zar sayacı, konum, kalan düşman, son atış dökümü
/// Sağ üst   : dört stat, stat puanı, kuşanılmış eşyalar
/// Sol alt   : 8 slotluk buff sembol sırası (fareyle üstüne gelince açıklama)
/// Üst orta  : boss adı ve geniş can çubuğu
/// Orta      : kısa süreli mesajlar
/// </summary>
public class PrototipHud : MonoBehaviour
{
    /// <summary>Sahnedeki tek HUD örneği. Kullanmadan önce null kontrolü yapılmalı.</summary>
    public static PrototipHud Instance { get; private set; }

    [Header("Referanslar")]
    [SerializeField, Tooltip("Can göstergesi için oyuncu canı.")]
    private PlayerHealth playerHealth;
    [SerializeField, Tooltip("Zar sayacı için zar havuzu.")]
    private DiceManager diceManager;
    [SerializeField, Tooltip("Oda numarası için zindan yöneticisi.")]
    private DungeonManager dungeon;
    [SerializeField, Tooltip("Etkileşim ipucu için oyuncunun etkileşim bileşeni.")]
    private PlayerInteractor interactor;
    [SerializeField, Tooltip("Buff sembolleri için koşu durumu.")]
    private RunState runState;
    [SerializeField, Tooltip("Eşya listesi için kalıcı donanım.")]
    private PlayerLoadout loadout;

    [Header("Görünüm")]
    [SerializeField, Tooltip("Normal yazı boyutu.")]
    private int fontSize = 17;
    [SerializeField, Tooltip("Ekran ortası mesaj yazı boyutu.")]
    private int messageFontSize = 34;
    [SerializeField, Tooltip("Can kutucuğunun genişliği (piksel).")]
    private float heartWidth = 17f;
    [SerializeField, Tooltip("Can kutucuğunun yüksekliği (piksel).")]
    private float heartHeight = 14f;

    [Header("Buff Sırası")]
    [SerializeField, Tooltip("Bir buff slotunun kenar uzunluğu (piksel).")]
    private float slotSize = 42f;
    [SerializeField, Tooltip("Slotlar arası boşluk (piksel).")]
    private float slotGap = 6f;
    [SerializeField, Tooltip("Yeni alınan buff slotunun parlama süresi (saniye).")]
    private float slotFlashDuration = 1.2f;

    [Header("Hasar Parlaması")]
    [SerializeField, Tooltip("Hasar alınca ekran kenarındaki kırmızı parlamanın süresi (saniye).")]
    private float hurtFlashDuration = 0.38f;
    [SerializeField, Tooltip("Parlamanın en yüksek saydamlığı.")]
    private float hurtFlashAlpha = 0.5f;

    private GUIStyle labelStyle;
    private GUIStyle hintStyle;
    private GUIStyle messageStyle;
    private GUIStyle promptStyle;
    private GUIStyle promptDisabledStyle;
    private GUIStyle itemStyle;
    private GUIStyle shotStyle;
    private GUIStyle bossStyle;
    private GUIStyle tooltipStyle;
    private GUIStyle debugStyle;

    private string message = string.Empty;
    private float messageTimeLeft;
    private float hurtFlashTimeLeft;

    private void Awake()
    {
        Instance = this;

        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (diceManager == null) diceManager = FindFirstObjectByType<DiceManager>();
        if (dungeon == null) dungeon = FindFirstObjectByType<DungeonManager>();
        if (interactor == null) interactor = FindFirstObjectByType<PlayerInteractor>();
        if (runState == null) runState = FindFirstObjectByType<RunState>();
        if (loadout == null) loadout = FindFirstObjectByType<PlayerLoadout>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Zaman yavaşlamasından etkilenmesin.
        float dt = Time.unscaledDeltaTime;
        if (messageTimeLeft > 0f) messageTimeLeft -= dt;
        if (hurtFlashTimeLeft > 0f) hurtFlashTimeLeft -= dt;
    }

    /// <summary>Oyuncu hasar alınca ekran kenarını kısa süre kırmızıya boyar.</summary>
    public void FlashHurt()
    {
        hurtFlashTimeLeft = Mathf.Max(0.05f, hurtFlashDuration);
    }

    /// <summary>Ekranın ortasında kısa süreli bir mesaj gösterir.</summary>
    public void ShowMessage(string text, float duration = 2f)
    {
        message = text;
        messageTimeLeft = Mathf.Max(0.1f, duration);
    }

    private void OnGUI()
    {
        BuildStyles();

        DrawHurtFlash();
        DrawLeftPanel();
        DrawRightPanel();
        DrawBuffRow();
        DrawBossBar();
        DrawPrompt();
        DrawMessage();
        DrawDebugHint();
    }

    /// <summary>Hasar geri bildirimi: ekranın dört kenarında sönen kırmızı bantlar.</summary>
    private void DrawHurtFlash()
    {
        if (hurtFlashTimeLeft <= 0f) return;

        float t = Mathf.Clamp01(hurtFlashTimeLeft / Mathf.Max(0.01f, hurtFlashDuration));
        float alpha = hurtFlashAlpha * t * t;

        Color previous = GUI.color;
        GUI.color = new Color(0.8f, 0.06f, 0.09f, alpha);

        float h = Screen.height * 0.085f;
        float w = Screen.width * 0.055f;

        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, h), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, Screen.height - h, Screen.width, h), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, 0f, w, Screen.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(Screen.width - w, 0f, w, Screen.height), Texture2D.whiteTexture);

        GUI.color = previous;
    }

    // --- Sol üst: can, zar, konum, son atış ------------------------------

    private void DrawLeftPanel()
    {
        float x = 16f;
        float y = 14f;
        const float line = 24f;

        GUI.Label(new Rect(x, y, 60f, line), "CAN", labelStyle);
        DrawHealth(x + 54f, y + 4f);
        y += line + 4f;

        GUI.Label(new Rect(x, y, 380f, line), DiceText(), labelStyle);
        y += line;

        GUI.Label(new Rect(x, y, 380f, line), LocationText(), labelStyle);
        y += line;

        if (dungeon != null && dungeon.InDungeon)
        {
            GUI.Label(new Rect(x, y, 380f, line), "DÜŞMAN: " + EnemyHealth.All.Count, labelStyle);
            y += line;
        }

        // Son atış dökümü: hasarın nasıl hesaplandığı.
        CombatModifiers mods = CombatModifiers.Instance;
        if (mods != null && mods.LastShot != null)
        {
            y += 6f;
            GUI.Label(new Rect(x, y, 620f, line), "SON ATIŞ", hintStyle);
            y += line - 6f;

            Color previous = GUI.color;
            GUI.color = mods.LastShot.Crit ? new Color(1f, 0.78f, 0.3f) : new Color(0.92f, 0.9f, 0.86f);
            GUI.Label(new Rect(x, y, 680f, line + 6f), mods.LastShot.Text, shotStyle);
            GUI.color = previous;
            y += line + 2f;
        }

        y += 6f;
        GUI.Label(new Rect(x, y, 620f, line),
            "WASD hareket · Fare nişan · Sol tık zar at · Space kaçınma · E etkileşim",
            hintStyle);
    }

    private void DrawHealth(float x, float y)
    {
        int max = playerHealth != null ? playerHealth.Max : 5;
        int current = playerHealth != null ? playerHealth.Current : 0;

        Color previous = GUI.color;

        for (int i = 0; i < max; i++)
        {
            GUI.color = i < current
                ? new Color(0.92f, 0.22f, 0.26f)
                : new Color(0.18f, 0.18f, 0.21f);

            GUI.DrawTexture(new Rect(x + i * (heartWidth + 3f), y, heartWidth, heartHeight), Texture2D.whiteTexture);
        }

        GUI.color = previous;
    }

    /// <summary>"ZAR: 2/3 elde" — tümü havadaysa oyuncunun silahsız olduğunu da söyler.</summary>
    private string DiceText()
    {
        if (diceManager == null) return "ZAR: -";

        int held = diceManager.HeldCount;
        int total = diceManager.TotalDice;

        string text = "ZAR: " + held + "/" + total + " elde";
        if (held == 0) text += "   — SİLAHSIZ";
        return text;
    }

    private string LocationText()
    {
        if (dungeon == null) return "KONUM: -";
        if (!dungeon.InDungeon) return "KONUM: Lobi";

        string suffix = dungeon.InBossRoom ? "BOSS" : dungeon.CurrentRoomName;
        return "ODA " + dungeon.CurrentRoomNumber + "/" + dungeon.TotalRooms + " — " + suffix;
    }

    // --- Sağ üst: statlar, stat puanı, eşyalar --------------------------

    private void DrawRightPanel()
    {
        PlayerProgress progress = PlayerProgress.Instance;
        if (progress == null) return;

        const float width = 235f;
        const float line = 23f;

        float x = Screen.width - width - 16f;
        float y = 14f;

        GUI.Label(new Rect(x, y, width, line), "GÜÇ            " + progress.Strength, labelStyle);
        y += line;
        GUI.Label(new Rect(x, y, width, line), "ÇEVİKLİK       " + progress.Agility, labelStyle);
        y += line;
        GUI.Label(new Rect(x, y, width, line), "ZEKÂ           " + progress.Intellect, labelStyle);
        y += line;
        GUI.Label(new Rect(x, y, width, line), "DAYANIKLILIK   " + progress.Endurance, labelStyle);
        y += line + 6f;

        if (progress.RewardPoints > 0)
        {
            Color previous = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(x, y, width, line), "STAT PUANI: " + progress.RewardPoints, labelStyle);
            GUI.color = previous;
        }
        else
        {
            GUI.Label(new Rect(x, y, width, line), "Stat puanı: 0", hintStyle);
        }

        y += line + 10f;
        DrawItems(x, y, width, line);
    }

    /// <summary>Kuşanılmış kalıcı eşyalar.</summary>
    private void DrawItems(float x, float y, float width, float line)
    {
        if (loadout == null) return;

        System.Collections.Generic.IReadOnlyList<ItemId> equipped = loadout.Equipped;
        int count = equipped != null ? equipped.Count : 0;

        GUI.Label(new Rect(x, y, width, line), "EŞYA " + count + "/" + loadout.Slots, labelStyle);
        y += line;

        if (count == 0)
        {
            GUI.Label(new Rect(x, y, width, line), "Lobideki Silahlık'ta seç", hintStyle);
            return;
        }

        Color previous = GUI.color;

        for (int i = 0; i < count; i++)
        {
            ItemDefinition def = ItemCatalog.Get(equipped[i]);

            GUI.color = def.Color;
            GUI.Label(new Rect(x, y, width, line), def.Name, itemStyle);
            GUI.color = previous;
            y += line - 4f;
        }

        GUI.color = previous;
    }

    // --- Sol alt: buff sembol sırası ------------------------------------

    private void DrawBuffRow()
    {
        if (runState == null) return;

        int slots = Mathf.Max(1, runState.BuffSlots);
        System.Collections.Generic.IReadOnlyList<BuffId> buffs = runState.Buffs;
        int count = buffs != null ? buffs.Count : 0;

        float totalWidth = slots * slotSize + (slots - 1) * slotGap;
        float x = 16f;
        float y = Screen.height - slotSize - 28f;

        GUI.Label(new Rect(x, y - 24f, totalWidth, 22f), "BUFF " + count + "/" + slots, labelStyle);

        Color previous = GUI.color;
        int hovered = -1;

        for (int i = 0; i < slots; i++)
        {
            Rect slot = new Rect(x + i * (slotSize + slotGap), y, slotSize, slotSize);

            if (i >= count)
            {
                // Boş slot: soluk çerçeve.
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                GUI.DrawTexture(slot, BuffIcons.EmptySlot());
                GUI.color = previous;
                continue;
            }

            BuffDefinition def = BuffCatalog.Get(buffs[i]);

            // Yeni alınan buff kısa süre parlasın.
            float flash = 0f;
            if (buffs[i] == runState.LastGained)
            {
                float age = Time.unscaledTime - runState.LastGainedTime;
                if (age >= 0f && age < slotFlashDuration)
                {
                    flash = (1f - age / slotFlashDuration) * Mathf.Abs(Mathf.Sin(age * 14f));
                }
            }

            // Buff'a özgü renkte kutu.
            GUI.color = new Color(def.Color.r, def.Color.g, def.Color.b, 0.3f + 0.5f * flash);
            GUI.DrawTexture(slot, Texture2D.whiteTexture);

            // İçinde buff'ı anlatan sembol.
            GUI.color = Color.Lerp(def.Color, Color.white, 0.35f + 0.6f * flash);
            GUI.DrawTexture(new Rect(slot.x + 5f, slot.y + 5f, slot.width - 10f, slot.height - 10f),
                BuffIcons.Get(buffs[i]));

            GUI.color = previous;

            if (slot.Contains(Event.current.mousePosition)) hovered = i;
        }

        GUI.color = previous;

        if (hovered >= 0 && hovered < count) DrawBuffTooltip(buffs[hovered], y);
    }

    /// <summary>Fare sembolün üstündeyken adı ve etkisi küçük bir kutuda görünür.</summary>
    private void DrawBuffTooltip(BuffId id, float rowY)
    {
        BuffDefinition def = BuffCatalog.Get(id);
        string text = def.Name + "  (" + def.Requirement + ")\n" + def.Effect;

        const float width = 310f;
        const float height = 62f;

        Rect box = new Rect(16f, rowY - height - 34f, width, height);

        Color previous = GUI.color;
        GUI.color = new Color(0.04f, 0.04f, 0.06f, 0.93f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);

        GUI.color = def.Color;
        GUI.DrawTexture(new Rect(box.x, box.y, 4f, box.height), Texture2D.whiteTexture);
        GUI.color = previous;

        GUI.Label(new Rect(box.x + 12f, box.y + 6f, box.width - 20f, box.height - 10f), text, tooltipStyle);
    }

    // --- Üst orta: boss can çubuğu --------------------------------------

    private void DrawBossBar()
    {
        BossEnemy boss = BossEnemy.Active;
        if (boss == null) return;

        EnemyHealth health = boss.Health;
        if (health == null || !health.IsAlive) return;

        float width = Mathf.Min(620f, Screen.width - 120f);
        float x = (Screen.width - width) * 0.5f;
        float y = 22f;
        const float barHeight = 18f;

        GUI.Label(new Rect(x, y, width, 26f),
            boss.BossName + (boss.IsEnraged ? "  —  ÖFKELİ" : string.Empty), bossStyle);

        y += 28f;

        float fraction = health.Max > 0 ? Mathf.Clamp01((float)health.Current / health.Max) : 0f;

        Color previous = GUI.color;

        GUI.color = new Color(0.05f, 0.05f, 0.07f, 0.9f);
        GUI.DrawTexture(new Rect(x - 2f, y - 2f, width + 4f, barHeight + 4f), Texture2D.whiteTexture);

        GUI.color = new Color(0.16f, 0.16f, 0.2f);
        GUI.DrawTexture(new Rect(x, y, width, barHeight), Texture2D.whiteTexture);

        GUI.color = boss.IsEnraged ? new Color(1f, 0.25f, 0.18f) : new Color(0.85f, 0.2f, 0.25f);
        GUI.DrawTexture(new Rect(x, y, width * fraction, barHeight), Texture2D.whiteTexture);

        GUI.color = previous;

        GUI.Label(new Rect(x, y, width, barHeight), health.Current + " / " + health.Max, bossStyle);
    }

    // --- Etkileşim ipucu ve mesaj ---------------------------------------

    private void DrawPrompt()
    {
        if (interactor == null) return;

        Interactable target = interactor.Current;
        if (target == null) return;

        GUIStyle style = target.CanInteract ? promptStyle : promptDisabledStyle;
        GUI.Label(new Rect(0f, Screen.height - 132f, Screen.width, 32f), "[E]  " + target.Prompt, style);
    }

    private void DrawMessage()
    {
        if (messageTimeLeft <= 0f || string.IsNullOrEmpty(message)) return;
        GUI.Label(new Rect(0f, Screen.height * 0.3f, Screen.width, 70f), message, messageStyle);
    }

    /// <summary>Hata ayıklama tuş listesi; yalnızca editörde görünür.</summary>
    private void DrawDebugHint()
    {
#if UNITY_EDITOR
        if (!DebugKeys.ShowHint) return;

        const float width = 220f;
        float x = Screen.width - width - 16f;
        float y = Screen.height - 110f;

        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x - 8f, y - 6f, width + 16f, 102f), Texture2D.whiteTexture);
        GUI.color = previous;

        GUI.Label(new Rect(x, y, width, 94f),
            "F5 boss odası\nF6 +1 stat puanı\nF7 ek zar\nF8 buff (şartsız)\nF9 kaydı sıfırla",
            debugStyle);
#endif
    }

    // --- Stiller ---------------------------------------------------------

    private void BuildStyles()
    {
        // GUIStyle yalnızca OnGUI içinde güvenle kurulabilir.
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = new Color(0.94f, 0.92f, 0.88f);
        }

        if (hintStyle == null)
        {
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(10, fontSize - 4)
            };
            hintStyle.normal.textColor = new Color(0.58f, 0.56f, 0.53f);
        }

        if (messageStyle == null)
        {
            messageStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = messageFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            messageStyle.normal.textColor = new Color(1f, 0.84f, 0.42f);
        }

        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize + 4,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            promptStyle.normal.textColor = new Color(0.9f, 1f, 0.75f);
        }

        if (promptDisabledStyle == null)
        {
            promptDisabledStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize + 2,
                alignment = TextAnchor.MiddleCenter
            };
            promptDisabledStyle.normal.textColor = new Color(0.72f, 0.55f, 0.5f);
        }

        if (itemStyle == null)
        {
            // GUI.color ile renklendirilebilmesi için yazı rengi beyaz bırakılıyor.
            itemStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize - 2,
                fontStyle = FontStyle.Bold
            };
            itemStyle.normal.textColor = Color.white;
        }

        if (shotStyle == null)
        {
            shotStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize - 1,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            shotStyle.normal.textColor = Color.white;
        }

        if (bossStyle == null)
        {
            bossStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize + 1,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            bossStyle.normal.textColor = Color.white;
        }

        if (tooltipStyle == null)
        {
            tooltipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize - 3,
                wordWrap = true
            };
            tooltipStyle.normal.textColor = new Color(0.92f, 0.92f, 0.9f);
        }

        if (debugStyle == null)
        {
            debugStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13
            };
            debugStyle.normal.textColor = new Color(0.6f, 0.78f, 0.6f);
        }
    }
}
