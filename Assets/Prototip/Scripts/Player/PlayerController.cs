using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// İzometrik oyuncu kontrolü: WASD ile kameraya göre hareket, fare ile nişan alma,
/// Space ile kaçınma hamlesi. Zar ve düşmanlar nişan bilgisini buradan okur.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Hareket")]
    [SerializeField, Tooltip("Normal yürüme hızı (birim/saniye).")]
    private float walkSpeed = 6f;
    [SerializeField, Tooltip("Nişan yönüne dönüş hızı (derece/saniye).")]
    private float turnSpeed = 900f;
    [SerializeField, Tooltip("Yerçekimi ivmesi. Negatif olmalı.")]
    private float gravity = -25f;

    [Header("Kaçınma (Space)")]
    [SerializeField, Tooltip("Kaçınma sırasındaki hız (birim/saniye).")]
    private float dashSpeed = 18f;
    [SerializeField, Tooltip("Kaçınma hamlesinin süresi (saniye).")]
    private float dashDuration = 0.18f;
    [SerializeField, Tooltip("İki kaçınma arasındaki bekleme (saniye).")]
    private float dashCooldown = 0.8f;

    [Header("Referanslar")]
    [SerializeField, Tooltip("Nişan ışını için kullanılan kamera. Boş ise otomatik bulunur.")]
    private Camera aimCamera;
    [SerializeField, Tooltip("Işınlanma sonrası SnapToTarget çağrılacak izometrik kamera.")]
    private IsometricCamera isoCamera;
    [SerializeField, Tooltip("Zarın elde tutulduğu nokta (göğüs hizası).")]
    private Transform chestPoint;

    private CharacterController controller;
    private Vector3 aimDirection = Vector3.forward;
    private Vector3 aimPoint;
    private Vector3 dashDirection = Vector3.forward;
    private float verticalVelocity;
    private float dashTimeLeft;
    private float dashCooldownLeft;

    /// <summary>Oyuncudan fare imlecine doğru yatay, normalize edilmiş yön.</summary>
    public Vector3 AimDirection => aimDirection;

    /// <summary>Fare imlecinin yerdeki dünya konumu.</summary>
    public Vector3 AimPoint => aimPoint;

    /// <summary>Kaçınma hamlesi sürüyor mu? (bu sırada hasar alınmaz)</summary>
    public bool IsDashing => dashTimeLeft > 0f;

    /// <summary>Kaçınma yeniden kullanılabilir mi?</summary>
    public bool DashReady => dashCooldownLeft <= 0f && dashTimeLeft <= 0f;

    /// <summary>Zarın elde durduğu nokta; atanmamışsa kökün kendisi.</summary>
    public Transform ChestPoint => chestPoint != null ? chestPoint : transform;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        aimDirection = transform.forward;
        aimPoint = transform.position + aimDirection;
    }

    private void Update()
    {
        // Sandık paneli açıkken oyuncu hareket edemez ve nişan almaz.
        if (GamePause.Paused) return;

        UpdateAim();

        Vector2 input = ReadMoveInput();
        Vector3 moveDir = ToCameraSpace(input);

        UpdateDash(moveDir);

        Vector3 horizontal = IsDashing ? dashDirection * dashSpeed : moveDir * walkSpeed;
        UpdateGravity();

        controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        UpdateRotation();
    }

    /// <summary>WASD okuması. Çaprazda hızlanma olmaması için vektör normalize edilir.</summary>
    private Vector2 ReadMoveInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return Vector2.zero;

        Vector2 v = Vector2.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
        if (v.sqrMagnitude > 1f) v.Normalize();
        return v;
    }

    /// <summary>Kameranın ileri ve sağ yönlerini yatay düzleme indirip girdiyle çarpar.</summary>
    private Vector3 ToCameraSpace(Vector2 input)
    {
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        Camera cam = ResolveCamera();
        if (cam != null)
        {
            forward = cam.transform.forward;
            forward.y = 0f;
            right = cam.transform.right;
            right.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            forward.Normalize();
            right.Normalize();
        }

        return forward * input.y + right * input.x;
    }

    /// <summary>
    /// Nişan: fare imlecinden oyuncunun ayak hizasındaki yatay düzleme matematiksel ışın.
    /// Fizik raycast kullanılmaz, böylece duvarlar nişanı bozmaz.
    /// </summary>
    private void UpdateAim()
    {
        Camera cam = ResolveCamera();
        Mouse mouse = Mouse.current;
        if (cam == null || mouse == null) return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        Plane ground = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (!ground.Raycast(ray, out float enter)) return;

        aimPoint = ray.GetPoint(enter);
        Vector3 d = aimPoint - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.0004f) aimDirection = d.normalized;
    }

    private void UpdateDash(Vector3 moveDir)
    {
        if (dashCooldownLeft > 0f) dashCooldownLeft -= Time.deltaTime;

        if (dashTimeLeft > 0f)
        {
            dashTimeLeft -= Time.deltaTime;
            return;
        }

        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.spaceKey.wasPressedThisFrame || dashCooldownLeft > 0f) return;

        // Dururken kaçınma nişan yönüne olsun.
        dashDirection = moveDir.sqrMagnitude > 0.0001f ? moveDir.normalized : aimDirection;
        dashTimeLeft = dashDuration;
        dashCooldownLeft = dashCooldown + dashDuration;
    }

    private void UpdateGravity()
    {
        if (controller.isGrounded && verticalVelocity <= 0f) verticalVelocity = -2f;
        else verticalVelocity += gravity * Time.deltaTime;
    }

    private void UpdateRotation()
    {
        if (aimDirection.sqrMagnitude < 0.0001f) return;
        Quaternion want = Quaternion.LookRotation(aimDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
    }

    /// <summary>
    /// CharacterController'lı oyuncuyu güvenli ışınlama: önce bileşen kapatılır,
    /// konum atanır, sonra geri açılır ve kamera hedefe yapıştırılır.
    /// </summary>
    public void Teleport(Vector3 position)
    {
        if (controller == null) controller = GetComponent<CharacterController>();

        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        transform.position = position;
        controller.enabled = wasEnabled;

        verticalVelocity = 0f;
        dashTimeLeft = 0f;
        dashCooldownLeft = 0f;

        IsometricCamera cam = ResolveIsoCamera();
        if (cam != null) cam.SnapToTarget();
    }

    private Camera ResolveCamera()
    {
        if (aimCamera != null) return aimCamera;

        IsometricCamera iso = ResolveIsoCamera();
        if (iso != null) aimCamera = iso.Cam;
        if (aimCamera == null) aimCamera = Camera.main;
        return aimCamera;
    }

    private IsometricCamera ResolveIsoCamera()
    {
        if (isoCamera == null) isoCamera = FindFirstObjectByType<IsometricCamera>();
        return isoCamera;
    }
}
