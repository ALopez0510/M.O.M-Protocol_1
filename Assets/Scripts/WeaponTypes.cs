using System;
using UnityEngine;

public enum WeaponType
{
    None = -1,
    StandardPistol = 0,
    AnvilLauncher = 1,
    SlimeGun = 2,
    ReverseGravityRay = 3,
    QuantumLadle = 4
}

[Serializable]
public class WeaponDefinition
{
    public WeaponType weaponType;
    public string displayName;
    public Sprite idleSprite;
    public Sprite[] fireSprites;
    public Sprite[] thrownSprites;
    public Sprite[] projectileSprites;
    public Sprite[] impactSprites;
    public float fireRate = 0.25f;
    public float projectileSpeed = 13f;
    public float projectileGravity = 0f;
    public Vector2 muzzleOffset = new Vector2(0.55f, 0.08f);
}
