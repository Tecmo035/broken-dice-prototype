using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lobideki Silahlık paneli (OnGUI). Altı eşyanın adı, şartı, etkisi ve
/// kuşanılmış olup olmadığı listelenir; tıklayarak ya da 1-6 tuşlarıyla
/// kuşanılıp çıkarılır. Şartı tutmayan eşya soluk görünür ve nedeni yazar.
///
/// Panel açıkken oyuncu hareket edemez; duraklatma GamePause üzerinden yapılır.
/// </summary>
public class ArmoryPanel : MonoBehaviour
{
    /// <summary>Sahnedeki tek örnek. Kullanmadan önce null kontrolü yapılmalı.</summary>
    public static ArmoryPanel Instance { get; private set; }

    [Header("Görünüm")]
    [SerializeField, Tooltip("Panelin genişliği (piksel).")]
    private float panelWidth = 720f;
    [SerializeField, Tooltip("Bir eşya satırının yüksekliği (piksel).")]
    private float rowHeight = 62f;
    [SerializeField, Tooltip("Satırlar arası boşluk (piksel).")]
    private float rowGap = 8f;

    private bool open;

    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle nameStyle;
    private GUIStyle effectStyle;
    private GUIStyle keyStyle;
    private GUIStyle lockStyle;
    private GUIStyle statusStyle;

    public bool IsOpen => open;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (open) GamePause.ForceResume();
    }

    public void Open()
    {
        if (open) return;

        open = true;
        GamePause.Set(true);
    }

    public void Close()
    {
        if (!open) return;

        open = false;
        GamePause.Set(false);
    }

    private void Update()
    {
        if (!open) return;

        // Paneli açan E basımı aynı karede buraya da düşebilir; kare damgası
        // o basımı yok sayar, yoksa panel açıldığı karede kapanırdı.
        if (GamePause.ChangedThisFrame) return;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        // 1-6 tuşlarıyla kuşan/çıkar.
        if (kb.digit1Key.wasPressedThisFrame) Toggle(0);
        else if (kb.digit2Key.wasPressedThisFrame) Toggle(1);
        else if (kb.digit3Key.wasPressedThisFrame) Toggle(2);
        else if (kb.digit4Key.wasPressedThisFrame) Toggle(3);
        else if (kb.digit5Key.wasPressedThisFrame) Toggle(4);
        else if (kb.digit6Key.wasPressedThisFrame) Toggle(5);
    }

    private void Toggle(int index)
    {
        if (index < 0 || index >= ItemCatalog.All.Length) return;

        PlayerLoadout loadout = PlayerLoadout.Instance;
        if (loadout == null) return;

        ItemDefinition def = ItemCatalog.All[index];

        if (!loadout.Toggle(def.Id, out string reason))
        {
            if (PrototipHud.Instance != null) PrototipHud.Instance.ShowMessage(reason, 2f);
            return;
        }

        // Yedek Zar Kesesi zar sayısını değiştirdiği için havuz yenilenir.
        DiceManager manager = DiceManager.Instance;
        if (manager != null)
        {
            manager.Rebuild();
            manager.ResetAll();
        }
    }

    private void OnGUI()
    {
        if (!open) return;

        BuildStyles();

        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;

        int count = ItemCatalog.All.Length;
        float listHeight = count * rowHeight + (count - 1) * rowGap;
        float panelHeight = listHeight + 150f;

        float x = (Screen.width - panelWidth) * 0.5f;
        float y = (Screen.height - panelHeight) * 0.5f;

        GUI.Box(new Rect(x, y, panelWidth, panelHeight), GUIContent.none);

        GUI.Label(new Rect(x, y + 18f, panelWidth, 40f), "SİLAHLIK", titleStyle);

        PlayerLoadout loadout = PlayerLoadout.Instance;
        int equipped = loadout != null ? loadout.Count : 0;
        int slots = loadout != null ? loadout.Slots : 6;

        GUI.Label(new Rect(x, y + 58f, panelWidth, 26f),
            "Kuşanılı " + equipped + "/" + slots + "  ·  tıkla ya da 1-6  ·  kapatmak için E / Esc",
            subtitleStyle);

        float rowY = y + 96f;
        for (int i = 0; i < count; i++)
        {
            DrawRow(ItemCatalog.All[i], i, new Rect(x + 18f, rowY, panelWidth - 36f, rowHeight));
            rowY += rowHeight + rowGap;
        }
    }

    private void DrawRow(ItemDefinition def, int index, Rect rect)
    {
        PlayerLoadout loadout = PlayerLoadout.Instance;
        bool isEquipped = loadout != null && loadout.Has(def.Id);
        bool unlocked = def.Unlocked;

        Color previous = GUI.color;

        if (unlocked)
        {
            if (GUI.Button(rect, GUIContent.none)) Toggle(index);
        }
        else
        {
            GUI.color = new Color(1f, 1f, 1f, 0.4f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = previous;
        }

        // Sol kenarda eşyanın rengi; kuşanılıysa dolu, değilse soluk.
        GUI.color = unlocked
            ? new Color(def.Color.r, def.Color.g, def.Color.b, isEquipped ? 1f : 0.45f)
            : new Color(def.Color.r, def.Color.g, def.Color.b, 0.25f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 6f, rect.height), Texture2D.whiteTexture);
        GUI.color = previous;

        float textX = rect.x + 18f;
        float textWidth = rect.width - 160f;

        GUI.Label(new Rect(textX, rect.y + 4f, 30f, 22f), (index + 1).ToString(), keyStyle);

        GUI.color = unlocked ? def.Color : new Color(def.Color.r, def.Color.g, def.Color.b, 0.5f);
        GUI.Label(new Rect(textX + 26f, rect.y + 6f, textWidth, 24f), def.Name, nameStyle);
        GUI.color = previous;

        if (unlocked)
        {
            GUI.Label(new Rect(textX + 26f, rect.y + 30f, textWidth, 24f), def.Effect, effectStyle);
        }
        else
        {
            GUI.Label(new Rect(textX + 26f, rect.y + 30f, textWidth, 24f), def.LockText, lockStyle);
        }

        // Sağda durum.
        Rect statusRect = new Rect(rect.xMax - 140f, rect.y + 18f, 128f, 26f);

        if (!unlocked)
        {
            GUI.color = new Color(1f, 0.55f, 0.5f, 0.8f);
            GUI.Label(statusRect, "KİLİTLİ", statusStyle);
        }
        else if (isEquipped)
        {
            GUI.color = new Color(0.5f, 1f, 0.6f);
            GUI.Label(statusRect, "KUŞANILI", statusStyle);
        }
        else
        {
            GUI.color = new Color(0.65f, 0.65f, 0.62f);
            GUI.Label(statusRect, "Boş — " + def.Requirement, statusStyle);
        }

        GUI.color = previous;
    }

    private void BuildStyles()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(1f, 0.86f, 0.45f);
        }

        if (subtitleStyle == null)
        {
            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter
            };
            subtitleStyle.normal.textColor = new Color(0.72f, 0.72f, 0.7f);
        }

        if (nameStyle == null)
        {
            nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold
            };
            nameStyle.normal.textColor = Color.white;
        }

        if (effectStyle == null)
        {
            effectStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14
            };
            effectStyle.normal.textColor = new Color(0.8f, 0.8f, 0.78f);
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
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            lockStyle.normal.textColor = new Color(1f, 0.5f, 0.45f);
        }

        if (statusStyle == null)
        {
            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            statusStyle.normal.textColor = Color.white;
        }
    }
}
