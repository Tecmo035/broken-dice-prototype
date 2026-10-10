using UnityEngine;

/// <summary>
/// Düşman mermisi. Düz çizgide gider, oyuncuya değerse hasar verir,
/// duvara çarpınca ya da süresi dolunca yok olur. Diğer düşmanlardan geçer.
/// </summary>
public class EnemyProjectile : MonoBehaviour
{
    [Header("Uçuş")]
    [SerializeField, Tooltip("Hız (birim/saniye).")]
    private float speed = 9f;
    [SerializeField, Tooltip("Çarpışma yarıçapı (birim).")]
    private float hitRadius = 0.25f;
    [SerializeField, Tooltip("Hiçbir şeye çarpmazsa yok olma süresi (saniye).")]
    private float lifetime = 5f;

    [Header("Hasar")]
    [SerializeField, Tooltip("Oyuncuya verilen hasar.")]
    private int damage = 1;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    private Vector3 direction = Vector3.forward;
    private float age;
    private bool spent;

    /// <summary>Doğduktan hemen sonra yön, hız ve hasar buradan verilir.</summary>
    public void Setup(Vector3 newDirection, float newSpeed, int newDamage)
    {
        newDirection.y = 0f;
        if (newDirection.sqrMagnitude > 0.0001f) direction = newDirection.normalized;

        speed = newSpeed;
        damage = newDamage;

        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private void Update()
    {
        if (spent) return;

        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Despawn();
            return;
        }

        float step = speed * Time.deltaTime;
        if (step <= 0f) return;

        if (TryCast(step, out PlayerHealth player, out bool blocked, out Vector3 point))
        {
            if (player != null)
            {
                transform.position = point;
                player.TakeDamage(damage);
                if (Fx.Instance != null) Fx.Instance.HitBurst(point, false);
                Despawn();
                return;
            }

            if (blocked)
            {
                transform.position = point;
                Despawn();
                return;
            }
        }

        transform.position += direction * step;
    }

    /// <summary>
    /// Yoldaki en yakın engeli bulur. Düşmanlar atlanır (mermi onlardan geçer),
    /// oyuncu bulunursa player dolu döner, başka bir şeyse blocked true olur.
    /// </summary>
    private bool TryCast(float distance, out PlayerHealth player, out bool blocked, out Vector3 point)
    {
        player = null;
        blocked = false;
        point = transform.position;

        int count = Physics.SphereCastNonAlloc(
            transform.position, hitRadius, direction, hitBuffer, distance, ~0, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (hit.collider == null) continue;
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.distance > nearest) continue;

            // Düşmanlar mermiyi engellemez.
            if (hit.collider.GetComponentInParent<EnemyHealth>() != null) continue;

            nearest = hit.distance;
            point = transform.position + direction * hit.distance;
            player = hit.collider.GetComponentInParent<PlayerHealth>();
            blocked = player == null;
            found = true;
        }

        return found;
    }

    private void Despawn()
    {
        spent = true;
        Destroy(gameObject);
    }
}
