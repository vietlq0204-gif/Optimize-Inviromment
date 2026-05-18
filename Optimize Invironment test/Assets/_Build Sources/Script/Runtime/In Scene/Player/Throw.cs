using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class Throw : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField] private GameObject prefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform directionReference;

    [Header("Throw")]
    [SerializeField] private float throwForce = 12f;
    [SerializeField] private float upwardForce = 0.5f;
    [SerializeField] private float spawnForwardOffset = 0.5f;
    [SerializeField] private float cooldown = 0.15f;
    [SerializeField] private bool inheritOwnerVelocity;

    private Rigidbody ownerRigidbody;
    private float nextThrowTime;

    private void Awake()
    {
        ownerRigidbody = GetComponentInParent<Rigidbody>();
    }

    private void Update()
    {
        if (WasThrowPressed())
        {
            ThrowObject();
        }
    }

    public void ThrowObject()
    {
        if (Time.time < nextThrowTime)
        {
            return;
        }

        if (prefab == null)
        {
            Debug.LogWarning($"{nameof(Throw)} on {name} has no prefab assigned.", this);
            return;
        }

        nextThrowTime = Time.time + cooldown;

        Vector3 direction = GetThrowDirection();
        Vector3 spawnPosition = GetSpawnPosition(direction);
        Quaternion spawnRotation = Quaternion.LookRotation(direction, Vector3.up);

        GameObject instance = Instantiate(prefab, spawnPosition, spawnRotation);
        Rigidbody body = instance.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = instance.AddComponent<Rigidbody>();
        }

        Vector3 velocity = direction * throwForce + Vector3.up * upwardForce;
        if (inheritOwnerVelocity && ownerRigidbody != null)
        {
            velocity += ownerRigidbody.linearVelocity;
        }

        body.linearVelocity = velocity;
        body.angularVelocity = Vector3.zero;
    }

    private Vector3 GetThrowDirection()
    {
        Transform reference = directionReference != null ? directionReference : spawnPoint != null ? spawnPoint : transform;
        Vector3 direction = reference.forward;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return direction.normalized;
    }

    private Vector3 GetSpawnPosition(Vector3 direction)
    {
        if (spawnPoint != null)
        {
            return spawnPoint.position;
        }

        return transform.position + direction * spawnForwardOffset;
    }

    private static bool WasThrowPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    private void OnValidate()
    {
        throwForce = Mathf.Max(0f, throwForce);
        upwardForce = Mathf.Max(0f, upwardForce);
        spawnForwardOffset = Mathf.Max(0f, spawnForwardOffset);
        cooldown = Mathf.Max(0f, cooldown);
    }
}
