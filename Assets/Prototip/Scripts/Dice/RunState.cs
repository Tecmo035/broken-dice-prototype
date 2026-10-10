using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Koşuya özel durum: alınan buff'lar ve sandıklardan bulunan Ek Zar sayısı.
/// Koşu bitince (kazanma ya da ölüm) sıfırlanır. Kalıcı olan hiçbir şey burada
/// tutulmaz — statlar ve eşyalar PlayerProgress / PlayerLoadout tarafında.
/// </summary>
public class RunState : MonoBehaviour
{
    /// <summary>Sahnedeki tek örnek. Kullanmadan önce null kontrolü yapılmalı.</summary>
    public static RunState Instance { get; private set; }

    [Header("Buff")]
    [SerializeField, Tooltip("Kaç buff taşınabilir.")]
    private int buffSlots = 8;

    private readonly List<BuffId> buffs = new List<BuffId>();
    private int bonusDice;

    /// <summary>Bu koşuda alınmış buff'lar (slot sırasıyla).</summary>
    public IReadOnlyList<BuffId> Buffs => buffs;

    public int BuffSlots => buffSlots;
    public int BuffCount => buffs.Count;
    public bool SlotsFull => buffs.Count >= buffSlots;

    /// <summary>Bu koşuda sandıklardan bulunan ek zar sayısı.</summary>
    public int BonusDice => bonusDice;

    /// <summary>En son alınan buff; HUD slotu kısa süre parlatmak için kullanır.</summary>
    public BuffId LastGained { get; private set; }

    /// <summary>En son buff'ın alındığı gerçek zaman (unscaled).</summary>
    public float LastGainedTime { get; private set; } = -99f;

    /// <summary>Buff ya da ek zar değişince tetiklenir (HUD ve zar havuzu dinler).</summary>
    public event Action OnChanged;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool Has(BuffId id)
    {
        return buffs.Contains(id);
    }

    /// <summary>
    /// Buff alınabilir mi? Slot, tekrar, çakışma ve stat şartı birlikte denetlenir.
    /// </summary>
    public bool CanTake(BuffId id, bool ignoreRequirement = false)
    {
        if (SlotsFull) return false;
        if (buffs.Contains(id)) return false;
        if (BuffCatalog.ConflictsWith(id, buffs)) return false;
        if (!ignoreRequirement && !BuffCatalog.Get(id).Unlocked) return false;
        return true;
    }

    /// <summary>Buff'ı ekler. Alınamıyorsa false döner.</summary>
    public bool AddBuff(BuffId id, bool ignoreRequirement = false)
    {
        if (!CanTake(id, ignoreRequirement)) return false;

        buffs.Add(id);
        LastGained = id;
        LastGainedTime = Time.unscaledTime;

        OnChanged?.Invoke();
        return true;
    }

    public void AddBonusDice(int amount)
    {
        if (amount <= 0) return;

        bonusDice += amount;
        OnChanged?.Invoke();
    }

    /// <summary>Koşu bitti: buff'lar ve ek zarlar sıfırlanır.</summary>
    public void ClearRun()
    {
        bool changed = buffs.Count > 0 || bonusDice != 0;

        buffs.Clear();
        bonusDice = 0;
        LastGainedTime = -99f;

        if (changed) OnChanged?.Invoke();
    }
}
