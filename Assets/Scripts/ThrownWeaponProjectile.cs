using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class ThrownWeaponProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 12f;
    [SerializeField] private float maxFlightTime = 0.65f;
    [SerializeField] private float spinFrameInterval = 0.07f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private int direction = 1;

    private WeaponType weaponType = WeaponType.StandardPistol;
    private Sprite idlePickupSprite;
    private Sprite[] spinSprites;
    private int spinIndex;
    private float spinTimer;
    private float flightTimer;
    private bool convertedToPickup;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb.gravityScale = 0.6f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    public void Initialize(int dir, Sprite projectileSprite = null)
    {
        InitializeWeaponThrow(WeaponType.StandardPistol, dir, projectileSprite, projectileSprite != null ? new Sprite[] { projectileSprite } : null);
    }

    public void InitializeWeaponThrow(WeaponType type, int dir, Sprite pickupSprite, Sprite[] thrownAnimSprites)
    {
        weaponType = type;
        direction = dir >= 0 ? 1 : -1;
        idlePickupSprite = pickupSprite;
        spinSprites = thrownAnimSprites;

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        if (spinSprites != null && spinSprites.Length > 0 && spinSprites[0] != null)
        {
            spriteRenderer.sprite = spinSprites[0];
        }
        else if (idlePickupSprite != null)
        {
            spriteRenderer.sprite = idlePickupSprite;
        }

        spriteRenderer.flipX = direction < 0;
        spriteRenderer.sortingOrder = 13;

        rb.gravityScale = 0.8f;
        rb.linearVelocity = new Vector2(direction * speed, 1.8f);
    }

    private void Update()
    {
        if (convertedToPickup) return;

        flightTimer += Time.deltaTime;
        if (spinSprites != null && spinSprites.Length > 1)
        {
            spinTimer += Time.deltaTime;
            if (spinTimer >= spinFrameInterval)
            {
                spinTimer = 0f;
                spinIndex = (spinIndex + 1) % spinSprites.Length;
                if (spinSprites[spinIndex] != null)
                {
                    spriteRenderer.sprite = spinSprites[spinIndex];
                }
            }
        }

        if (flightTimer >= maxFlightTime)
        {
            ConvertToPickup();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (convertedToPickup) return;
        if (other.CompareTag("Player")) return;
        if (other.GetComponent<WeaponPickup>() != null) return;
        if (other.GetComponent<WeaponBulletProjectile>() != null) return;

        if (!other.isTrigger)
        {
            ConvertToPickup();
        }
    }

    private void ConvertToPickup()
    {
        if (convertedToPickup) return;
        convertedToPickup = true;

        GameObject pickupObj = new GameObject("WeaponPickup_" + weaponType);
        pickupObj.transform.position = transform.position;

        SpriteRenderer sr = pickupObj.AddComponent<SpriteRenderer>();
        Sprite finalSprite = idlePickupSprite != null ? idlePickupSprite : (spriteRenderer != null ? spriteRenderer.sprite : null);
        sr.sprite = finalSprite;
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
        wp.Configure(weaponType, finalSprite, 0.35f);

        Destroy(gameObject);
    }
}
