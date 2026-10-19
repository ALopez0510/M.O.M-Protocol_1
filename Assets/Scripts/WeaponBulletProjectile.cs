using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class WeaponBulletProjectile : MonoBehaviour
{
    private WeaponType weaponType;
    private Sprite[] flightSprites;
    private Sprite[] impactSprites;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private int direction = 1;
    private float animTimer;
    private int frameIndex;
    private bool hasImpacted;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
    }

    public void Initialize(WeaponDefinition def, int dir)
    {
        weaponType = def.weaponType;
        direction = dir >= 0 ? 1 : -1;
        flightSprites = def.projectileSprites;
        impactSprites = def.impactSprites;

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        spriteRenderer.sortingOrder = 14;
        spriteRenderer.flipX = direction < 0;

        if (flightSprites != null && flightSprites.Length > 0)
        {
            if (weaponType == WeaponType.QuantumLadle)
            {
                frameIndex = Random.Range(0, flightSprites.Length);
                spriteRenderer.sprite = flightSprites[frameIndex];
            }
            else
            {
                spriteRenderer.sprite = flightSprites[0];
            }
        }

        rb.gravityScale = def.projectileGravity;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        float launchUp = 0f;
        if (weaponType == WeaponType.AnvilLauncher) launchUp = 5.5f;
        else if (weaponType == WeaponType.SlimeGun) launchUp = 3.2f;
        else if (weaponType == WeaponType.QuantumLadle) launchUp = 2.2f;

        rb.linearVelocity = new Vector2(direction * def.projectileSpeed, launchUp);
        Destroy(gameObject, 3.0f);
    }

    private void Update()
    {
        if (hasImpacted) return;

        if (weaponType == WeaponType.SlimeGun && flightSprites != null && flightSprites.Length > 1)
        {
            animTimer += Time.deltaTime;
            if (animTimer >= 0.08f)
            {
                animTimer = 0f;
                frameIndex = (frameIndex + 1) % flightSprites.Length;
                spriteRenderer.sprite = flightSprites[frameIndex];
            }
        }
        else if (weaponType == WeaponType.QuantumLadle || weaponType == WeaponType.AnvilLauncher)
        {
            transform.Rotate(0f, 0f, -direction * 360f * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasImpacted) return;
        if (other.CompareTag("Player")) return;
        if (other.GetComponent<WeaponPickup>() != null) return;
        if (other.GetComponent<WeaponBulletProjectile>() != null) return;
        if (other.GetComponent<ThrownWeaponProjectile>() != null) return;

        if (weaponType == WeaponType.ReverseGravityRay && other.attachedRigidbody != null && other.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic)
        {
            other.attachedRigidbody.gravityScale = -Mathf.Abs(other.attachedRigidbody.gravityScale > 0.1f ? other.attachedRigidbody.gravityScale : 1.5f);
            other.attachedRigidbody.linearVelocity = new Vector2(other.attachedRigidbody.linearVelocity.x, 4.5f);
        }

        if (!other.isTrigger)
        {
            TriggerImpact();
        }
    }

    private void TriggerImpact()
    {
        hasImpacted = true;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        rb.simulated = false;
        transform.rotation = Quaternion.identity;

        if (impactSprites != null && impactSprites.Length > 0 && spriteRenderer != null)
        {
            Sprite imp = impactSprites[Random.Range(0, impactSprites.Length)];
            if (imp != null)
            {
                spriteRenderer.sprite = imp;
            }
            float stayDuration = (weaponType == WeaponType.SlimeGun) ? 2.0f : 0.18f;
            Destroy(gameObject, stayDuration);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
