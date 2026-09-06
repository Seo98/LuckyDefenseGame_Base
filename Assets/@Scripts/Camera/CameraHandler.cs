/*
 * Copyright (C) 2014 Francisco Manuel Garcia Moreno
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Provides a bounded RTS-style camera controlled through the Unity Input System.
/// </summary>
/// <remarks>
/// Controls: WASD or arrow keys to pan, hold the right mouse button and drag to orbit,
/// mouse wheel to zoom, and Escape to restore the initial transform.
/// </remarks>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class CameraHandler : MonoBehaviour
{
    [Header("Features")]
    [SerializeField] private bool canMove = true;
    [SerializeField] private bool canRotate = true;
    [SerializeField] private bool canZoom = true;

    [Header("Pan Bounds")]
    [SerializeField] private float minPosX = -10f;
    [SerializeField] private float maxPosX = 10f;
    [SerializeField] private float minPosZ = -10f;
    [SerializeField] private float maxPosZ = 10f;
    [SerializeField, Min(0f)] private float movementSpeed = 5f;

    [Header("Orbit")]
    [SerializeField] private float minPitch = 10f;
    [SerializeField] private float maxPitch = 90f;
    [SerializeField, Min(0f)] private float rotationSensitivity = 0.15f;

    [Header("Zoom")]
    [Tooltip("The point the camera moves toward and away from while zooming.")]
    [SerializeField] private Transform zoomTarget;
    [SerializeField, Min(0.01f)] private float minZoom = 8f;
    [SerializeField, Min(0.01f)] private float maxZoom = 30f;
    [Tooltip("World-space zoom distance per mouse scroll unit. A Windows mouse wheel notch is commonly 120 units.")]
    [SerializeField, Min(0f)] private float zoomSensitivity = 0.01f;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private float pitch;
    private float yaw;

    /// <summary>Gets or sets whether right-mouse orbiting is enabled.</summary>
    public bool EnableMouseRotate
    {
        get => canRotate;
        set => canRotate = value;
    }

    /// <summary>Gets or sets whether keyboard panning is enabled.</summary>
    public bool EnableKeyPanning
    {
        get => canMove;
        set => canMove = value;
    }

    /// <summary>Gets or sets whether mouse-wheel zooming is enabled.</summary>
    public bool EnableMouseZoom
    {
        get => canZoom;
        set => canZoom = value;
    }

    private void Awake()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        SyncRotationState();
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            ResetCamera();
            return;
        }

        if (canRotate)
        {
            HandleRotation();
        }

        if (canMove)
        {
            HandleMovement();
        }

        if (canZoom)
        {
            HandleZoom();
        }

        ClampPositionToBounds();
    }

    /// <summary>Restores the camera transform captured when this component awoke.</summary>
    public void ResetCamera()
    {
        transform.SetPositionAndRotation(initialPosition, initialRotation);
        SyncRotationState();
        ClampPositionToBounds();
    }

    private void OnValidate()
    {
        minZoom = Mathf.Max(0.01f, minZoom);
        maxZoom = Mathf.Max(minZoom, maxZoom);
        movementSpeed = Mathf.Max(0f, movementSpeed);
        rotationSensitivity = Mathf.Max(0f, rotationSensitivity);
        zoomSensitivity = Mathf.Max(0f, zoomSensitivity);
    }

    private void HandleRotation()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.rightButton.isPressed)
        {
            return;
        }

        Vector2 pointerDelta = mouse.delta.ReadValue();
        if (pointerDelta == Vector2.zero)
        {
            return;
        }

        yaw += pointerDelta.x * rotationSensitivity;
        float lowerPitch = Mathf.Min(minPitch, maxPitch);
        float upperPitch = Mathf.Max(minPitch, maxPitch);
        pitch = Mathf.Clamp(pitch - (pointerDelta.y * rotationSensitivity), lowerPitch, upperPitch);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void HandleMovement()
    {
        Vector2 input = ReadMovementInput();
        if (input == Vector2.zero)
        {
            return;
        }

        Quaternion horizontalRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 forward = horizontalRotation * Vector3.forward;
        Vector3 right = horizontalRotation * Vector3.right;
        Vector3 movement = (right * input.x) + (forward * input.y);

        transform.position += movement * (movementSpeed * Time.deltaTime);
    }

    private void HandleZoom()
    {
        if (zoomTarget == null)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        float scrollY = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollY, 0f))
        {
            return;
        }

        Vector3 offset = transform.position - zoomTarget.position;
        float currentDistance = offset.magnitude;
        if (currentDistance < 0.001f)
        {
            offset = -transform.forward;
            currentDistance = minZoom;
        }

        float nextDistance = Mathf.Clamp(
            currentDistance - (scrollY * zoomSensitivity),
            minZoom,
            maxZoom);

        transform.position = zoomTarget.position + (offset.normalized * nextDistance);
    }

    private Vector2 ReadMovementInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            horizontal -= 1f;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            horizontal += 1f;
        }

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            vertical -= 1f;
        }

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            vertical += 1f;
        }

        return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
    }

    private void ClampPositionToBounds()
    {
        Vector3 position = transform.position;
        position.x = Mathf.Clamp(position.x, Mathf.Min(minPosX, maxPosX), Mathf.Max(minPosX, maxPosX));
        position.z = Mathf.Clamp(position.z, Mathf.Min(minPosZ, maxPosZ), Mathf.Max(minPosZ, maxPosZ));
        transform.position = position;
    }

    private void SyncRotationState()
    {
        pitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
        yaw = transform.eulerAngles.y;
    }
}
