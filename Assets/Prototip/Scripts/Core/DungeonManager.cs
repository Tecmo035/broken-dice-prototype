using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Koşunun akışı. Tek sahnede lobi, üç normal oda düzeni ve boss odası
/// 100'er birim arayla durur; oyuncu aralarında ışınlanır.
///
/// Her koşuda toplam oda sayısı rastgele seçilir (varsayılan 3-7).
/// SON ODA HER ZAMAN BOSS ODASIDIR. Önceki odalar normal düzenlerden rastgele
/// seçilir ve aynı düzen art arda iki kez gelmez. Düşman sayısı oda sırasına
/// göre kademeli artar; her oda temizlendiğinde oyuncu 1 can kazanır.
/// </summary>
public class DungeonManager : MonoBehaviour
{
    [Header("Odalar")]
    [SerializeField, Tooltip("Normal oda düzenleri (Sütunlu, Koridorlu, Arena).")]
    private DungeonRoom[] rooms;
    [SerializeField, Tooltip("Her koşunun son odası: boss odası.")]
    private DungeonRoom bossRoom;

    [Header("Koşu Uzunluğu")]
    [SerializeField, Tooltip("Bir koşudaki en az oda sayısı (boss dahil).")]
    private int minRooms = 3;
    [SerializeField, Tooltip("Bir koşudaki en çok oda sayısı (boss dahil).")]
    private int maxRooms = 7;

    [Header("Zorluk Artışı")]
    [SerializeField, Tooltip("Kaç odada bir düşman sayısına +1 eklenir. 2 = her iki odada bir.")]
    private int roomsPerEnemyStep = 2;
    [SerializeField, Tooltip("Düşman sayısına eklenebilecek azami ek.")]
    private int maxEnemyBonus = 3;

    [Header("Ödüller")]
    [SerializeField, Tooltip("Oda temizlenince kazanılan can.")]
    private int healPerRoom = 1;
    [SerializeField, Tooltip("Zindan tamamlanınca kazanılan stat puanı.")]
    private int completionReward = 1;

    [Header("Lobi")]
    [SerializeField, Tooltip("Lobideki oyuncu doğma noktası.")]
    private Transform lobbySpawn;

    [Header("Referanslar")]
    [SerializeField, Tooltip("Işınlanacak oyuncu.")]
    private PlayerController player;
    [SerializeField, Tooltip("Ölüm dinlenecek oyuncu canı.")]
    private PlayerHealth playerHealth;
    [SerializeField, Tooltip("Geçişlerde zarları ele döndüren havuz.")]
    private DiceManager diceManager;
    [SerializeField, Tooltip("Koşu bitince sıfırlanacak koşu durumu (buff ve ek zarlar).")]
    private RunState runState;

    [Header("Zamanlama")]
    [SerializeField, Tooltip("Kapıdan geçtikten sonra sıradaki odaya gecikme (saniye).")]
    private float roomTransitionDelay = 0.9f;
    [SerializeField, Tooltip("Koşu bittikten sonra lobiye dönüş gecikmesi (saniye).")]
    private float runEndDelay = 2.4f;

    private readonly List<DungeonRoom> order = new List<DungeonRoom>();
    private DungeonRoom currentRoom;
    private int roomIndex = -1;
    private bool busy;

    /// <summary>Oyuncu şu anda zindanda mı (lobide değil mi)?</summary>
    public bool InDungeon => currentRoom != null;

    /// <summary>HUD için: kaçıncı odadayız (1 tabanlı). Lobide 0.</summary>
    public int CurrentRoomNumber => currentRoom != null ? roomIndex + 1 : 0;

    /// <summary>HUD için: bu koşunun toplam oda sayısı.</summary>
    public int TotalRooms => order.Count;

    /// <summary>HUD için: içinde bulunulan odanın adı.</summary>
    public string CurrentRoomName => currentRoom != null ? currentRoom.RoomName : "Lobi";

    /// <summary>HUD için: boss odasında mıyız?</summary>
    public bool InBossRoom => currentRoom != null && currentRoom.IsBossRoom;

    private void Start()
    {
        if (playerHealth != null) playerHealth.OnDeath += HandlePlayerDeath;
        GoToLobby();
    }

    private void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnDeath -= HandlePlayerDeath;
        UnsubscribeRoom();
    }

    /// <summary>Lobideki zindan girişi bunu çağırır.</summary>
    public void StartRun()
    {
        if (busy || InDungeon) return;

        BuildRunOrder();
        if (order.Count == 0)
        {
            Say("Zindanda oda yok (odalar bağlanmamış).", 3f);
            return;
        }

        if (playerHealth != null) playerHealth.ResetHealth();

        // Buff'lar ve ek zarlar koşuya özeldir: her koşu temiz başlar.
        ResetRun();

        roomIndex = -1;
        AdvanceRoom();
    }

    /// <summary>
    /// Koşu planı: rastgele uzunluk, normal düzenlerden tekrarsız dizi ve
    /// en sonda boss odası.
    /// </summary>
    private void BuildRunOrder()
    {
        order.Clear();

        List<DungeonRoom> pool = new List<DungeonRoom>();
        if (rooms != null)
        {
            for (int i = 0; i < rooms.Length; i++)
            {
                if (rooms[i] != null && !rooms[i].IsBossRoom) pool.Add(rooms[i]);
            }
        }

        int low = Mathf.Max(1, minRooms);
        int high = Mathf.Max(low, maxRooms);
        int total = Random.Range(low, high + 1);

        // Boss odası varsa son oda ona ayrılır.
        int normalCount = bossRoom != null ? total - 1 : total;

        DungeonRoom previous = null;
        for (int i = 0; i < normalCount && pool.Count > 0; i++)
        {
            DungeonRoom pick = PickDifferent(pool, previous);
            order.Add(pick);
            previous = pick;
        }

        if (bossRoom != null) order.Add(bossRoom);
    }

    /// <summary>Aynı düzen art arda iki kez gelmesin diye öncekinden farklı seçer.</summary>
    private static DungeonRoom PickDifferent(List<DungeonRoom> pool, DungeonRoom previous)
    {
        if (pool.Count == 1) return pool[0];

        for (int attempt = 0; attempt < 12; attempt++)
        {
            DungeonRoom candidate = pool[Random.Range(0, pool.Count)];
            if (candidate != previous) return candidate;
        }

        return pool[Random.Range(0, pool.Count)];
    }

    private void AdvanceRoom()
    {
        roomIndex++;

        if (roomIndex >= order.Count)
        {
            StartCoroutine(CompleteRunRoutine());
            return;
        }

        EnterRoom(order[roomIndex]);
    }

    private void EnterRoom(DungeonRoom room)
    {
        UnsubscribeRoom();
        currentRoom = room;

        GamePause.ForceResume();

        if (player != null) player.Teleport(room.PlayerEntry.position);
        if (diceManager != null) diceManager.ResetAll();

        if (room.Door != null)
        {
            room.Door.SetPlayer(player != null ? player.transform : null);
            room.Door.OnPassed += HandleDoorPassed;
        }

        room.OnCleared += HandleRoomCleared;
        room.Activate(playerHealth, EnemyBonusForRoom(roomIndex));

        string label = room.IsBossRoom
            ? "BOSS — " + room.RoomName
            : "Oda " + CurrentRoomNumber + "/" + order.Count + " — " + room.RoomName;

        Say(label, 1.8f);
    }

    /// <summary>Oda sırasına bağlı düşman sayısı eki.</summary>
    private int EnemyBonusForRoom(int index)
    {
        int step = Mathf.Max(1, roomsPerEnemyStep);
        return Mathf.Clamp(index / step, 0, Mathf.Max(0, maxEnemyBonus));
    }

    /// <summary>Oda temizlendi: oyuncu bir can kazanır.</summary>
    private void HandleRoomCleared(DungeonRoom room)
    {
        if (playerHealth == null || healPerRoom <= 0) return;

        int healed = playerHealth.Heal(healPerRoom);
        if (healed > 0) Say("+" + healed + " can", 1.4f);
    }

    private void HandleDoorPassed()
    {
        if (busy) return;
        StartCoroutine(NextRoomRoutine());
    }

    private IEnumerator NextRoomRoutine()
    {
        busy = true;

        DungeonRoom leaving = currentRoom;
        UnsubscribeRoom();
        if (leaving != null) leaving.Deactivate();
        currentRoom = null;

        yield return new WaitForSecondsRealtime(roomTransitionDelay);

        busy = false;
        AdvanceRoom();
    }

    private IEnumerator CompleteRunRoutine()
    {
        busy = true;

        UnsubscribeRoom();
        currentRoom = null;

        if (PlayerProgress.Instance != null) PlayerProgress.Instance.AddReward(completionReward);
        Say("Zindan tamamlandı! +" + completionReward + " stat puanı", runEndDelay);

        yield return new WaitForSecondsRealtime(runEndDelay);

        busy = false;
        GoToLobby();
    }

    private void HandlePlayerDeath()
    {
        if (busy) return;
        StartCoroutine(LoseRunRoutine());
    }

    private IEnumerator LoseRunRoutine()
    {
        busy = true;
        GamePause.ForceResume();

        Say("Zindanı kaybettin — ödül yok", runEndDelay);

        yield return new WaitForSecondsRealtime(runEndDelay);

        busy = false;
        GoToLobby();
    }

    /// <summary>
    /// Koşuyu bitirip lobiye döner: odaları boşaltır, canı doldurur,
    /// buff'ları ve ek zarları sıfırlar. Kalıcı eşya ve statlar etkilenmez.
    /// </summary>
    public void GoToLobby()
    {
        GamePause.ForceResume();

        UnsubscribeRoom();
        currentRoom = null;
        roomIndex = -1;

        DeactivateAllRooms();
        ClearProjectiles();

        if (playerHealth != null) playerHealth.ResetHealth();
        if (player != null && lobbySpawn != null) player.Teleport(lobbySpawn.position);

        ResetRun();
        order.Clear();
    }

    private void DeactivateAllRooms()
    {
        if (rooms != null)
        {
            for (int i = 0; i < rooms.Length; i++)
            {
                if (rooms[i] != null) rooms[i].Deactivate();
            }
        }

        if (bossRoom != null) bossRoom.Deactivate();
    }

    /// <summary>Koşuya özel her şeyi sıfırlar ve zar havuzunu yeniden kurar.</summary>
    private void ResetRun()
    {
        if (runState != null) runState.ClearRun();

        if (diceManager != null)
        {
            diceManager.Rebuild();
            diceManager.ResetAll();
        }
    }

    private void UnsubscribeRoom()
    {
        if (currentRoom == null) return;

        currentRoom.OnCleared -= HandleRoomCleared;
        if (currentRoom.Door != null) currentRoom.Door.OnPassed -= HandleDoorPassed;
    }

    private static void ClearProjectiles()
    {
        EnemyProjectile[] projectiles = FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
        for (int i = 0; i < projectiles.Length; i++)
        {
            if (projectiles[i] != null) Destroy(projectiles[i].gameObject);
        }
    }

    private static void Say(string message, float duration)
    {
        if (PrototipHud.Instance != null) PrototipHud.Instance.ShowMessage(message, duration);
    }

#if UNITY_EDITOR
    /// <summary>F5: koşuyu başlatıp doğrudan boss odasına atlar (yalnızca editörde).</summary>
    public void DebugJumpToBoss()
    {
        if (bossRoom == null)
        {
            Say("Boss odası bağlanmamış.", 2f);
            return;
        }

        StopAllCoroutines();
        busy = false;

        if (playerHealth != null) playerHealth.ResetHealth();

        DeactivateAllRooms();
        ClearProjectiles();

        order.Clear();
        order.Add(bossRoom);

        roomIndex = -1;
        AdvanceRoom();
    }
#endif
}
