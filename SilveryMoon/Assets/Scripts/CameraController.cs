using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Smooth follow camera for a Paper-Mario-like scene. Designed to work with PaperMarioMovement.cs.
/// - Smoothly follows a target (LateUpdate).
/// - Optional manual orbit via mouse/right stick.
/// - Zoom in/out with mouse wheel / gamepad triggers.
/// - Simple collision clipping (raycast from target to desiredCameraPosition).
/// - Uses new Input System created in code (no asset required).
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3 offset = new Vector3(0f, 4f, -6f); // local offset (applied after rotation)
    public bool useFixedAngle = false; // if true, camera won't orbit; it will maintain target yaw

    [Header("Follow")]
    public float followSmoothTime = 0.12f;
    public float rotationSmoothSpeed = 8f;

    [Header("Orbit / Look")]
    public float yawSpeed = 120f; // degrees/sec for mouse/gamepad
    public float pitch = 30f;
    public float minPitch = 10f;
    public float maxPitch = 80f;
    public bool invertY = false;

    [Header("Zoom")]
    public float minDistance = 2f;
    public float maxDistance = 12f;
    public float zoomSpeed = 2f;

    [Header("Collision")]
    public LayerMask collisionMask = ~0; // default all layers
    public float collisionOffset = 0.2f;

    // runtime
    float yaw = 0f;
    float currentDistance;
    Vector3 velocity = Vector3.zero;

    // input
    InputAction lookAction;
    InputAction zoomAction;

    void Awake()
    {
        currentDistance = -offset.z;
        if (currentDistance < 0) currentDistance = Mathf.Clamp(Mathf.Abs(currentDistance), minDistance, maxDistance);

        // build InputActions
        lookAction = new InputAction("CameraLook", InputActionType.Value);
        lookAction.AddBinding("<Mouse>/delta");
        lookAction.AddBinding("<Gamepad>/rightStick");
        lookAction.Enable();

        zoomAction = new InputAction("CameraZoom", InputActionType.Value);
        zoomAction.AddBinding("<Mouse>/scroll");
        // gamepad zoom could be triggers - example:
        zoomAction.AddBinding("<Gamepad>/leftTrigger");
        zoomAction.AddBinding("<Gamepad>/rightTrigger");
        zoomAction.Enable();

        // initialize yaw from current rotation
        yaw = transform.eulerAngles.y;
    }

    void OnEnable()
    {
        lookAction?.Enable();
        zoomAction?.Enable();
    }

    void OnDisable()
    {
        lookAction?.Disable();
        zoomAction?.Disable();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // read look input
        Vector2 lookDelta = lookAction.ReadValue<Vector2>();
        // adjust sensitivity/time
        // mouse returns pixels per frame, gamepad returns (-1..1). We'll scale mouse down by a factor.
        float mouseScale = 0.02f;
        Vector2 adjusted = lookDelta * mouseScale;
        // For gamepad rightStick, values are small already so it's fine to add yawSpeed later.
        float horizontalInput = adjusted.x + lookDelta.x; // conservative mixing (mouse and gamepad)
        float verticalInput = adjusted.y + lookDelta.y;

        // If using fixed angle, do not allow yaw change by player; otherwise apply orbit
        if (!useFixedAngle)
        {
            // yaw change (horizontal)
            yaw += horizontalInput * yawSpeed * Time.deltaTime;
        }

        // pitch
        float pitchDelta = -verticalInput * yawSpeed * Time.deltaTime * (invertY ? -1f : 1f);
        pitch = Mathf.Clamp(pitch + pitchDelta, minPitch, maxPitch);

        // zoom
        Vector2 zoomRaw = zoomAction.ReadValue<Vector2>();
        float zoomDelta = 0f;
        // mouse scroll typically sets y, gamepad triggers produce single value; handle both:
        if (Mathf.Abs(zoomRaw.y) > 0.0001f) zoomDelta = -zoomRaw.y * zoomSpeed; // mouse
        else if (Mathf.Abs(zoomRaw.x) > 0.0001f) zoomDelta = (zoomRaw.x - 0.5f) * zoomSpeed * 0.5f;
        // clamp distance
        currentDistance = Mathf.Clamp(currentDistance + zoomDelta, minDistance, maxDistance);

        // Desired rotation and position
        Quaternion desiredRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredOffset = desiredRot * new Vector3(0f, 0f, -currentDistance) + Vector3.up * offset.y;
        Vector3 desiredPos = target.position + desiredOffset + Vector3.up * 0f; // optional extra offset

        // collision: raycast from target position up to desiredPos
        Vector3 origin = target.position + Vector3.up * 0.5f; // small eye height to avoid ground hits
        Vector3 dir = (desiredPos - origin);
        float dist = dir.magnitude;
        if (dist > 0.001f)
        {
            RaycastHit hit;
            if (Physics.SphereCast(origin, 0.2f, dir.normalized, out hit, dist, collisionMask))
            {
                // place camera just before the hit point
                desiredPos = origin + dir.normalized * Mathf.Max(0.1f, hit.distance - collisionOffset);
            }
        }

        // smooth position
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref velocity, followSmoothTime);

        // look at target (smooth)
        Quaternion lookRot = Quaternion.LookRotation((target.position - transform.position).normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotationSmoothSpeed * Time.deltaTime);
    }
}