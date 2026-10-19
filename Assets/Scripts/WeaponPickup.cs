using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class WeaponPickup : MonoBehaviour
{
    [SerializeField] private WeaponType weaponType = WeaponType.StandardPistol;
    [SerializeField] private Sprite weaponSprite;
    [SerializeField] private float pickupDelay = 0.35f;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private BoxCollider2D solidCollider;
    private BoxCollider2D triggerCollider;
    private float spawnTime;
    private bool canBePickedUp = true;

    public WeaponType WeaponType => weaponType;
    public bool CanBePickedUp => canBePickedUp && (Time.time - spawnTime >= pickupDelay);

    private void Awake()
    {
        spawnTime = Time.time;
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        rb.gravityScale = 2.5f;
        rb.linearDamping = 12f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Ensure we have one non-trigger collider to rest on ground (ignoring Player collision)
        // and one trigger collider to detect Player pickup
        BoxCollider2D[] cols = GetComponents<BoxCollider2D>();
        if (cols.Length == 1)
        {
            solidCollider = cols[0];
            solidCollider.isTrigger = false;
            solidCollider.size = new Vector2(0.65f, 0.42f);

            triggerCollider = gameObject.AddComponent<BoxCollider2D>();
            triggerCollider.isTrigger = true;
            triggerCollider.size = new Vector2(0.95f, 0.75f);
        }
        else if (cols.Length >= 2)
        {
            solidCollider = cols[0];
            solidCollider.isTrigger = false;
            triggerCollider = cols[1];
            triggerCollider.isTrigger = true;
        }

        if (weaponSprite != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = weaponSprite;
        }
    }

    private void Start()
    {
        IgnorePlayerSolidCollision();
    }

    private void FixedUpdate()
    {
        if (rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.001f)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        WeaponPickup otherPickup = collision.collider.GetComponent<WeaponPickup>();
        if (otherPickup != null && solidCollider != null)
        {
            Physics2D.IgnoreCollision(solidCollider, collision.collider, true);
            return;
        }
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.001f)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    public void Configure(WeaponType type, Sprite sprite, float delay = 0.35f)
    {
        weaponType = type;
        weaponSprite = sprite;
        pickupDelay = delay;
        spawnTime = Time.time;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && sprite != null)
        {
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = 8;
        }
        IgnorePlayerSolidCollision();
    }

    private void IgnorePlayerSolidCollision()
    {
        if (solidCollider == null) return;
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            Collider2D playerCol = playerObj.GetComponent<Collider2D>();
            if (playerCol != null)
            {
                Physics2D.IgnoreCollision(solidCollider, playerCol, true);
            }
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!CanBePickedUp) return;
        PlayerController2D player = other.GetComponent<PlayerController2D>();
        if (player != null)
        {
            player.TryPickupWeaponFromGround(this);
        }
    }
}
