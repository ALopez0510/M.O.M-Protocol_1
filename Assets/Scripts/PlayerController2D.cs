using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6.5f;
    [SerializeField] private float jumpForce = 13f;

    [Header("Wall Slide & Wall Jump")]
    [SerializeField] private float wallSlideSpeed = 2.2f;
    [SerializeField] private Vector2 wallJumpForce = new Vector2(8f, 12.5f);
    [SerializeField] private float wallCheckDistance = 0.15f;

    [Header("Dodge Roll")]
    [SerializeField] private float dodgeRollSpeed = 10f;
    [SerializeField] private float dodgeRollDuration = 0.5f;
    [SerializeField] private float dodgeRollCooldown = 0.15f;

    [Header("Weapons & Hand Display")]
    [SerializeField] private WeaponType currentWeapon = WeaponType.StandardPistol;
    [SerializeField] private SpriteRenderer heldWeaponRenderer;
    [SerializeField] private Vector2 handIdleOffset = new Vector2(0.42f, 1.03f);
    [SerializeField] private Vector2 handRunOffset = new Vector2(0.45f, 1.00f);
    [SerializeField] private Vector2 handJumpOffset = new Vector2(0.38f, 1.05f);
    [SerializeField] private List<WeaponDefinition> weaponDefinitions = new List<WeaponDefinition>();

    [Header("Throw Weapon")]
    [SerializeField] private GameObject thrownWeaponPrefab;
    [SerializeField] private Sprite thrownWeaponSprite;
    [SerializeField] private Vector2 throwSpawnOffset = new Vector2(0.85f, 1.15f);
    [SerializeField] private float throwAnimationDuration = 0.5f;
    [SerializeField] private float throwSpawnDelay = 0.22f;

    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    private float moveInput;
    private float externalMoveInput;
    private bool useExternalInput;

    private bool isGrounded;
    private bool isTouchingWall;
    private bool isWallSliding;
    private int wallDirection; // 1 = wall on right, -1 = wall on left
    private int facingDirection = 1; // 1 = right, -1 = left

    private bool isDodgeRolling;
    private float dodgeRollCooldownTimer;
    private bool isThrowing;
    private bool weaponReleasedDuringThrow;
    private float wallJumpLockTimer;

    private bool isFiringWeapon;
    private float fireCooldownTimer;
    private float pickupSwapCooldownTimer;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsWallSlidingHash = Animator.StringToHash("IsWallSliding");
    private static readonly int IsDodgeRollingHash = Animator.StringToHash("IsDodgeRolling");
    private static readonly int IsThrowingHash = Animator.StringToHash("IsThrowing");
    private static readonly int JumpTriggerHash = Animator.StringToHash("Jump");
    private static readonly int DodgeRollTriggerHash = Animator.StringToHash("DodgeRoll");
    private static readonly int ThrowWeaponTriggerHash = Animator.StringToHash("ThrowWeapon");

    public bool IsGrounded => isGrounded;
    public bool IsWallSliding => isWallSliding;
    public bool IsDodgeRolling => isDodgeRolling;
    public bool IsThrowing => isThrowing;
    public bool IsFiringWeapon => isFiringWeapon;
    public int FacingDirection => facingDirection;
    public WeaponType CurrentWeapon => currentWeapon;
    public SpriteRenderer HeldWeaponRenderer => heldWeaponRenderer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        if (rb.gravityScale < 2f)
        {
            rb.gravityScale = 3f;
        }

        EnsureHeldWeaponRenderer();
        UpdateHeldWeaponVisual();
    }

    private void EnsureHeldWeaponRenderer()
    {
        if (heldWeaponRenderer == null)
        {
            Transform child = transform.Find("HeldWeapon");
            if (child != null)
            {
                heldWeaponRenderer = child.GetComponent<SpriteRenderer>();
            }
            if (heldWeaponRenderer == null)
            {
                GameObject heldObj = new GameObject("HeldWeapon");
                heldObj.transform.SetParent(transform);
                heldObj.transform.localPosition = new Vector3(handIdleOffset.x, handIdleOffset.y, 0f);
                heldObj.transform.localScale = Vector3.one;
                heldWeaponRenderer = heldObj.AddComponent<SpriteRenderer>();
            }
        }
        heldWeaponRenderer.sortingOrder = (spriteRenderer != null ? spriteRenderer.sortingOrder : 10) + 2;
    }

    private void Update()
    {
        if (dodgeRollCooldownTimer > 0f) dodgeRollCooldownTimer -= Time.deltaTime;
        if (wallJumpLockTimer > 0f) wallJumpLockTimer -= Time.deltaTime;
        if (fireCooldownTimer > 0f) fireCooldownTimer -= Time.deltaTime;
        if (pickupSwapCooldownTimer > 0f) pickupSwapCooldownTimer -= Time.deltaTime;

        ReadInput(out bool jumpPressed, out bool rollPressed, out bool shootPressed, out bool throwPressed, out bool pickupPressed);
        CheckEnvironment();

        if (pickupPressed)
        {
            PickupNearestWeapon();
        }

        if (rollPressed && !isDodgeRolling && !isThrowing && dodgeRollCooldownTimer <= 0f)
        {
            StartCoroutine(PerformDodgeRoll());
        }

        if (shootPressed && !isDodgeRolling && !isThrowing && !isWallSliding && currentWeapon != WeaponType.None && fireCooldownTimer <= 0f)
        {
            StartCoroutine(PerformShootWeapon());
        }

        if (throwPressed && !isThrowing && !isDodgeRolling && !isWallSliding && currentWeapon != WeaponType.None)
        {
            StartCoroutine(PerformThrowWeapon());
        }

        if (jumpPressed && !isDodgeRolling)
        {
            PerformJump();
        }

        UpdateFacingAndAnimator();
        UpdateHeldWeaponTransform();
    }

    private void FixedUpdate()
    {
        if (isDodgeRolling)
        {
            rb.linearVelocity = new Vector2(facingDirection * dodgeRollSpeed, rb.linearVelocity.y);
            return;
        }

        if (isThrowing && isGrounded)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        if (wallJumpLockTimer <= 0f)
        {
            rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
        }

        if (isWallSliding && rb.linearVelocity.y < -wallSlideSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -wallSlideSpeed);
        }
    }

    private void ReadInput(out bool jumpPressed, out bool rollPressed, out bool shootPressed, out bool throwPressed, out bool pickupPressed)
    {
        jumpPressed = false;
        rollPressed = false;
        shootPressed = false;
        throwPressed = false;
        pickupPressed = false;

        float kbMove = 0f;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) kbMove -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) kbMove += 1f;

            if (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                jumpPressed = true;

            if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
                rollPressed = true;

            // Shoot / Use weapon in hand
            if (kb.fKey.wasPressedThisFrame || kb.kKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                shootPressed = true;

            // Throw weapon
            if (kb.jKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame || kb.gKey.wasPressedThisFrame)
                throwPressed = true;

            // E key: if near a pickup on the ground, pick it up; otherwise throw current weapon
            if (kb.eKey.wasPressedThisFrame)
            {
                if (FindNearestPickup(1.3f) != null) pickupPressed = true;
                else throwPressed = true;
            }

            if (kb.rKey.wasPressedThisFrame)
                pickupPressed = true;
        }

        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame) shootPressed = true;
            if (mouse.rightButton.wasPressedThisFrame) throwPressed = true;
        }

        var pad = Gamepad.current;
        if (pad != null)
        {
            float stickX = pad.leftStick.x.ReadValue();
            if (Mathf.Abs(stickX) > 0.2f) kbMove = Mathf.Sign(stickX);
            if (pad.buttonSouth.wasPressedThisFrame) jumpPressed = true;
            if (pad.buttonEast.wasPressedThisFrame) rollPressed = true;
            if (pad.buttonWest.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame) shootPressed = true;
            if (pad.buttonNorth.wasPressedThisFrame || pad.leftTrigger.wasPressedThisFrame) throwPressed = true;
        }

        moveInput = useExternalInput ? externalMoveInput : kbMove;
    }

    private void CheckEnvironment()
    {
        Bounds b = boxCollider.bounds;
        isGrounded = false;
        isTouchingWall = false;
        wallDirection = 0;

        Vector2 groundBoxCenter = new Vector2(b.center.x, b.min.y - 0.06f);
        Vector2 groundBoxSize = new Vector2(b.size.x * 0.85f, 0.1f);
        Collider2D[] groundHits = Physics2D.OverlapBoxAll(groundBoxCenter, groundBoxSize, 0f);
        for (int i = 0; i < groundHits.Length; i++)
        {
            Collider2D col = groundHits[i];
            if (col != boxCollider && !col.isTrigger &&
                col.GetComponent<ThrownWeaponProjectile>() == null &&
                col.GetComponent<WeaponPickup>() == null &&
                col.GetComponent<WeaponBulletProjectile>() == null)
            {
                isGrounded = true;
                break;
            }
        }

        Vector2 sideBoxSize = new Vector2(wallCheckDistance, b.size.y * 0.7f);
        Vector2 rightCenter = new Vector2(b.max.x + wallCheckDistance * 0.5f, b.center.y);
        Vector2 leftCenter = new Vector2(b.min.x - wallCheckDistance * 0.5f, b.center.y);

        bool rightHit = HasSolidOverlap(rightCenter, sideBoxSize);
        bool leftHit = HasSolidOverlap(leftCenter, sideBoxSize);

        if (rightHit)
        {
            isTouchingWall = true;
            wallDirection = 1;
        }
        else if (leftHit)
        {
            isTouchingWall = true;
            wallDirection = -1;
        }

        isWallSliding = !isGrounded && isTouchingWall && !isDodgeRolling && rb.linearVelocity.y <= 0.5f;
    }

    private bool HasSolidOverlap(Vector2 center, Vector2 size)
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col != boxCollider && !col.isTrigger &&
                col.GetComponent<ThrownWeaponProjectile>() == null &&
                col.GetComponent<WeaponPickup>() == null &&
                col.GetComponent<WeaponBulletProjectile>() == null)
            {
                return true;
            }
        }
        return false;
    }

    private void PerformJump()
    {
        if (isGrounded)
        {
            isGrounded = false;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            animator.SetTrigger(JumpTriggerHash);
            animator.Play("Jump", 0, 0f);
        }
        else if (isWallSliding)
        {
            isWallSliding = false;
            int jumpDir = -wallDirection;
            facingDirection = jumpDir;
            wallJumpLockTimer = 0.18f;
            rb.linearVelocity = new Vector2(jumpDir * wallJumpForce.x, wallJumpForce.y);
            animator.SetTrigger(JumpTriggerHash);
            animator.Play("Jump", 0, 0f);
        }
    }

    private IEnumerator PerformDodgeRoll()
    {
        isDodgeRolling = true;
        if (Mathf.Abs(moveInput) > 0.01f)
        {
            facingDirection = moveInput > 0f ? 1 : -1;
        }

        Vector2 originalSize = boxCollider.size;
        Vector2 originalOffset = boxCollider.offset;
        boxCollider.size = new Vector2(originalSize.x, originalSize.y * 0.65f);
        boxCollider.offset = new Vector2(originalOffset.x, originalOffset.y * 0.65f);

        animator.SetBool(IsDodgeRollingHash, true);
        animator.SetTrigger(DodgeRollTriggerHash);
        animator.Play("DodgeRoll", 0, 0f);
        UpdateHeldWeaponVisual();

        yield return new WaitForSeconds(dodgeRollDuration);

        boxCollider.size = originalSize;
        boxCollider.offset = originalOffset;
        isDodgeRolling = false;
        dodgeRollCooldownTimer = dodgeRollCooldown;
        animator.SetBool(IsDodgeRollingHash, false);
        UpdateHeldWeaponVisual();
    }

    private IEnumerator PerformShootWeapon()
    {
        WeaponDefinition def = GetWeaponDefinition(currentWeapon);
        if (def == null) yield break;

        isFiringWeapon = true;
        fireCooldownTimer = Mathf.Max(0.15f, def.fireRate);

        // Spawn projectile from weapon muzzle
        SpawnWeaponBullet(def);

        // Animate firing frames on HeldWeapon in the player's hand
        if (def.fireSprites != null && def.fireSprites.Length > 0 && heldWeaponRenderer != null)
        {
            float frameTime = Mathf.Clamp(def.fireRate / (def.fireSprites.Length + 1), 0.045f, 0.09f);
            for (int i = 0; i < def.fireSprites.Length; i++)
            {
                if (currentWeapon != def.weaponType || isDodgeRolling || isThrowing) break;
                if (def.fireSprites[i] != null)
                {
                    heldWeaponRenderer.sprite = def.fireSprites[i];
                }
                yield return new WaitForSeconds(frameTime);
            }
        }
        else
        {
            yield return new WaitForSeconds(0.12f);
        }

        isFiringWeapon = false;
        UpdateHeldWeaponVisual();
    }

    private void SpawnWeaponBullet(WeaponDefinition def)
    {
        Vector3 handWorldPos = heldWeaponRenderer != null
            ? heldWeaponRenderer.transform.position
            : transform.position + new Vector3(facingDirection * handIdleOffset.x, handIdleOffset.y, 0f);

        Vector3 spawnPos = handWorldPos + new Vector3(facingDirection * def.muzzleOffset.x, def.muzzleOffset.y, 0f);

        GameObject bulletObj = new GameObject("Bullet_" + def.weaponType);
        bulletObj.transform.position = spawnPos;

        SpriteRenderer sr = bulletObj.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 14;

        Rigidbody2D bulletRb = bulletObj.AddComponent<Rigidbody2D>();
        bulletRb.gravityScale = def.projectileGravity;

        BoxCollider2D col = bulletObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.45f, 0.35f);

        WeaponBulletProjectile proj = bulletObj.AddComponent<WeaponBulletProjectile>();
        proj.Initialize(def, facingDirection);
    }

    private IEnumerator PerformThrowWeapon()
    {
        if (currentWeapon == WeaponType.None) yield break;

        WeaponType thrownType = currentWeapon;
        WeaponDefinition def = GetWeaponDefinition(thrownType);

        isThrowing = true;
        weaponReleasedDuringThrow = false;
        if (Mathf.Abs(moveInput) > 0.01f)
        {
            facingDirection = moveInput > 0f ? 1 : -1;
        }

        animator.SetBool(IsThrowingHash, true);
        animator.SetTrigger(ThrowWeaponTriggerHash);
        animator.Play("ThrowWeapon", 0, 0f);

        float clampedDelay = Mathf.Clamp(throwSpawnDelay, 0.05f, throwAnimationDuration);
        yield return new WaitForSeconds(clampedDelay);

        // Release weapon from hand and spawn spinning thrown weapon
        weaponReleasedDuringThrow = true;
        currentWeapon = WeaponType.None;
        pickupSwapCooldownTimer = 0.35f;
        UpdateHeldWeaponVisual();

        SpawnThrownWeaponInstance(thrownType, def);

        float remaining = Mathf.Max(0.01f, throwAnimationDuration - clampedDelay);
        yield return new WaitForSeconds(remaining);

        isThrowing = false;
        weaponReleasedDuringThrow = false;
        animator.SetBool(IsThrowingHash, false);
        UpdateHeldWeaponVisual();
    }

    private void SpawnThrownWeaponInstance(WeaponType thrownType, WeaponDefinition def)
    {
        float spawnOffsetX = throwSpawnOffset.x;
        RaycastHit2D wallHit = Physics2D.Raycast(
            transform.position + new Vector3(0f, throwSpawnOffset.y, 0f),
            new Vector2(facingDirection, 0f),
            spawnOffsetX + 0.35f);
        if (wallHit.collider != null && !wallHit.collider.isTrigger && wallHit.collider != boxCollider)
        {
            spawnOffsetX = Mathf.Max(0.1f, wallHit.distance - 0.35f);
        }

        Vector3 spawnPos = transform.position + new Vector3(facingDirection * spawnOffsetX, throwSpawnOffset.y, 0f);
        Sprite pickupSprite = def != null && def.idleSprite != null ? def.idleSprite : thrownWeaponSprite;
        Sprite[] spinSprites = def != null && def.thrownSprites != null && def.thrownSprites.Length > 0
            ? def.thrownSprites
            : (pickupSprite != null ? new Sprite[] { pickupSprite } : null);

        if (thrownWeaponPrefab != null)
        {
            GameObject projObj = Instantiate(thrownWeaponPrefab, spawnPos, Quaternion.identity);
            projObj.name = "ThrownWeapon_" + thrownType;
            ThrownWeaponProjectile proj = projObj.GetComponent<ThrownWeaponProjectile>();
            if (proj != null)
            {
                proj.InitializeWeaponThrow(thrownType, facingDirection, pickupSprite, spinSprites);
            }
        }
        else
        {
            GameObject projObj = new GameObject("ThrownWeapon_" + thrownType);
            projObj.transform.position = spawnPos;
            SpriteRenderer sr = projObj.AddComponent<SpriteRenderer>();
            BoxCollider2D col = projObj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.75f, 0.45f);
            Rigidbody2D projRb = projObj.AddComponent<Rigidbody2D>();
            ThrownWeaponProjectile proj = projObj.AddComponent<ThrownWeaponProjectile>();
            proj.InitializeWeaponThrow(thrownType, facingDirection, pickupSprite, spinSprites);
        }
    }

    public bool TryPickupWeaponFromGround(WeaponPickup pickup)
    {
        if (pickup == null || !pickup.CanBePickedUp) return false;
        if (isThrowing || isDodgeRolling) return false;
        if (pickupSwapCooldownTimer > 0f) return false;

        // Automatically pick up when unarmed; or swap if touching a different weapon pickup
        if (currentWeapon == WeaponType.None)
        {
            EquipWeapon(pickup.WeaponType);
            pickupSwapCooldownTimer = 0.35f;
            Destroy(pickup.gameObject);
            return true;
        }
        else if (pickup.WeaponType != currentWeapon)
        {
            WeaponType previousWeapon = currentWeapon;
            Vector3 dropPos = pickup.transform.position + new Vector3(-facingDirection * 0.35f, 0.2f, 0f);
            EquipWeapon(pickup.WeaponType);
            pickupSwapCooldownTimer = 0.65f;
            Destroy(pickup.gameObject);
            SpawnDroppedWeaponPickup(previousWeapon, dropPos);
            return true;
        }
        return false;
    }

    public bool PickupNearestWeapon()
    {
        WeaponPickup nearest = FindNearestPickup(1.6f);
        if (nearest == null) return false;

        if (currentWeapon != WeaponType.None && currentWeapon != nearest.WeaponType)
        {
            WeaponType prev = currentWeapon;
            SpawnDroppedWeaponPickup(prev, transform.position + new Vector3(-facingDirection * 0.4f, 0.4f, 0f));
        }

        EquipWeapon(nearest.WeaponType);
        pickupSwapCooldownTimer = 0.4f;
        Destroy(nearest.gameObject);
        return true;
    }

    private WeaponPickup FindNearestPickup(float maxDist)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position + new Vector3(0f, 0.9f, 0f), maxDist);
        WeaponPickup best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            WeaponPickup wp = hits[i].GetComponent<WeaponPickup>();
            if (wp != null && wp.CanBePickedUp)
            {
                float d = Vector2.Distance(transform.position, wp.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = wp;
                }
            }
        }
        return best;
    }

    public WeaponPickup SpawnDroppedWeaponPickup(WeaponType type, Vector3 worldPos)
    {
        WeaponDefinition def = GetWeaponDefinition(type);
        if (def == null) return null;

        GameObject pickupObj = new GameObject("WeaponPickup_" + type);
        pickupObj.transform.position = worldPos;

        SpriteRenderer sr = pickupObj.AddComponent<SpriteRenderer>();
        sr.sprite = def.idleSprite;
        sr.sortingOrder = 8;

        Rigidbody2D pickupRb = pickupObj.AddComponent<Rigidbody2D>();
        pickupRb.gravityScale = 2.5f;
        pickupRb.linearDamping = 12f;
        pickupRb.freezeRotation = true;
        pickupRb.linearVelocity = Vector2.zero;

        BoxCollider2D solidCol = pickupObj.AddComponent<BoxCollider2D>();
        solidCol.isTrigger = false;
        solidCol.size = new Vector2(0.65f, 0.42f);

        BoxCollider2D trigCol = pickupObj.AddComponent<BoxCollider2D>();
        trigCol.isTrigger = true;
        trigCol.size = new Vector2(0.95f, 0.75f);

        WeaponPickup wp = pickupObj.AddComponent<WeaponPickup>();
        wp.Configure(type, def.idleSprite, 0.5f);
        return wp;
    }

    public void EquipWeapon(WeaponType type)
    {
        currentWeapon = type;
        isFiringWeapon = false;
        EnsureHeldWeaponRenderer();
        UpdateHeldWeaponVisual();
    }

    public WeaponDefinition GetWeaponDefinition(WeaponType type)
    {
        for (int i = 0; i < weaponDefinitions.Count; i++)
        {
            if (weaponDefinitions[i] != null && weaponDefinitions[i].weaponType == type)
            {
                return weaponDefinitions[i];
            }
        }
        return null;
    }

    private void UpdateHeldWeaponVisual()
    {
        if (heldWeaponRenderer == null) return;

        bool shouldShow = currentWeapon != WeaponType.None &&
                          !isDodgeRolling &&
                          !isWallSliding &&
                          !(isThrowing && weaponReleasedDuringThrow);

        heldWeaponRenderer.enabled = shouldShow;
        if (!shouldShow) return;

        if (!isFiringWeapon)
        {
            WeaponDefinition def = GetWeaponDefinition(currentWeapon);
            if (def != null && def.idleSprite != null)
            {
                heldWeaponRenderer.sprite = def.idleSprite;
            }
        }
    }

    private void UpdateHeldWeaponTransform()
    {
        if (heldWeaponRenderer == null) return;

        bool shouldShow = currentWeapon != WeaponType.None &&
                          !isDodgeRolling &&
                          !isWallSliding &&
                          !(isThrowing && weaponReleasedDuringThrow);

        heldWeaponRenderer.enabled = shouldShow;
        if (!shouldShow) return;

        Vector2 offset = handIdleOffset;
        if (isThrowing)
        {
            offset = new Vector2(0.50f, 1.25f);
        }
        else if (!isGrounded)
        {
            offset = handJumpOffset;
        }
        else if (Mathf.Abs(moveInput) > 0.1f)
        {
            // Subtle bobbing with running stride
            float bob = Mathf.Sin(Time.time * 18f) * 0.04f;
            offset = new Vector2(handRunOffset.x, handRunOffset.y + bob);
        }

        heldWeaponRenderer.transform.localPosition = new Vector3(facingDirection * offset.x, offset.y, 0f);
        heldWeaponRenderer.flipX = facingDirection < 0;
        heldWeaponRenderer.sortingOrder = (spriteRenderer != null ? spriteRenderer.sortingOrder : 10) + 2;

        if (!isFiringWeapon)
        {
            WeaponDefinition def = GetWeaponDefinition(currentWeapon);
            if (def != null && def.idleSprite != null && heldWeaponRenderer.sprite != def.idleSprite)
            {
                heldWeaponRenderer.sprite = def.idleSprite;
            }
        }
    }

    private void UpdateFacingAndAnimator()
    {
        if (isWallSliding && wallDirection != 0)
        {
            facingDirection = wallDirection;
        }
        else if (!isDodgeRolling && !isThrowing && Mathf.Abs(moveInput) > 0.01f)
        {
            facingDirection = moveInput > 0f ? 1 : -1;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = facingDirection < 0;
        }

        if (animator != null)
        {
            animator.SetFloat(SpeedHash, Mathf.Abs(moveInput));
            animator.SetBool(IsGroundedHash, isGrounded);
            animator.SetBool(IsWallSlidingHash, isWallSliding);
            animator.SetBool(IsDodgeRollingHash, isDodgeRolling);
            animator.SetBool(IsThrowingHash, isThrowing);
        }
    }

    // Public control methods for external scripts, UI buttons, or automated testing
    public void SetHorizontalInput(float value)
    {
        useExternalInput = true;
        externalMoveInput = Mathf.Clamp(value, -1f, 1f);
    }

    public void ClearExternalInput()
    {
        useExternalInput = false;
        externalMoveInput = 0f;
    }

    public void TriggerJump()
    {
        CheckEnvironment();
        if (!isDodgeRolling)
        {
            PerformJump();
        }
    }

    public void TriggerDodgeRoll()
    {
        if (!isDodgeRolling && !isThrowing)
        {
            StartCoroutine(PerformDodgeRoll());
        }
    }

    public void TriggerShoot()
    {
        if (!isDodgeRolling && !isThrowing && !isWallSliding && currentWeapon != WeaponType.None)
        {
            StartCoroutine(PerformShootWeapon());
        }
    }

    public void TriggerThrowWeapon()
    {
        if (!isThrowing && !isDodgeRolling && !isWallSliding && currentWeapon != WeaponType.None)
        {
            StartCoroutine(PerformThrowWeapon());
        }
    }
}
