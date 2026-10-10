using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Sunum ve test için hata ayıklama tuşları. TAMAMI #if UNITY_EDITOR içinde:
/// derlemede bu kodun hiçbiri bulunmaz.
///
///   F5 = doğrudan boss odasına ışınlan
///   F6 = 1 ödül puanı ver
///   F7 = Ek Zar ver
///   F8 = şartı yok sayarak sıradaki buff'ı ver
///   F9 = kaydı sıfırla (statlar ve eşya seçimi dahil)
/// </summary>
public class DebugKeys : MonoBehaviour
{
#if UNITY_EDITOR
    [SerializeField, Tooltip("Hata ayıklama tuşlarını aç/kapat.")]
    private bool enableKeys = true;

    [SerializeField, Tooltip("HUD'un köşesinde tuş listesini göster.")]
    private bool showHint = true;

    /// <summary>HUD listeyi gösterecek mi? (editör dışında her zaman false)</summary>
    public static bool ShowHint { get; private set; }

    private void Awake()
    {
        ShowHint = showHint && enableKeys;
    }

    private void Update()
    {
        if (!enableKeys) return;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.f5Key.wasPressedThisFrame) JumpToBoss();
        if (kb.f6Key.wasPressedThisFrame) GiveReward();
        if (kb.f7Key.wasPressedThisFrame) GiveDice();
        if (kb.f8Key.wasPressedThisFrame) GiveBuff();
        if (kb.f9Key.wasPressedThisFrame) ResetSave();
    }

    private static void JumpToBoss()
    {
        DungeonManager dungeon = FindFirstObjectByType<DungeonManager>();
        if (dungeon == null)
        {
            Say("[F5] Zindan yöneticisi yok.");
            return;
        }

        dungeon.DebugJumpToBoss();
        Say("[F5] Boss odası");
    }

    private static void GiveReward()
    {
        PlayerProgress progress = PlayerProgress.Instance;
        if (progress == null) return;

        progress.AddReward(1);
        Say("[F6] +1 ödül puanı");
    }

    private static void GiveDice()
    {
        RunState run = RunState.Instance;
        DiceManager manager = DiceManager.Instance;

        if (manager != null && !manager.CanAddDice)
        {
            Say("[F7] Zar üst sınırda.");
            return;
        }

        if (run != null) run.AddBonusDice(1);
        if (manager != null) manager.Rebuild();

        Say("[F7] Ek Zar — toplam " + (manager != null ? manager.TotalDice : 0));
    }

    /// <summary>Stat şartını yok sayarak sıradaki alınabilir buff'ı verir.</summary>
    private static void GiveBuff()
    {
        RunState run = RunState.Instance;
        if (run == null) return;

        for (int i = 0; i < BuffCatalog.All.Length; i++)
        {
            BuffDefinition def = BuffCatalog.All[i];
            if (!run.AddBuff(def.Id, true)) continue;

            Say("[F8] " + def.Name + " — " + def.Effect);
            return;
        }

        Say("[F8] Verilecek buff kalmadı.");
    }

    private static void ResetSave()
    {
        PlayerProgress progress = PlayerProgress.Instance;
        if (progress != null) progress.ResetAll();

        PlayerLoadout loadout = PlayerLoadout.Instance;
        if (loadout != null) loadout.ClearAll();

        DiceManager manager = DiceManager.Instance;
        if (manager != null)
        {
            manager.Rebuild();
            manager.ResetAll();
        }

        Say("[F9] Kayıt sıfırlandı (statlar ve eşyalar)");
    }

    private static void Say(string message)
    {
        if (PrototipHud.Instance != null) PrototipHud.Instance.ShowMessage(message, 2f);
    }
#endif
}
