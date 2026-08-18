using UnityEngine;

[DisallowMultipleComponent]
public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private Transform interactionOrigin;

    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private LayerMask interactionMask = ~0;

    public bool HasInteractTarget { get; private set; }
    public RaycastHit LastHit { get; private set; }

    protected virtual void Awake()
    {
        ResolveReferences();
    }

    protected virtual void OnEnable()
    {
        ResolveReferences();
    }

    protected virtual void Update()
    {
        if (inputReader == null || !inputReader.InteractTriggeredThisFrame)
        {
            return;
        }

        TryInteract();
    }

    public bool TryInteract()
    {
        Transform origin = GetInteractionOrigin();
        if (origin == null)
        {
            HasInteractTarget = false;
            return false;
        }

        if (!Physics.Raycast(origin.position, origin.forward, out RaycastHit hit, interactionDistance, interactionMask, QueryTriggerInteraction.Ignore))
        {
            HasInteractTarget = false;
            return false;
        }

        LastHit = hit;
        HasInteractTarget = true;

        IPlayerInteractable interactable = hit.collider.GetComponentInParent<IPlayerInteractable>();
        if (interactable != null)
        {
            interactable.Interact(this);
            return true;
        }

        hit.collider.SendMessage("Interact", this, SendMessageOptions.DontRequireReceiver);
        return true;
    }

    private Transform GetInteractionOrigin()
    {
        if (interactionOrigin == null)
        {
            ResolveReferences();
        }

        return interactionOrigin;
    }

    private void ResolveReferences()
    {
        if (inputReader == null)
        {
            inputReader = GetComponent<PlayerInputReader>();
        }

        if (interactionOrigin == null)
        {
            PlayerMotor playerMotor = GetComponent<PlayerMotor>();
            if (playerMotor != null && playerMotor.CameraTransform != null)
            {
                interactionOrigin = playerMotor.CameraTransform;
            }
            else if (Camera.main != null)
            {
                interactionOrigin = Camera.main.transform;
            }
            else
            {
                interactionOrigin = transform;
            }
        }
    }
}
