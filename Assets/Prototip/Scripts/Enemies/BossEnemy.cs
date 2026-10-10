using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Zindan Bekçisi. İki saldırısı var ve her ikisinin öncesinde okunur bir uyarı
/// verir; canı yarıya inince öfkelenir.
///
///   Hücum        : oyuncuya döner, yerde çizgiyle yönünü gösterir, sonra o
///                  çizgi boyunca atılır. Çarparsa 1 can götürür, duvara
///                  çarparsa sersemler (zar atma fırsatı).
///   Mermi çemberi: durur, parlar, sonra her yöne mermi saçar.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class BossEnemy : MonoBehaviour
{
    private enum Phase
    {
        Approach,
        ChargeTelegraph,
        Charging,
        Stunned,
        BurstTelegraph,
        Recover
    }

    /// <summary>Sahnede yaşayan boss; HUD can çubuğu bunu okur.</summary>
    public static BossEnemy Active { get; private set; }

    [Header("Tanım")]
    [SerializeField, Tooltip("HUD'da görünecek ad.")]
    private string bossName = "Zindan Bekçisi";

    [Header("Hareket")]
    [SerializeField, Tooltip("Normal yürüme hızı (birim/saniye).")]
    private float moveSpeed = 2.6f;
    [SerializeField, Tooltip("Oyuncuya bu mesafeye gelince saldırı seçer (birim).")]
    private float engageRange = 12f;

    [Header("Saldırı Döngüsü")]
    [SerializeField, Tooltip("İki saldırı arasındaki bekleme (saniye).")]
    private float attackInterval = 2.6f;
    [SerializeField, Tooltip("Öfkeliyken bekleme çarpanı. 0.6 = %40 daha sık.")]
    private float enragedIntervalFactor = 0.6f;

    [Header("Hücum")]
    [SerializeField, Tooltip("Yön çizgisinin ekranda kaldığı uyarı süresi (saniye).")]
    private float chargeTelegraph = 0.8f;
    [SerializeField, Tooltip("Hücum hızı (birim/saniye).")]
    private float chargeSpeed = 22f;
    [SerializeField, Tooltip("Hücumun azami mesafesi (birim).")]
    private float chargeDistance = 18f;
    [SerializeField, Tooltip("Hücumun oyuncuya değme yarıçapı (birim).")]
    private float chargeHitRadius = 1.6f;
    [SerializeField, Tooltip("Hücum hasarı (can).")]
    private int chargeDamage = 1;
    [SerializeField, Tooltip("Duvara çarpınca sersemleme süresi (saniye).")]
    private float stunDuration = 1.6f;

    [Header("Mermi Çemberi")]
    [SerializeField, Tooltip("Parlama uyarısının süresi (saniye).")]
    private float burstTelegraph = 0.6f;
    [SerializeField, Tooltip("Her yöne atılan mermi sayısı.")]
    private int burstCount = 10;
    [SerializeField, Tooltip("Mermi hızı (birim/saniye). Aralarından geçilebilmeli.")]
    private float burstProjectileSpeed = 8f;
    [SerializeField, Tooltip("Mermi hasarı (can).")]
    private int burstProjectileDamage = 1;
    [SerializeField, Tooltip("Mermilerin çıkış yüksekliği (birim).")]
    private float muzzleHeight = 1.8f;

    [Header("Öfke")]
    [SerializeField, Range(0f, 1f), Tooltip("Bu can oranının altına inince öfkelenir.")]
    private float enrageThreshold = 0.5f;
    [SerializeField, Tooltip("Öfkelenince bir kez çağrılan Kovalayan sayısı.")]
    private int summonCount = 2;
    [SerializeField, Tooltip("Çağrılan düşmanların boss'tan uzaklığı (birim).")]
    private float summonRadius = 4f;

    [Header("Referanslar")]
    [SerializeField, Tooltip("Hücum yönünü gösteren yer çizgisi.")]
    private LineRenderer chargeLine;
    [SerializeField, Tooltip("Atılacak mermi prefabı.")]
    private GameObject projectilePrefab;
    [SerializeField, Tooltip("Öfkelenince çağrılacak Kovalayan prefabı.")]
    private GameObject summonPrefab;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private readonly List<EnemyHealth> summoned = new List<EnemyHealth>();

    private NavMeshAgent agent;
    private EnemyHealth health;
    private PlayerHealth playerHealth;
    private Transform playerTransform;

    private Phase phase = Phase.Approach;
    private float phaseTimer;
    private float attackTimer;
    private Vector3 chargeDirection = Vector3.forward;
    private float chargeTravelled;
    private bool enraged;
    private bool chargeHitPlayer;

    public string BossName => bossName;
    public EnemyHealth Health => health;
    public bool IsEnraged => enraged;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();

        if (chargeLine != null)
        {
            chargeLine.useWorldSpace = true;
            chargeLine.positionCount = 2;
            chargeLine.enabled = false;
        }
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }

    private void OnDestroy()
    {
        // Boss ölünce ya da oda boşalınca çağrılan düşmanlar da gider.
        KillSummoned();
    }

    private void Start()
    {
        agent.speed = moveSpeed;
        agent.updateRotation = false;

        attackTimer = attackInterval * 0.5f;

        if (playerHealth == null) SetTarget(FindFirstObjectByType<PlayerHealth>());
    }

    /// <summary>Oda boss'u doğururken hedefi buradan verir.</summary>
    public void SetTarget(PlayerHealth target)
    {
        playerHealth = target;
        playerTransform = target != null ? target.transform : null;
    }

    private void Update()
    {
        if (GamePause.Paused) return;
        if (health == null || !health.IsAlive) return;

        if (playerTransform == null)
        {
            SetTarget(FindFirstObjectByType<PlayerHealth>());
            if (playerTransform == null) return;
        }

        CheckEnrage();

        switch (phase)
        {
            case Phase.Approach:
                UpdateApproach();
                break;
            case Phase.ChargeTelegraph:
                UpdateChargeTelegraph();
                break;
            case Phase.Charging:
                UpdateCharging();
                break;
            case Phase.Stunned:
                UpdateStunned();
                break;
            case Phase.BurstTelegraph:
                UpdateBurstTelegraph();
                break;
            case Phase.Recover:
                UpdateRecover();
                break;
        }
    }

    // --- Evreler ---------------------------------------------------------

    private void UpdateApproach()
    {
        FacePlayer();
        SetAgentStopped(false);

        if (agent.enabled && agent.isOnNavMesh) agent.SetDestination(playerTransform.position);

        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f) return;

        if (HorizontalDistanceToPlayer() > engageRange) return;

        // Hücum ve mermi çemberi dönüşümlü değil, rastgele seçilir.
        if (Random.value < 0.55f) BeginChargeTelegraph();
        else BeginBurstTelegraph();
    }

    private void BeginChargeTelegraph()
    {
        phase = Phase.ChargeTelegraph;
        phaseTimer = 0f;

        SetAgentStopped(true);
        if (health != null) health.SetTelegraph(true);
    }

    private void UpdateChargeTelegraph()
    {
        FacePlayer();

        // Uyarı boyunca yön oyuncuyu takip eder, son anda kilitlenir.
        Vector3 d = playerTransform.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.0001f) chargeDirection = d.normalized;

        DrawChargeLine(true);

        phaseTimer += Time.deltaTime;
        if (phaseTimer < chargeTelegraph) return;

        phase = Phase.Charging;
        chargeTravelled = 0f;
        chargeHitPlayer = false;

        DrawChargeLine(false);
        if (health != null) health.SetTelegraph(false);

        // Hücum sırasında konumu kendimiz sürüyoruz.
        if (agent.enabled) agent.enabled = false;
    }

    private void UpdateCharging()
    {
        float step = chargeSpeed * Time.deltaTime;

        // Oyuncuya değdi mi?
        if (!chargeHitPlayer && HorizontalDistanceToPlayer() <= chargeHitRadius)
        {
            chargeHitPlayer = true;
            if (playerHealth != null) playerHealth.TakeDamage(chargeDamage);
        }

        // Duvara çarptı mı?
        if (HitsWall(step))
        {
            BeginStun();
            return;
        }

        transform.position += chargeDirection * step;
        chargeTravelled += step;

        if (chargeTravelled >= chargeDistance) EndCharge();
    }

    private bool HitsWall(float distance)
    {
        int count = Physics.SphereCastNonAlloc(
            transform.position + Vector3.up, 1.1f, chargeDirection, hitBuffer, distance,
            ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider c = hitBuffer[i].collider;
            if (c == null) continue;
            if (c.transform.IsChildOf(transform)) continue;

            // Düşman ve oyuncu duvar sayılmaz.
            if (c.GetComponentInParent<EnemyHealth>() != null) continue;
            if (c.GetComponentInParent<PlayerHealth>() != null) continue;

            return true;
        }

        return false;
    }

    private void BeginStun()
    {
        phase = Phase.Stunned;
        phaseTimer = 0f;

        if (health != null) health.SetStunned(true);
        if (Fx.Instance != null) Fx.Instance.HitBurst(transform.position + Vector3.up * muzzleHeight, true);
        if (Fx.Instance != null) Fx.Instance.ShakeCamera(0.45f, 0.25f);
    }

    private void UpdateStunned()
    {
        phaseTimer += Time.deltaTime;
        if (phaseTimer < stunDuration) return;

        if (health != null) health.SetStunned(false);
        EndCharge();
    }

    private void EndCharge()
    {
        RestoreAgent();

        phase = Phase.Recover;
        phaseTimer = 0f;
    }

    private void BeginBurstTelegraph()
    {
        phase = Phase.BurstTelegraph;
        phaseTimer = 0f;

        SetAgentStopped(true);
        if (health != null) health.SetTelegraph(true);
    }

    private void UpdateBurstTelegraph()
    {
        FacePlayer();

        phaseTimer += Time.deltaTime;
        if (phaseTimer < burstTelegraph) return;

        if (health != null) health.SetTelegraph(false);
        FireBurst();

        phase = Phase.Recover;
        phaseTimer = 0f;
    }

    private void UpdateRecover()
    {
        SetAgentStopped(true);
        FacePlayer();

        phaseTimer += Time.deltaTime;
        if (phaseTimer < 0.35f) return;

        phase = Phase.Approach;
        attackTimer = CurrentAttackInterval;
    }

    // --- Saldırılar ------------------------------------------------------

    private void FireBurst()
    {
        if (projectilePrefab == null) return;

        int count = Mathf.Max(3, burstCount);
        Vector3 muzzle = transform.position + Vector3.up * muzzleHeight;

        // Çember her seferinde biraz kaysın, desen tahmin edilemesin.
        float offset = Random.Range(0f, 360f / count);

        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i + offset;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            GameObject go = Instantiate(projectilePrefab, muzzle + dir * 1.4f, Quaternion.LookRotation(dir, Vector3.up));
            if (go.TryGetComponent(out EnemyProjectile projectile))
            {
                projectile.Setup(dir, burstProjectileSpeed, burstProjectileDamage);
            }
        }

        if (Fx.Instance != null) Fx.Instance.HitBurst(muzzle, true);
    }

    private void CheckEnrage()
    {
        if (enraged || health == null) return;

        float fraction = health.Max > 0 ? (float)health.Current / health.Max : 1f;
        if (fraction > enrageThreshold) return;

        enraged = true;

        health.SetEnraged(true);
        Summon();

        if (Fx.Instance != null)
        {
            Fx.Instance.ShakeCamera(0.6f, 0.4f);
            Fx.Instance.HitBurst(transform.position + Vector3.up * muzzleHeight, true);
        }

        if (PrototipHud.Instance != null) PrototipHud.Instance.ShowMessage(bossName + " öfkelendi!", 2f);
    }

    private void Summon()
    {
        if (summonPrefab == null || summonCount <= 0) return;

        for (int i = 0; i < summonCount; i++)
        {
            float angle = (360f / summonCount) * i + 45f;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * summonRadius;
            Vector3 position = transform.position + offset;

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, summonRadius, NavMesh.AllAreas))
            {
                position = hit.position;
            }

            GameObject go = Instantiate(summonPrefab, position, Quaternion.identity);

            if (go.TryGetComponent(out ChaserEnemy chase)) chase.SetTarget(playerHealth);
            if (go.TryGetComponent(out EnemyHealth summonHealth)) summoned.Add(summonHealth);
        }
    }

    /// <summary>Boss gidince çağrılan düşmanlar da ölür.</summary>
    private void KillSummoned()
    {
        for (int i = summoned.Count - 1; i >= 0; i--)
        {
            EnemyHealth e = summoned[i];
            if (e == null) continue;

            if (Fx.Instance != null) Fx.Instance.DeathBurst(e.HeadPoint, e.BaseColor);
            Destroy(e.gameObject);
        }

        summoned.Clear();
    }

    // --- Yardımcılar -----------------------------------------------------

    private float CurrentAttackInterval
    {
        get
        {
            float interval = Mathf.Max(0.3f, attackInterval);
            if (enraged) interval *= Mathf.Clamp(enragedIntervalFactor, 0.2f, 1f);
            return interval;
        }
    }

    private void DrawChargeLine(bool visible)
    {
        if (chargeLine == null) return;

        if (!visible)
        {
            chargeLine.enabled = false;
            return;
        }

        Vector3 start = transform.position + Vector3.up * 0.1f;
        Vector3 end = start + chargeDirection * chargeDistance;

        chargeLine.enabled = true;
        chargeLine.positionCount = 2;
        chargeLine.SetPosition(0, start);
        chargeLine.SetPosition(1, end);
    }

    private float HorizontalDistanceToPlayer()
    {
        Vector3 d = playerTransform.position - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    private void FacePlayer()
    {
        Vector3 d = playerTransform.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
    }

    private void SetAgentStopped(bool stopped)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        if (stopped && !agent.isStopped) agent.ResetPath();
        agent.isStopped = stopped;
    }

    /// <summary>Hücum bitti: ajanı NavMesh üzerinde güvenle geri açar.</summary>
    private void RestoreAgent()
    {
        if (agent == null) return;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
        }

        agent.enabled = true;
        if (agent.isOnNavMesh) agent.ResetPath();
    }
}
