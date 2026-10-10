using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kuşanılmış kalıcı eşyalar. Lobideki Silahlık'ta seçilir, PlayerPrefs'e
/// bit maskesi olarak kaydedilir ve koşu bitince SIFIRLANMAZ.
///
/// Bir stat sonradan şartın altına düşemediği için kuşanılmış eşyanın
/// geçersizleşmesi mümkün değil; yine de yükleme sırasında şart denetlenir.
/// </summary>
public class PlayerLoadout : MonoBehaviour
{
    /// <summary>Sahnedeki tek örnek. Kullanmadan önce null kontrolü yapılmalı.</summary>
    public static PlayerLoadout Instance { get; private set; }

    [SerializeField, Tooltip("Aynı anda kuşanılabilecek eşya sayısı (katalogdaki tüm eşyalar).")]
    private int slots = 6;

    private const string Key = "ZarPrototip_Esyalar";

    private readonly List<ItemId> equipped = new List<ItemId>();

    /// <summary>Kuşanılmış eşyalar.</summary>
    public IReadOnlyList<ItemId> Equipped => equipped;

    public int Slots => slots;
    public int Count => equipped.Count;

    /// <summary>Kuşanılmış eşyalar değişince tetiklenir (zar havuzu ve HUD dinler).</summary>
    public event Action OnChanged;

    private void Awake()
    {
        Instance = this;
        Load();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool Has(ItemId id)
    {
        return equipped.Contains(id);
    }

    /// <summary>
    /// Eşyayı kuşanır ya da çıkarır. Şart tutmuyorsa kuşanmaz ve false döner.
    /// </summary>
    public bool Toggle(ItemId id, out string reason)
    {
        reason = string.Empty;

        if (equipped.Contains(id))
        {
            equipped.Remove(id);
            Save();
            OnChanged?.Invoke();
            return true;
        }

        ItemDefinition def = ItemCatalog.Get(id);

        if (!def.Unlocked)
        {
            reason = def.LockText;
            return false;
        }

        if (equipped.Count >= Mathf.Max(1, slots))
        {
            reason = "Eşya slotları dolu.";
            return false;
        }

        equipped.Add(id);
        Save();
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Eşya seçimini temizler (F9 sıfırlaması).</summary>
    public void ClearAll()
    {
        if (equipped.Count == 0) return;

        equipped.Clear();
        Save();
        OnChanged?.Invoke();
    }

    private void Load()
    {
        equipped.Clear();

        int mask = PlayerPrefs.GetInt(Key, 0);

        for (int i = 0; i < ItemCatalog.All.Length; i++)
        {
            ItemDefinition def = ItemCatalog.All[i];
            int bit = 1 << (int)def.Id;

            if ((mask & bit) == 0) continue;

            // Şartı tutmayan bir eşya kayıtta kalmışsa kuşanılmış sayılmaz.
            if (!def.Unlocked) continue;

            equipped.Add(def.Id);
        }
    }

    private void Save()
    {
        int mask = 0;
        for (int i = 0; i < equipped.Count; i++) mask |= 1 << (int)equipped[i];

        PlayerPrefs.SetInt(Key, mask);
        PlayerPrefs.Save();
    }
}
