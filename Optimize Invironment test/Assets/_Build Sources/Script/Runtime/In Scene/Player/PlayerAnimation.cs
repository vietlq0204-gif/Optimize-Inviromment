using UnityEngine;

[RequireComponent(typeof(Animator))]
public sealed class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private string velocityParameter = "Velocity";
    [SerializeField] private float dampTime = 0.1f;
    [SerializeField] private float fallbackMaxPlanarSpeed = 5f;

    private Animator animatorComponent;
    private Rigidbody rigidbodyComponent;
    private PlayerControllerSimple playerController;
    private int velocityParameterHash;

    private void Awake()
    {
        animatorComponent = GetComponent<Animator>();
        rigidbodyComponent = GetComponent<Rigidbody>();
        playerController = GetComponent<PlayerControllerSimple>();
        velocityParameterHash = Animator.StringToHash(velocityParameter);
    }

    private void Update()
    {
        float normalizedVelocity = GetNormalizedVelocity();
        animatorComponent.SetFloat(velocityParameterHash, normalizedVelocity, dampTime, Time.deltaTime);
    }
    
    private float GetNormalizedVelocity()
    {
        float planarSpeed = playerController != null
            ? playerController.CurrentPlanarSpeed
            : GetPlanarSpeedFromRigidbody();

        float maxPlanarSpeed = playerController != null
            ? Mathf.Max(playerController.MoveSpeed, 0.01f)
            : Mathf.Max(fallbackMaxPlanarSpeed, 0.01f);

        return Mathf.Clamp01(planarSpeed / maxPlanarSpeed);
    }

    private float GetPlanarSpeedFromRigidbody()
    {
        if (rigidbodyComponent == null)
        {
            return 0f;
        }

        Vector3 planarVelocity = rigidbodyComponent.linearVelocity;
        planarVelocity.y = 0f;
        return planarVelocity.magnitude;
    }
}
