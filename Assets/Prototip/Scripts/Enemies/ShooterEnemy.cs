using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Mermi atan düşman: oyuncuyla belirli bir mesafe aralığını korumaya çalışır,
/// düzenli aralıklarla kısa bir hazırlık işaretinden sonra yavaş bir mermi atar.
/// Mermi kaçınılabilir olacak kadar yavaştır.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class ShooterEnemy : MonoBehaviour
{
    [Header("Hareket")]
    [SerializeField, Tooltip("Yürüme hızı (birim/saniye).")]
    private float moveSpeed = 3f;
    [SerializeField, Tooltip("Oyuncuya bundan yakınsa geri çekilir (birim).")]
    private float minDistance = 7f;
    [SerializeField, Tooltip("Oyuncuya bundan uzaksa yaklaşır (birim).")]
    private float maxDistance = 10f;
    [SerializeField, Tooltip("Geri çekilirken bir seferde denenen kaçış mesafesi (birim).")]
    private float retreatStep = 3.5f;

    [Header("Ateş")]
    [SerializeField, Tooltip("İki atış arasındaki süre (saniye).")]
    private float fireInterval = 2f;
    [SerializeField, Tooltip("Atıştan önceki hazırlık işareti süresi (saniye).")]
    private float telegraphDuration = 0.3f;
    [SerializeField, Tooltip("Merminin çıkış yüksekliği (birim).")]
    private float muzzleHeight = 1.2f;
    [SerializeField, Tooltip("Merminin gövdeden ileri çıkış kayması (birim).")]
    private float muzzleForward = 0.9f;

    [Header("Mermi")]
    [SerializeField, Tooltip("Atılacak mermi prefabı.")]
    private GameObject projectilePrefab;
    [SerializeField, Tooltip("Mermi hızı (birim/saniye).")]
    private float projectileSpeed = 9f;
    [SerializeField, Tooltip("Mermi hasarı.")]
    private int projectileDamage = 1;

    private NavMeshAgent agent;
    private EnemyHealth health;
    private PlayerHealth playerHealth;
    private Transform playerTransform;

    private float fireTimer;
    private float telegraphLeft = -1f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
    }

    private void Start()
    {
        agent.speed = moveSpeed;
        // Dönüşü kendimiz yönetiyoruz; geri çekilirken de oyuncuya bakmalı.
        agent.updateRotation = false;

        fireTimer = Random.Range(0f, fireInterval * 0.5f);
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

        FacePlayer();
        UpdatePositioning();
        UpdateFiring();
    }

    /// <summary>Mesafe koruma: çok uzaksa yaklaş, çok yakınsa uzaklaş, aradaysa dur.</summary>
    private void UpdatePositioning()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        float distance = HorizontalDistanceToPlayer();

        if (distance > maxDistance)
        {
            agent.isStopped = false;
            agent.SetDestination(playerTransform.position);
            return;
        }

        if (distance < minDistance)
        {
            Vector3 away = transform.position - playerTransform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = transform.forward;

            Vector3 goal = transform.position + away.normalized * retreatStep;
            if (NavMesh.SamplePosition(goal, out NavMeshHit hit, retreatStep, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
            }
            return;
        }

        agent.isStopped = true;
        agent.ResetPath();
    }

    private void UpdateFiring()
    {
        // Hazırlık işareti sürüyor.
        if (telegraphLeft >= 0f)
        {
            telegraphLeft -= Time.deltaTime;
            if (telegraphLeft > 0f) return;

            telegraphLeft = -1f;
            health.SetTelegraph(false);
            Fire();
            return;
        }

        fireTimer += Time.deltaTime;
        if (fireTimer < fireInterval) return;

        fireTimer = 0f;
        telegraphLeft = telegraphDuration;
        health.SetTelegraph(true);
    }

    private void Fire()
    {
        if (projectilePrefab == null || playerTransform == null) return;

        Vector3 dir = (playerTransform.position + Vector3.up * muzzleHeight) - (transform.position + Vector3.up * muzzleHeight);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        Vector3 muzzle = transform.position + Vector3.up * muzzleHeight + dir * muzzleForward;

        GameObject go = Instantiate(projectilePrefab, muzzle, Quaternion.LookRotation(dir, Vector3.up));
        if (go.TryGetComponent(out EnemyProjectile projectile)) projectile.Setup(dir, projectileSpeed, projectileDamage);
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
}
