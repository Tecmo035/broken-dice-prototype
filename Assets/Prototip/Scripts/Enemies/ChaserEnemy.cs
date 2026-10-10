using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Kovalayan düşman: NavMeshAgent ile oyuncuyu kovalar, yaklaşınca kısa süre
/// hazırlanır (rengiyle haber verir), sonra oyuncu hâlâ menzildeyse vurur.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class ChaserEnemy : MonoBehaviour
{
    [Header("Hareket")]
    [SerializeField, Tooltip("Kovalama hızı (birim/saniye).")]
    private float moveSpeed = 3.4f;

    [Header("Saldırı")]
    [SerializeField, Tooltip("Bu mesafeye girince saldırı hazırlığı başlar (birim).")]
    private float attackRange = 1.6f;
    [SerializeField, Tooltip("Hazırlanma süresi (saniye). Bu sürede renk değişir.")]
    private float windupDuration = 0.4f;
    [SerializeField, Tooltip("Hazırlık bitince oyuncu bu mesafedeyse vuruş isabet eder (birim).")]
    private float attackReach = 2f;
    [SerializeField, Tooltip("Verilen hasar.")]
    private int damage = 1;
    [SerializeField, Tooltip("Saldırı sonrası bekleme (saniye).")]
    private float attackCooldown = 1f;

    private NavMeshAgent agent;
    private EnemyHealth health;
    private PlayerHealth playerHealth;
    private Transform playerTransform;

    private float windupLeft = -1f;
    private float cooldownLeft;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
    }

    private void Start()
    {
        agent.speed = moveSpeed;
        if (playerHealth == null) SetTarget(FindFirstObjectByType<PlayerHealth>());
    }

    /// <summary>Oda yöneticisi düşmanı doğurduğunda hedefi buradan verir.</summary>
    public void SetTarget(PlayerHealth target)
    {
        playerHealth = target;
        playerTransform = target != null ? target.transform : null;
    }

    private void Update()
    {
        if (health == null || !health.IsAlive) return;

        if (playerTransform == null)
        {
            SetTarget(FindFirstObjectByType<PlayerHealth>());
            if (playerTransform == null) return;
        }

        if (cooldownLeft > 0f) cooldownLeft -= Time.deltaTime;

        float distance = HorizontalDistanceToPlayer();

        // Hazırlık sürüyor: yerinde dur, oyuncuya bak, süre bitince vur.
        if (windupLeft >= 0f)
        {
            StopAgent(true);
            FacePlayer();

            windupLeft -= Time.deltaTime;
            if (windupLeft > 0f) return;

            windupLeft = -1f;
            health.SetTelegraph(false);
            cooldownLeft = attackCooldown;

            if (distance <= attackReach && playerHealth != null) playerHealth.TakeDamage(damage);
            return;
        }

        // Yaklaştı ve bekleme bitti: hazırlığı başlat.
        if (distance <= attackRange && cooldownLeft <= 0f)
        {
            windupLeft = windupDuration;
            health.SetTelegraph(true);
            StopAgent(true);
            return;
        }

        StopAgent(false);
        if (agent.enabled && agent.isOnNavMesh) agent.SetDestination(playerTransform.position);
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

    /// <summary>NavMesh üzerinde olmayan bir ajana komut vermek uyarı ürettiği için korumalı.</summary>
    private void StopAgent(bool stopped)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        if (stopped && !agent.isStopped) agent.ResetPath();
        agent.isStopped = stopped;
    }
}
