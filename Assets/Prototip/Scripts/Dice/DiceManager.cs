using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Oyuncunun zar havuzunun sahibi. Zar sayısı değiştikçe zarları doğurur/yok eder,
/// sol tıkı tek yerden okuyup ELDEKİ BİR zarı atar ve eldeki zarları göğüs
/// noktası çevresinde bir yüzük hâlinde dizer.
///
/// Tüm zarlar havadaysa oyuncu silahsızdır: atış yapılamaz. Oyunun ana gerilimi bu.
/// </summary>
public class DiceManager : MonoBehaviour
{
    /// <summary>Sahnedeki tek örnek. Kullanmadan önce null kontrolü yapılmalı.</summary>
    public static DiceManager Instance { get; private set; }

    [Header("Havuz")]
    [SerializeField, Tooltip("Zar prefabı.")]
    private GameObject dicePrefab;
    [SerializeField, Tooltip("Hiçbir eşya/buff yokken sahip olunan zar sayısı.")]
    private int baseDiceCount = 1;
    [SerializeField, Tooltip("Zar sayısının üst sınırı.")]
    private int maxDiceCount = 5;

    [Header("Elde Dizilim")]
    [SerializeField, Tooltip("Eldeki zarların göğüs noktası çevresindeki yüzük yarıçapı.")]
    private float holdOrbitRadius = 0.42f;
    [SerializeField, Tooltip("Yüzüğün dönme hızı (derece/saniye).")]
    private float holdOrbitSpeed = 35f;

    [Header("Referanslar")]
    [SerializeField, Tooltip("Zarları atan oyuncu.")]
    private PlayerController player;

    private readonly List<Dice> dice = new List<Dice>();

    /// <summary>Sahip olunan toplam zar sayısı.</summary>
    public int TotalDice => dice.Count;

    public int MaxDiceCount => maxDiceCount;

    /// <summary>Şu anda elde kaç zar var?</summary>
    public int HeldCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < dice.Count; i++)
            {
                if (dice[i] != null && dice[i].IsHeld) count++;
            }
            return count;
        }
    }

    /// <summary>Atış yapılabilir mi? (elde en az bir zar varsa)</summary>
    public bool CanThrow => HeldCount > 0;

    /// <summary>Zarların menzili (göstergeyi çizen AimIndicator bunu okur).</summary>
    public float Range => dice.Count > 0 && dice[0] != null ? dice[0].Range : 12f;

    /// <summary>Eşya ve buff'lar hesaba katılınca olması gereken zar sayısı.</summary>
    public int DesiredCount
    {
        get
        {
            int count = Mathf.Max(1, baseDiceCount);
            count += ItemDiceBonus;

            RunState run = RunState.Instance;
            if (run != null) count += run.BonusDice;

            return Mathf.Clamp(count, 1, Mathf.Max(1, maxDiceCount));
        }
    }

    /// <summary>Üst sınıra ulaşılmadıysa sandık Ek Zar teklif edebilir.</summary>
    public bool CanAddDice => DesiredCount < Mathf.Max(1, maxDiceCount);

    /// <summary>Yedek Zar Kesesi eşyasının katkısı.</summary>
    private int ItemDiceBonus
    {
        get
        {
            PlayerLoadout loadout = PlayerLoadout.Instance;
            return loadout != null && loadout.Has(ItemId.SpareDiceBag) ? 1 : 0;
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        Rebuild();
    }

    private void Update()
    {
        if (GamePause.Paused) return;

        LayoutHeldDice();

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        ThrowOne();
    }

    /// <summary>Her sol tık eldeki BİR zarı atar.</summary>
    public bool ThrowOne()
    {
        if (player == null) return false;

        for (int i = 0; i < dice.Count; i++)
        {
            Dice d = dice[i];
            if (d == null || !d.IsHeld) continue;

            return d.Throw(player.AimDirection);
        }

        return false;
    }

    /// <summary>
    /// Havuzu olması gereken sayıya getirir. Ek Zar alınınca ve koşu
    /// sıfırlanınca çağrılır.
    /// </summary>
    public void Rebuild()
    {
        // DungeonManager.Start bizden önce çağırabilir; oyuncu referansı hazır olmalı.
        if (player == null) player = FindFirstObjectByType<PlayerController>();

        if (dicePrefab == null)
        {
            Debug.LogWarning("[Zar Prototip] Zar prefabı bağlanmamış, havuz kurulamadı.");
            return;
        }

        // Yok edilmiş örnekleri listeden temizle.
        for (int i = dice.Count - 1; i >= 0; i--)
        {
            if (dice[i] == null) dice.RemoveAt(i);
        }

        int target = DesiredCount;

        while (dice.Count > target)
        {
            int last = dice.Count - 1;
            Dice d = dice[last];
            dice.RemoveAt(last);
            if (d != null) Destroy(d.gameObject);
        }

        while (dice.Count < target)
        {
            GameObject go = Instantiate(dicePrefab, transform);
            go.name = "Zar" + (dice.Count + 1);

            if (!go.TryGetComponent(out Dice d))
            {
                Debug.LogError("[Zar Prototip] Zar prefabında Dice bileşeni yok.");
                Destroy(go);
                return;
            }

            d.SetPlayer(player);
            d.ResetToHand();
            dice.Add(d);
        }

        LayoutHeldDice();
    }

    /// <summary>Bütün zarları ele döndürür (oda ve koşu geçişlerinde).</summary>
    public void ResetAll()
    {
        for (int i = 0; i < dice.Count; i++)
        {
            if (dice[i] != null) dice[i].ResetToHand();
        }

        LayoutHeldDice();
    }

    /// <summary>
    /// Verilen düşmanın başında, bu zar dışında kaç zar dönüyor?
    /// Zar kendi kayma yuvasını buradan öğrenir, üst üste binmesinler diye.
    /// </summary>
    public int CountRollingOn(EnemyHealth enemy, Dice except)
    {
        if (enemy == null) return 0;

        int count = 0;
        for (int i = 0; i < dice.Count; i++)
        {
            Dice d = dice[i];
            if (d == null || d == except) continue;
            if (d.RollingTarget == enemy) count++;
        }

        return count;
    }

    /// <summary>Eldeki zarları göğüs noktası çevresinde yavaşça dönen bir yüzüğe dizer.</summary>
    private void LayoutHeldDice()
    {
        int total = dice.Count;
        float spin = Time.time * holdOrbitSpeed;

        for (int i = 0; i < total; i++)
        {
            Dice d = dice[i];
            if (d == null) continue;

            if (total <= 1)
            {
                d.HoldOffset = Vector3.zero;
                continue;
            }

            float angle = (360f / total) * i + spin;
            d.HoldOffset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * holdOrbitRadius;
        }
    }
}
