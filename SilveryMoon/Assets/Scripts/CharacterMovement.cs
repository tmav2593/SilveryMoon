using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Paper-Mario style movement for a 2D sprite living in a 3D world (new Input System).
/// - Movement on XZ plane using a CharacterController.
/// - Keeps sprite facing the camera (billboard).
/// - Flips sprite horizontally when moving left/right relative to camera (uses SpriteRenderer.flipX by default).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PaperMarioMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float acceleration = 20f;
    public float deceleration = 25f;
    public float rotationSmoothSpeed = 12f;

    [Header("References")]
    public Transform cameraTransform;   // leave null to auto-find Camera.main
    public Transform spriteTransform;   // the child that holds sprite/animator
    public SpriteRenderer spriteRenderer; // optional, used for flipX
    public Animator animator;           // optional

    [Header("Options")]
    public bool cameraRelative = true;
    public bool allowRun = true;
    public bool flipSpriteBasedOnMovement = true; // flip sprite horizontally when moving left/right
    public float spriteFlipDeadzone = 0.15f; // minimum horizontal input to flip

    // runtime
    CharacterController cc;
    Vector3 currentVelocity = Vector3.zero;
    Vector2 moveInput = Vector2.zero;

    // input system
    InputAction moveAction;
    InputAction runAction;

    const float k_InputDeadzone = 0.01f;

    void Awake()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        cc = GetComponent<CharacterController>();

        if (spriteTransform == null)
        {
            var t = transform.Find("Sprite");
            if (t != null) spriteTransform = t;
        }

        if (spriteRenderer == null && spriteTransform != null)
        {
            spriteRenderer = spriteTransform.GetComponent<SpriteRenderer>();
        }

        if (animator == null && spriteTransform != null)
        {
            animator = spriteTransform.GetComponent<Animator>();
        }

        // Build input actions in code
        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddBinding("<Keyboard>/arrowKeys");
        moveAction.AddBinding("<Gamepad>/leftStick");
        moveAction.Enable();

        runAction = new InputAction("Run", InputActionType.Button);
        runAction.AddBinding("<Keyboard>/leftShift");
        runAction.AddBinding("<Gamepad>/buttonSouth");
        runAction.Enable();
    }

    void OnEnable()
    {
        moveAction?.Enable();
        runAction?.Enable();
    }

    void OnDisable()
    {
        moveAction?.Disable();
        runAction?.Disable();
    }

    void Update()
    {
        // read input
        moveInput = moveAction.ReadValue<Vector2>();
        if (moveInput.magnitude < k_InputDeadzone) moveInput = Vector2.zero;

        // desired direction in world space (XZ)
        Vector3 desiredDir;
        if (cameraRelative && cameraTransform != null)
        {
            Vector3 camForward = cameraTransform.forward;
            camForward.y = 0f;
            camForward.Normalize();

            Vector3 camRight = cameraTransform.right;
            camRight.y = 0f;
            camRight.Normalize();

            desiredDir = camRight * moveInput.x + camForward * moveInput.y;
        }
        else
        {
            desiredDir = new Vector3(moveInput.x, 0f, moveInput.y);
        }

        // select speed
        float targetMaxSpeed = walkSpeed;
        if (allowRun && runAction != null && runAction.IsPressed()) targetMaxSpeed = runSpeed;

        Vector3 desiredVelocity = desiredDir.sqrMagnitude > 0.0001f ? desiredDir.normalized * targetMaxSpeed : Vector3.zero;

        // smooth velocity with acceleration/deceleration
        float maxDelta = (desiredVelocity.sqrMagnitude > 0.0001f ? acceleration : deceleration) * Time.deltaTime;
        currentVelocity = Vector3.MoveTowards(currentVelocity, desiredVelocity, maxDelta);

        // apply movement
        cc.Move(currentVelocity * Time.deltaTime);

        // rotate body toward movement direction (for shadow/feet)
        Vector3 flatVel = currentVelocity;
        flatVel.y = 0f;
        if (flatVel.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(flatVel.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSmoothSpeed * Time.deltaTime);
        }

        // billboard the sprite (keep it facing the camera upright)
        if (spriteTransform != null && cameraTransform != null)
        {
            Vector3 dirToCamera = cameraTransform.position - spriteTransform.position;
            dirToCamera.y = 0f;
            if (dirToCamera.sqrMagnitude > 0.00001f)
            {
                spriteTransform.rotation = Quaternion.LookRotation(dirToCamera);
            }
        }

        // flip sprite horizontally based on movement direction relative to camera
        if (flipSpriteBasedOnMovement && spriteRenderer != null)
        {
            float horizontal = 0f;

            // prefer using camera-space input if available
            if (cameraRelative && cameraTransform != null)
            {
                horizontal = moveInput.x;
            }
            else
            {
                // fallback: determine horizontal in camera space using world velocity and camera right
                if (cameraTransform != null)
                {
                    Vector3 flatVelNorm = flatVel.sqrMagnitude > 0.0001f ? flatVel.normalized : Vector3.zero;
                    horizontal = Vector3.Dot(flatVelNorm, cameraTransform.right);
                }
                else
                {
                    // fallback local X velocity
                    horizontal = flatVel.x;
                }
            }

            if (Mathf.Abs(horizontal) > spriteFlipDeadzone)
            {
                // when horizontal < 0, we consider that "left" relative to camera, so flipX = true
                spriteRenderer.flipX = horizontal < 0f;
            }
            // when within deadzone, we don't change flip to avoid jitter
        }

        // animator parameters
        if (animator != null)
        {
            float speed = new Vector3(currentVelocity.x, 0f, currentVelocity.z).magnitude;
            animator.SetFloat("Speed", speed);

            if (cameraRelative)
            {
                animator.SetFloat("MoveX", moveInput.x);
                animator.SetFloat("MoveY", moveInput.y);
            }
            else
            {
                Vector3 localDir = transform.InverseTransformDirection(flatVel.normalized);
                animator.SetFloat("MoveX", localDir.x);
                animator.SetFloat("MoveY", localDir.z);
            }
        }
    }

    public float GetCurrentSpeed() => new Vector3(currentVelocity.x, 0f, currentVelocity.z).magnitude;
}