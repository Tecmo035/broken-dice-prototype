using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Elle tasarlanmış bir zindan odası. Kendi düşman havuzu, doğma noktaları,
/// sandık noktası ve çıkış kapısı vardır.
///
/// Odaya girilince havuzdan rastgele düşmanlar doğar. SANDIK ODA BAŞLARKEN
/// YOKTUR: tüm düşmanlar ölünce kapıyla aynı anda belirir. Oda ikinci kez
/// ziyaret edilirse Activate her şeyi (düşman, sandık, kapı) sıfırdan kurar.
/// </summary>
public class DungeonRoom : MonoBehaviour
{
    [Header("Tanım")]
    [SerializeField, Tooltip("HUD ve mesajlarda görünecek oda adı.")]
    private string roomName = "Oda";
    [SerializeField, Tooltip("Boss odası mı? Boss odası her koşunun sonunda gelir.")]
    private bool bossRoom;

    [Header("Noktalar")]
    [SerializeField, Tooltip("Oyuncunun odaya giriş noktası.")]
    private Transform playerEntry;
    [SerializeField, Tooltip("Düşmanların doğabileceği noktalar.")]
    private Transform[] enemySpawns;
    [SerializeField, Tooltip("Oda temizlenince sandığın belireceği nokta.")]
    private Transform chestSpawn;

    [Header("Düşman Havuzu")]
    [SerializeField, Tooltip("Kovalayan düşman prefabı.")]
    private GameObject chaserPrefab;
    [SerializeField, Tooltip("Mermi atan düşman prefabı.")]
    private GameObject shooterPrefab;
    [SerializeField, Tooltip("Boss prefabı (yalnızca boss odasında kullanılır).")]
    private GameObject bossPrefab;
    [SerializeField, Range(0f, 1f), Tooltip("Kovalayan çıkma ağırlığı. 1 = hepsi kovalayan.")]
    private float chaserWeight = 0.6f;
    [SerializeField, Tooltip("En az düşman sayısı.")]
    private int minEnemies = 3;
    [SerializeField, Tooltip("En çok düşman sayısı.")]
    private int maxEnemies = 4;

    [Header("Sandık")]
    [SerializeField, Tooltip("Oda temizlenince belirecek sandık prefabı.")]
    private GameObject chestPrefab;

    [Header("Kapı")]
    [SerializeField, Tooltip("Oda temizlenince açılacak çıkış kapısı.")]
    private RoomDoor door;

    private readonly List<EnemyHealth> alive = new List<EnemyHealth>();
    private GameObject spawnedChest;
    private bool active;

    public string RoomName => roomName;
    public bool IsBossRoom => bossRoom;
    public Transform PlayerEntry => playerEntry != null ? playerEntry : transform;
    public Transform ChestSpawn => chestSpawn;
    public RoomDoor Door => door;
    public int AliveCount => alive.Count;
    public bool IsActive => active;

    /// <summary>Odadaki tüm düşmanlar ölünce bir kez tetiklenir.</summary>
    public event Action<DungeonRoom> OnCleared;

    /// <summary>
    /// Odayı sıfırdan canlandırır: eski her şeyi siler, kapıyı kapatır ve
    /// havuzdan dalgayı doğurur. extraEnemies koşu ilerledikçe gelen ektir.
    /// </summary>
    public void Activate(PlayerHealth target, int extraEnemies)
    {
        Deactivate();

        active = true;
        if (door != null) door.SetOpen(false);

        SpawnWave(target, extraEnemies);

        // Havuz boşsa (prefab atanmamışsa) oda anında temizlenmiş sayılır.
        if (alive.Count == 0) Clear();
    }

    /// <summary>Odayı boşaltır: düşmanları ve alınmamış sandığı siler.</summary>
    public void Deactivate()
    {
        active = false;

        if (spawnedChest != null)
        {
            Destroy(spawnedChest);
            spawnedChest = null;
        }

        for (int i = alive.Count - 1; i >= 0; i--)
        {
            EnemyHealth health = alive[i];
            if (health == null) continue;

            health.OnDeath -= HandleEnemyDeath;
            Destroy(health.gameObject);
        }

        alive.Clear();
    }

    private void SpawnWave(PlayerHealth target, int extraEnemies)
    {
        if (bossRoom)
        {
            SpawnBoss(target);
            return;
        }

        int extra = Mathf.Max(0, extraEnemies);
        int low = Mathf.Max(1, minEnemies + extra);
        int high = Mathf.Max(low, maxEnemies + extra);
        int count = UnityEngine.Random.Range(low, high + 1);

        List<Transform> points = ShuffledSpawnPoints();

        for (int i = 0; i < count; i++)
        {
            bool chaser = UnityEngine.Random.value < chaserWeight;

            GameObject prefab = chaser ? chaserPrefab : shooterPrefab;
            if (prefab == null) prefab = chaser ? shooterPrefab : chaserPrefab;
            if (prefab == null) continue;

            Vector3 position = points.Count > 0 ? points[i % points.Count].position : transform.position;

            // Nokta sayısı yetmezse üst üste binmesinler.
            if (points.Count > 0 && i >= points.Count)
            {
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * 1.8f;
                position += new Vector3(jitter.x, 0f, jitter.y);
            }

            Register(Instantiate(prefab, position, Quaternion.identity), target);
        }
    }

    private void SpawnBoss(PlayerHealth target)
    {
        if (bossPrefab == null) return;

        Vector3 position = enemySpawns != null && enemySpawns.Length > 0 && enemySpawns[0] != null
            ? enemySpawns[0].position
            : transform.position;

        Register(Instantiate(bossPrefab, position, Quaternion.identity), target);
    }

    private void Register(GameObject go, PlayerHealth target)
    {
        if (go == null) return;

        if (go.TryGetComponent(out EnemyHealth health))
        {
            health.OnDeath += HandleEnemyDeath;
            alive.Add(health);
        }

        if (go.TryGetComponent(out ChaserEnemy chase)) chase.SetTarget(target);
        if (go.TryGetComponent(out ShooterEnemy shoot)) shoot.SetTarget(target);
        if (go.TryGetComponent(out BossEnemy boss)) boss.SetTarget(target);
    }

    private List<Transform> ShuffledSpawnPoints()
    {
        List<Transform> points = new List<Transform>();
        if (enemySpawns == null) return points;

        for (int i = 0; i < enemySpawns.Length; i++)
        {
            if (enemySpawns[i] != null) points.Add(enemySpawns[i]);
        }

        for (int i = points.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (points[i], points[j]) = (points[j], points[i]);
        }

        return points;
    }

    private void HandleEnemyDeath(EnemyHealth health)
    {
        if (health != null) health.OnDeath -= HandleEnemyDeath;
        alive.Remove(health);

        if (!active || alive.Count > 0) return;
        Clear();
    }

    /// <summary>Oda temizlendi: kapı açılır ve sandık belirir.</summary>
    private void Clear()
    {
        active = false;

        if (door != null) door.SetOpen(true);

        SpawnChest();

        if (PrototipHud.Instance != null)
        {
            PrototipHud.Instance.ShowMessage("Oda temizlendi — sandık belirdi, kapı açıldı", 2.2f);
        }

        OnCleared?.Invoke(this);
    }

    /// <summary>Sandığı belirme efektiyle ortaya çıkarır.</summary>
    private void SpawnChest()
    {
        if (chestPrefab == null || chestSpawn == null) return;
        if (spawnedChest != null) return;

        spawnedChest = Instantiate(chestPrefab, chestSpawn.position, Quaternion.identity);

        if (Fx.Instance != null) Fx.Instance.ChestAppear(chestSpawn.position);
    }
}
