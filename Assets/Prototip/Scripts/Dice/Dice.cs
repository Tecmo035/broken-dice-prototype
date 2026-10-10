using UnityEngine;

/// <summary>Zarın bulunduğu durum.</summary>
public enum DiceState
{
    /// <summary>Elde: atılmaya hazır.</summary>
    Held,
    /// <summary>Uçuyor: düz çizgide ilk düşmana ya da duvara kadar.</summary>
    Flying,
    /// <summary>Düşmanın başında dönüyor: sonuç henüz belli değil.</summary>
    Rolling,
    /// <summary>Zincir sıçraması: başka bir düşmana güdümlü gidiyor.</summary>
    Chaining,
    /// <summary>Geri dönüyor: oyuncuya güdümlü uçuyor.</summary>
    Returning
}

/// <summary>
/// Oyuncunun silahı olan zarın TEK bir tanesi. Birden fazla zar olabilir ve
/// her biri kendi durum makinesini bağımsız yürütür; hepsi ayrı ayrı geri gelir.
///
/// Girdi okumak, menzil çizgisi çizmek ve hasar hesabı bu sınıfın işi değil:
/// DiceManager sol tıkı okur, AimIndicator çizgiyi çizer, CombatModifiers
/// hasarı hesaplar. Hasarın UYGULANMASI da sonuç göstergesi düşmana değdiğinde
/// DiceResultDisplay tarafından yapılır.
/// </summary>
public class Dice : MonoBehaviour
{
    [Header("Fırlatma")]
    [SerializeField, Tooltip("Gidiş hızı (birim/saniye).")]
    private float throwSpeed = 26f;
    [SerializeField, Tooltip("Azami menzil (birim). Bu mesafede düşman yoksa ıskalar.")]
    private float range = 12f;
    [SerializeField, Tooltip("Çarpışma yarıçapı; büyük değer nişanı bağışlayıcı yapar.")]
    private float hitRadius = 0.35f;

    [Header("Dönme")]
    [SerializeField, Tooltip("Düşmanın başında dönme süresi (saniye).")]
    private float rollDuration = 0.8f;
    [SerializeField, Tooltip("Dönerken takla hızı (derece/saniye).")]
    private float rollSpinSpeed = 900f;
    [SerializeField, Tooltip("Aynı düşmanda birden fazla zar varken yana kaçma yarıçapı.")]
    private float rollSpreadRadius = 0.5f;
    [SerializeField, Tooltip("Aynı düşmanda birden fazla zar varken üst üste binmemesi için yükseklik artışı.")]
    private float rollSpreadHeight = 0.34f;

    [Header("Geri Dönüş")]
    [SerializeField, Tooltip("Oyuncuya dönüş hızı (birim/saniye).")]
    private float returnSpeed = 30f;
    [SerializeField, Tooltip("Bu yarıçapa girince zar yakalanır (birim).")]
    private float catchRadius = 1f;

    [Header("Zincir Sıçraması")]
    [SerializeField, Tooltip("Sıçrama hızı (birim/saniye).")]
    private float chainSpeed = 24f;
    [SerializeField, Tooltip("Hedefe bu kadar yaklaşınca dönmeye başlar (birim).")]
    private float chainArriveRadius = 0.8f;

    [Header("His")]
    [SerializeField, Tooltip("Normal vuruşta kamera sarsıntı şiddeti.")]
    private float normalShake = 0.12f;
    [SerializeField, Tooltip("Normal vuruşta sarsıntı süresi (saniye).")]
    private float normalShakeTime = 0.12f;
    [SerializeField, Tooltip("Kritikte kamera sarsıntı şiddeti.")]
    private float critShake = 0.65f;
    [SerializeField, Tooltip("Kritikte sarsıntı süresi (saniye).")]
    private float critShakeTime = 0.3f;
    [SerializeField, Tooltip("Kritikte zaman ölçeği (1 = yavaşlama yok).")]
    private float hitStopScale = 0.04f;
    [SerializeField, Tooltip("Kritikte zaman yavaşlaması süresi (gerçek saniye).")]
    private float hitStopDuration = 0.11f;

    [Header("Görsel")]
    [SerializeField, Tooltip("Uçarken takla hızı (derece/saniye).")]
    private float flightSpinSpeed = 540f;
    [SerializeField, Tooltip("Eldeyken yavaş dönüş hızı (derece/saniye).")]
    private float idleSpinSpeed = 60f;

    [Header("Referanslar")]
    [SerializeField, Tooltip("Zarı fırlatan oyuncu.")]
    private PlayerController player;
    [SerializeField, Tooltip("Zarın arkasındaki iz.")]
    private TrailRenderer trail;
    [SerializeField, Tooltip("Sonucu gösteren çerçeve prefabı (slot makinesi).")]
    private GameObject resultDisplayPrefab;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[24];

    private DiceState state = DiceState.Held;
    private EnemyHealth targetEnemy;
    private DiceResultDisplay display;
    private Vector3 flightDirection = Vector3.forward;
    private float travelled;
    private float rollTimer;
    private float currentRollDuration;
    private int bouncesLeft;
    private int chainsLeft;
    private int rollSlot;
    private int lastResult;
    private bool secondChanceUsed;

    /// <summary>Zarın mevcut durumu.</summary>
    public DiceState State => state;

    /// <summary>Elde mi? (atılabilir mi)</summary>
    public bool IsHeld => state == DiceState.Held;

    /// <summary>En son gelen zar sonucu.</summary>
    public int LastResult => lastResult;

    /// <summary>Menzil; menzil göstergesi bu değeri okur.</summary>
    public float Range => range;

    /// <summary>Dönerken üstünde durduğu düşman; dönmüyorsa null.</summary>
    public EnemyHealth RollingTarget => state == DiceState.Rolling ? targetEnemy : null;

    /// <summary>
    /// Eldeyken göğüs noktasına eklenen kayma. DiceManager her kare atar;
    /// böylece birden fazla zar üst üste binmez.
    /// </summary>
    public Vector3 HoldOffset { get; set; }

    /// <summary>Oyuncuyu bağlar (havuz zarı doğurduğunda çağrılır).</summary>
    public void SetPlayer(PlayerController owner)
    {
        player = owner;
    }

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (trail == null) trail = GetComponent<TrailRenderer>();
        ResetToHand();
    }

    private void OnDestroy()
    {
        CancelDisplay();
    }

    private void Update()
    {
        if (GamePause.Paused) return;

        switch (state)
        {
            case DiceState.Held:
                UpdateHeld();
                break;
            case DiceState.Flying:
                UpdateFlying();
                break;
            case DiceState.Rolling:
                UpdateRolling();
                break;
            case DiceState.Chaining:
                UpdateChaining();
                break;
            case DiceState.Returning:
                UpdateReturning();
                break;
        }
    }

    // --- Durumlar -------------------------------------------------------

    private void UpdateHeld()
    {
        transform.position = HoldPoint().position + HoldOffset;
        transform.Rotate(Vector3.up, idleSpinSpeed * Time.deltaTime, Space.Self);
    }

    private void UpdateFlying()
    {
        float dt = Time.deltaTime;

        ApplyHoming(dt);

        float step = CurrentThrowSpeed * dt;

        if (TryCast(transform.position, flightDirection, step,
                out EnemyHealth enemy, out Vector3 point, out Vector3 normal))
        {
            if (enemy != null)
            {
                BeginRoll(enemy);
                return;
            }

            // Duvara çarptı. Sekme buff'ı varsa bir kez yansıyıp yoluna devam eder.
            if (bouncesLeft > 0 && TryBounce(point, normal)) return;

            transform.position = point;
            BeginReturn();
            return;
        }

        transform.position += flightDirection * step;
        travelled += step;
        Tumble(flightSpinSpeed);

        // Menzil bitti: ıskaladı.
        if (travelled >= range) BeginReturn();
    }

    private void UpdateRolling()
    {
        // Dönme sırasında düşman ölürse zar hemen geri döner.
        if (targetEnemy == null || !targetEnemy.IsAlive)
        {
            CancelDisplay();
            BeginReturn();
            return;
        }

        // Düşman hareket etse de zar başının üstünü takip eder.
        Vector3 anchor = targetEnemy.HeadPoint + RollOffset();
        transform.position = anchor;
        Tumble(rollSpinSpeed);

        if (display != null) display.UpdateAnchor(anchor + Vector3.up * 0.55f);

        rollTimer += Time.deltaTime;
        if (rollTimer >= currentRollDuration) Resolve();
    }

    private void UpdateChaining()
    {
        if (targetEnemy == null || !targetEnemy.IsAlive)
        {
            BeginReturn();
            return;
        }

        Vector3 destination = targetEnemy.HeadPoint;
        Vector3 toTarget = destination - transform.position;
        float distance = toTarget.magnitude;

        if (distance <= chainArriveRadius)
        {
            BeginRoll(targetEnemy);
            return;
        }

        Vector3 dir = toTarget / Mathf.Max(distance, 0.0001f);
        transform.position += dir * (chainSpeed * Time.deltaTime);
        Tumble(flightSpinSpeed);
    }

    private void UpdateReturning()
    {
        Vector3 holdPosition = HoldPoint().position + HoldOffset;
        Vector3 toHand = holdPosition - transform.position;
        float distance = toHand.magnitude;

        if (distance <= catchRadius)
        {
            Catch();
            return;
        }

        Vector3 dir = toHand / Mathf.Max(distance, 0.0001f);
        transform.position += dir * (CurrentReturnSpeed * Time.deltaTime);
        Tumble(flightSpinSpeed);
    }

    // --- Geçişler -------------------------------------------------------

    /// <summary>
    /// Zarı verilen yöne fırlatır. Yalnızca zar eldeyken çalışır.
    /// DiceManager çağırır; her sol tık yalnızca BİR zarı atar.
    /// </summary>
    public bool Throw(Vector3 direction)
    {
        if (state != DiceState.Held) return false;

        transform.position = HoldPoint().position + HoldOffset;

        Vector3 dir = direction;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;

        flightDirection = dir.normalized;
        travelled = 0f;
        targetEnemy = null;
        secondChanceUsed = false;

        CombatModifiers mods = CombatModifiers.Instance;
        bouncesLeft = mods != null ? mods.BounceAllowance : 0;
        chainsLeft = mods != null ? mods.ChainAllowance : 0;

        state = DiceState.Flying;
        SetTrail(true);
        return true;
    }

    /// <summary>Sekme: duvar normalinden yansıyıp kalan menzille yoluna devam eder.</summary>
    private bool TryBounce(Vector3 point, Vector3 normal)
    {
        Vector3 flat = normal;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) return false;
        flat.Normalize();

        Vector3 reflected = Vector3.Reflect(flightDirection, flat);
        reflected.y = 0f;
        if (reflected.sqrMagnitude < 0.0001f) return false;

        bouncesLeft--;
        flightDirection = reflected.normalized;

        // Duvarın içine gömülüp aynı yerde tekrar çarpmasın diye biraz dışarı alınıyor.
        transform.position = point + flat * (hitRadius + 0.05f);

        if (Fx.Instance != null) Fx.Instance.HitBurst(transform.position, false);
        return true;
    }

    private void BeginRoll(EnemyHealth enemy)
    {
        targetEnemy = enemy;
        rollTimer = 0f;
        secondChanceUsed = false;

        // Aynı düşmanda zaten dönen zar varsa yana ve yukarı kayarak ayrışır.
        rollSlot = DiceManager.Instance != null ? DiceManager.Instance.CountRollingOn(enemy, this) : 0;

        CombatModifiers mods = CombatModifiers.Instance;
        float factor = mods != null ? mods.RollDurationMultiplier : 1f;
        currentRollDuration = Mathf.Max(0.05f, rollDuration * factor);

        state = DiceState.Rolling;
        SetTrail(false);

        SpawnDisplay();
    }

    private void BeginReturn()
    {
        targetEnemy = null;
        state = DiceState.Returning;
        SetTrail(true);
    }

    private void Catch()
    {
        state = DiceState.Held;
        targetEnemy = null;
        transform.position = HoldPoint().position + HoldOffset;
        SetTrail(false);
    }

    /// <summary>Zarı zorla ele döndürür (oda geçişlerinde kullanılır).</summary>
    public void ResetToHand()
    {
        CancelDisplay();

        state = DiceState.Held;
        targetEnemy = null;
        travelled = 0f;
        rollTimer = 0f;
        rollSlot = 0;
        bouncesLeft = 0;
        chainsLeft = 0;
        secondChanceUsed = false;
        transform.position = HoldPoint().position + HoldOffset;
        SetTrail(false);
        if (trail != null) trail.Clear();
    }

    // --- Sonuç ----------------------------------------------------------

    /// <summary>
    /// Dönme bitti: sonuç belirlenir ve göstergeye verilir.
    /// Hasar burada UYGULANMAZ; gösterge düşmana indiğinde uygulanır.
    /// </summary>
    private void Resolve()
    {
        EnemyHealth enemy = targetEnemy;
        CombatModifiers mods = CombatModifiers.Instance;

        int faces = mods != null ? mods.FaceCount : 6;
        int lowest = mods != null ? mods.LowestFace : 1;

        int result = mods != null ? mods.RollFace() : Random.Range(1, faces + 1);
        lastResult = result;

        Vector3 anchor = enemy != null ? enemy.HeadPoint + RollOffset() : transform.position;

        // İkinci Şans: en DÜŞÜK yüz geldiyse zar yerinde bir kez daha döner.
        if (result == lowest && mods != null && mods.HasSecondChance && !secondChanceUsed)
        {
            secondChanceUsed = true;
            rollTimer = 0f;

            if (Fx.Instance != null)
            {
                Fx.Instance.ShowText(anchor + Vector3.up * 0.9f, result + " — tekrar!",
                    new Color(0.85f, 0.6f, 1f), 1.1f);
            }

            if (display != null) display.Begin(anchor + Vector3.up * 0.55f, faces, currentRollDuration);
            return;
        }

        ShotBreakdown shot = mods != null ? mods.Resolve(result) : null;
        int damage = shot != null ? shot.Total : result * 10;
        bool crit = shot != null && shot.Crit;

        if (Fx.Instance != null)
        {
            Fx.Instance.HitBurst(anchor, crit);
            Fx.Instance.ShakeCamera(crit ? critShake : normalShake, crit ? critShakeTime : normalShakeTime);
            if (crit) Fx.Instance.HitStop(hitStopScale, hitStopDuration);
        }

        // Gösterge sonucu asılı tutar, düşmana iner ve hasarı değdiği anda uygular.
        if (display != null)
        {
            display.Resolve(result, crit, enemy, damage, anchor);
            display = null;
        }
        else if (enemy != null && enemy.IsAlive)
        {
            // Gösterge prefabı bağlanmamışsa hasar doğrudan uygulanır.
            if (Fx.Instance != null) Fx.Instance.ShowHitDamage(enemy.HeadPoint, damage, crit);
            enemy.TakeDamage(damage);
            if (mods != null) mods.ApplySideEffects(enemy, damage, anchor);
        }

        // Zincir: hasardan sonra menzildeki başka bir düşmana sıçra.
        if (chainsLeft > 0 && mods != null)
        {
            EnemyHealth next = mods.FindChainTarget(transform.position, enemy);
            if (next != null)
            {
                chainsLeft--;
                targetEnemy = next;
                state = DiceState.Chaining;
                SetTrail(true);

                if (Fx.Instance != null)
                {
                    Fx.Instance.ShowText(anchor + Vector3.up * 0.4f, "ZİNCİR",
                        new Color(0.45f, 0.95f, 1f), 0.8f);
                }
                return;
            }
        }

        BeginReturn();
    }

    private void SpawnDisplay()
    {
        CancelDisplay();

        if (resultDisplayPrefab == null) return;

        CombatModifiers mods = CombatModifiers.Instance;
        int faces = mods != null ? mods.FaceCount : 6;

        Vector3 anchor = (targetEnemy != null ? targetEnemy.HeadPoint + RollOffset() : transform.position)
                         + Vector3.up * 0.55f;

        GameObject go = Instantiate(resultDisplayPrefab, anchor, Quaternion.identity);
        if (!go.TryGetComponent(out display))
        {
            Destroy(go);
            return;
        }

        display.Begin(anchor, faces, currentRollDuration);
    }

    private void CancelDisplay()
    {
        if (display == null) return;

        display.Cancel();
        display = null;
    }

    // --- Güdüm ----------------------------------------------------------

    /// <summary>
    /// Güdüm buff'ı: dar bir koni içindeki en yakın düşmana doğru sınırlı hızda döner.
    /// Dönüş hızı düşük tutulduğu için köşeyi dönemez.
    /// </summary>
    private void ApplyHoming(float dt)
    {
        CombatModifiers mods = CombatModifiers.Instance;
        if (mods == null || !mods.HasHoming) return;

        EnemyHealth target = FindHomingTarget(mods.HomingRange, mods.HomingAngle);
        if (target == null) return;

        Vector3 want = target.HeadPoint - transform.position;
        want.y = 0f;
        if (want.sqrMagnitude < 0.0001f) return;

        float maxRadians = mods.HomingTurnSpeed * Mathf.Deg2Rad * dt;
        Vector3 turned = Vector3.RotateTowards(flightDirection, want.normalized, maxRadians, 0f);
        turned.y = 0f;
        if (turned.sqrMagnitude < 0.0001f) return;

        flightDirection = turned.normalized;
    }

    private EnemyHealth FindHomingTarget(float maxRange, float maxAngle)
    {
        EnemyHealth best = null;
        float bestSqr = maxRange * maxRange;

        for (int i = 0; i < EnemyHealth.All.Count; i++)
        {
            EnemyHealth candidate = EnemyHealth.All[i];
            if (candidate == null || !candidate.IsAlive) continue;

            Vector3 d = candidate.transform.position - transform.position;
            d.y = 0f;

            float sqr = d.sqrMagnitude;
            if (sqr > bestSqr || sqr < 0.0001f) continue;
            if (Vector3.Angle(flightDirection, d) > maxAngle) continue;

            bestSqr = sqr;
            best = candidate;
        }

        return best;
    }

    // --- Yardımcılar ----------------------------------------------------

    private float CurrentThrowSpeed
    {
        get
        {
            CombatModifiers mods = CombatModifiers.Instance;
            return throwSpeed * (mods != null ? Mathf.Max(0.1f, mods.SpeedMultiplier) : 1f);
        }
    }

    private float CurrentReturnSpeed
    {
        get
        {
            CombatModifiers mods = CombatModifiers.Instance;
            return returnSpeed * (mods != null ? Mathf.Max(0.1f, mods.SpeedMultiplier) : 1f);
        }
    }

    private Transform HoldPoint()
    {
        return player != null ? player.ChestPoint : transform;
    }

    /// <summary>Aynı düşmanda dönen zarların üst üste binmemesi için kayma.</summary>
    private Vector3 RollOffset()
    {
        if (rollSlot <= 0) return Vector3.zero;

        float angle = 55f * rollSlot;
        Vector3 side = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * rollSpreadRadius;
        return side + Vector3.up * (rollSpreadHeight * rollSlot);
    }

    private void Tumble(float speed)
    {
        transform.Rotate(new Vector3(1f, 0.8f, 0.35f), speed * Time.deltaTime, Space.Self);
    }

    private void SetTrail(bool emitting)
    {
        if (trail == null) return;
        trail.emitting = emitting;
    }

    /// <summary>
    /// Uçuş yolundaki ilk engeli bulur. Tag ve layer kullanılmaz:
    /// isabet eden collider'ın ebeveynlerinde EnemyHealth var mı diye bakılır.
    /// Düşman bulunursa enemy dolu döner, duvar ise enemy null döner.
    ///
    /// Hayalet Zar buff'ı varsa düşman olmayan hiçbir şey engel saymaz.
    /// </summary>
    private bool TryCast(Vector3 origin, Vector3 direction, float distance,
        out EnemyHealth enemy, out Vector3 point, out Vector3 normal)
    {
        enemy = null;
        point = origin;
        normal = Vector3.zero;

        if (distance <= 0f) return false;

        CombatModifiers mods = CombatModifiers.Instance;
        bool ghost = mods != null && mods.HasGhost;

        int count = Physics.SphereCastNonAlloc(
            origin, hitRadius, direction, hitBuffer, distance, ~0, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (hit.collider == null) continue;
            if (IsSelfOrPlayer(hit.collider)) continue;
            if (hit.distance > nearest) continue;

            EnemyHealth health = hit.collider.GetComponentInParent<EnemyHealth>();
            if (health != null && !health.IsAlive) continue;

            // Hayalet Zar: duvarları yok say, sadece düşmana çarp.
            if (ghost && health == null) continue;

            nearest = hit.distance;
            enemy = health;
            point = origin + direction * hit.distance;
            normal = hit.normal;
            found = true;
        }

        return found;
    }

    private bool IsSelfOrPlayer(Collider collider)
    {
        Transform t = collider.transform;
        if (t.IsChildOf(transform)) return true;
        if (player != null && t.IsChildOf(player.transform)) return true;
        return false;
    }
}
