using UnityEngine;
using System.Collections.Generic;

public class PhysicsTimeRecorder : BaseTimeRecorder
{
    private int frame = 0;
    private int birthFrame;
    private bool isBirthFrameInitialized;

    private List<Vector3> positions;
    private List<Quaternion> rotations;
    private List<Vector3> velocities;
    private List<Vector3> angularVelocities;

    private void OnEnable()
    {
        WorldOriginManager.worldShifted += HandleWorldShift;
    }

    private void OnDisable()
    {
        WorldOriginManager.worldShifted -= HandleWorldShift;
    }

    protected override void Start()
    {
        base.Start();
        TryInitializeBirthFrame();

        positions = new List<Vector3>();
        rotations = new List<Quaternion>();
        velocities = new List<Vector3>();
        angularVelocities = new List<Vector3>();
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        TryInitializeBirthFrame();
    }

    public override void UpdateVisibility(int globalFrame)
    {
        bool shouldBeActive = (globalFrame >= birthFrame);
        if (gameObject.activeSelf != shouldBeActive)
        {
            gameObject.SetActive(shouldBeActive);
        }
    }

    public override void Record()
    {
        if (!gameObject.activeSelf) return;

        if (frame < positions.Count)
        {
            positions.RemoveRange(frame, positions.Count - frame);
            rotations.RemoveRange(frame, rotations.Count - frame);
            velocities.RemoveRange(frame, velocities.Count - frame);
            angularVelocities.RemoveRange(frame, angularVelocities.Count - frame);
        }

        positions.Add(transform.position);
        rotations.Add(transform.rotation);
        velocities.Add(rb.linearVelocity);
        angularVelocities.Add(rb.angularVelocity);
        frame++;
    }

    public override void Rewind()
    {
        if (!gameObject.activeSelf) return;

        if (frame > 0)
        {
            frame--;
            ApplyFrame(frame);

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }
    }

    public override void Replay()
    {
        if (!gameObject.activeSelf) return;
        
        if (frame < positions.Count)
        {
            ApplyFrame(frame);
            frame++;
        }
    }

    private void ApplyFrame(int frameToApply)
    {
        transform.position = positions[frameToApply];
        transform.rotation = rotations[frameToApply];
    }

    public override bool isDoneReplaying()
    {
        return frame >= positions.Count;
    }

    public override void ClearFutureHistory()
    {
        if (frame < positions.Count)
        {
            positions.RemoveRange(frame, positions.Count - frame);
            rotations.RemoveRange(frame, rotations.Count - frame);
            velocities.RemoveRange(frame, velocities.Count - frame);
            angularVelocities.RemoveRange(frame, angularVelocities.Count - frame);
        }
    }

    public override void ApplyFinalState()
    {
        if (rb != null && frame > 0 && frame <= positions.Count)
        {
            int lastFrame = frame - 1;
            rb.linearVelocity = velocities[lastFrame];
            rb.angularVelocity = angularVelocities[lastFrame];
        }
    }

    private void HandleWorldShift(Vector3 shiftDelta)
    {
        if (positions == null || positions.Count == 0) return;

        for (int i = 0; i < positions.Count; i++)
        {
            positions[i] += shiftDelta;
        }
    }

    private void TryInitializeBirthFrame()
    {
        if (isBirthFrameInitialized) return;
        if (!CoreRuntimeAccess.TryGetTimeManager(out TimeManager timeManager)) return;

        birthFrame = timeManager.GlobalFrame;
        isBirthFrameInitialized = true;
    }
}
