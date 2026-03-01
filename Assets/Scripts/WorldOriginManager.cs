using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
public class WorldOriginManager : MonoBehaviour
{
    public static event Action<Vector3> worldShifted;

    [Header("Shift Settings")]
    [Min(1f)] public float shiftThreshold = 2000f;
    [Min(1f)] public float maxShiftPerFrame = 200f;
    public bool shiftOnXYOnly = true;

    [Header("Exclusions")]
    public List<Transform> excludedRoots = new List<Transform>();

    private Camera mainCamera;
    private Transform mainCameraRoot;
    private float thresholdSqr;

    private void Awake()
    {
        mainCamera = Camera.main;
        mainCameraRoot = mainCamera != null ? mainCamera.transform.root : null;
        shiftThreshold = Mathf.Max(1f, WorldScale.ScaleLength(shiftThreshold));
        maxShiftPerFrame = Mathf.Max(1f, WorldScale.ScaleLength(maxShiftPerFrame));
        thresholdSqr = shiftThreshold * shiftThreshold;
    }

    private void LateUpdate()
    {
        // LargeWorldCoordinator handles floating-origin mapping through world/local conversion.
        // Running root-shift logic together causes apparent drag limits around shiftThreshold.
        if (LargeWorldCoordinator.Instance != null) return;

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;
        if (mainCameraRoot == null) mainCameraRoot = mainCamera.transform.root;

        Vector3 cameraPosition = mainCamera.transform.position;
        Vector3 originCheck = shiftOnXYOnly
            ? new Vector3(cameraPosition.x, cameraPosition.y, 0f)
            : cameraPosition;

        if (originCheck.sqrMagnitude < thresholdSqr) return;

        Vector3 shiftDelta = -originCheck;
        float shiftMagnitude = shiftDelta.magnitude;
        if (shiftMagnitude > maxShiftPerFrame)
        {
            shiftDelta = shiftDelta / shiftMagnitude * maxShiftPerFrame;
        }

        ShiftWorldRoots(shiftDelta);
        Physics.SyncTransforms();
        worldShifted?.Invoke(shiftDelta);
    }

    private void ShiftWorldRoots(Vector3 shiftDelta)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            if (root == null) continue;

            Transform rootTransform = root.transform;
            if (IsExcluded(rootTransform)) continue;
            if (IsAutoExcluded(rootTransform)) continue;

            Rigidbody rootRigidbody = rootTransform.GetComponent<Rigidbody>();
            if (rootRigidbody != null)
            {
                rootRigidbody.position += shiftDelta;
                continue;
            }

            rootTransform.position += shiftDelta;
        }
    }

    private bool IsExcluded(Transform root)
    {
        if (root == null) return true;

        foreach (Transform excluded in excludedRoots)
        {
            if (excluded == null) continue;
            if (root == excluded.root) return true;
        }

        return false;
    }

    // Keep camera and core manager roots out of direct transform shifts.
    // Their positions are resolved by their own systems in LateUpdate.
    private bool IsAutoExcluded(Transform root)
    {
        if (root == null) return true;
        if (mainCameraRoot != null && root == mainCameraRoot) return true;
        if (root.GetComponentInChildren<WorldOriginManager>(true) != null) return true;
        if (root.GetComponentInChildren<ScaledSpaceManager>(true) != null) return true;
        return false;
    }
}
