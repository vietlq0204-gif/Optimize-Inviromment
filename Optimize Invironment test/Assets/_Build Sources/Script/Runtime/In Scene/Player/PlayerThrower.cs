using UnityEngine;

[DisallowMultipleComponent]
public class PlayerThrower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private GameObject projectilePrefab;
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

    protected virtual void Awake()
    {
        inputReader = inputReader != null ? inputReader : GetComponent<PlayerInputReader>();
        ownerRigidbody = GetComponentInParent<Rigidbody>();
    }

    protected virtual void Update()
    {
        if (inputReader != null && inputReader.AttackTriggeredThisFrame)
        {
            ThrowObject();
        }
    }

    public virtual bool ThrowObject()
    {
        if (projectilePrefab == null || Time.time < nextThrowTime)
        {
            return false;
        }

        nextThrowTime = Time.time + cooldown;

        Vector3 direction = GetThrowDirection();
        Vector3 spawnPosition = GetSpawnPosition(direction);
        Quaternion spawnRotation = Quaternion.LookRotation(direction, Vector3.up);

        GameObject instance = Instantiate(projectilePrefab, spawnPosition, spawnRotation);
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
        return true;
    }

    private Vector3 GetThrowDirection()
    {
        Transform reference = directionReference != null ? directionReference : spawnPoint != null ? spawnPoint : transform;
        Vector3 direction = reference.forward;
        return direction.sqrMagnitude <= 0.0001f ? Vector3.forward : direction.normalized;
    }

    private Vector3 GetSpawnPosition(Vector3 direction)
    {
        if (spawnPoint != null)
        {
            return spawnPoint.position;
        }

        return transform.position + direction * spawnForwardOffset;
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        throwForce = Mathf.Max(0f, throwForce);
        upwardForce = Mathf.Max(0f, upwardForce);
        spawnForwardOffset = Mathf.Max(0f, spawnForwardOffset);
        cooldown = Mathf.Max(0f, cooldown);
    }
#endif
}
