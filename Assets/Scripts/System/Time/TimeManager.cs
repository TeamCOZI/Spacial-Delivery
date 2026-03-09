using UnityEngine;
using System.Collections.Generic;
using System;
using TMPro;

public class TimeManager : MonoBehaviour
{
    private const float PausedTimeScale = 1f;

    [Header("Debug")]
    public TextMeshProUGUI globalFrameText;
    public PeriodVisualizer periodVisualizer;
    public static TimeManager Instance { get; private set; }

    public static event Action OnGlobalPeriodCompleted;

    private readonly List<BaseTimeRecorder> statefulRecorders = new List<BaseTimeRecorder>();
    private readonly List<OrbitRevolution> statelessOrbiters = new List<OrbitRevolution>();
    private enum TimeState { Playing, Paused, FastForward, Rewinding, Replaying }
    private TimeState currentState;

    public int GlobalFrame { get; private set; } = 0;
    public int PeriodStartFrame { get; private set; } = 0;
    public bool IsPaused => currentState == TimeState.Paused;
    private int cleanupCounter;
    private const int NullCleanupIntervalFrames = 30;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        SetState(TimeState.Playing, 1f);
    }

    private void Update()
    {
        if (globalFrameText != null)
        {
            globalFrameText.SetText("Global Frame: {0}", GlobalFrame);
        }
    }

    private void FixedUpdate()
    {
        CompactStatefulRecorders();

        cleanupCounter++;
        if (cleanupCounter >= NullCleanupIntervalFrames)
        {
            cleanupCounter = 0;
            CompactStatelessOrbiters();
        }

        AdvanceGlobalFrame();
        UpdateRecorderVisibility();
        ProcessCurrentState();

        CheckGlobalPeriod();
    }

    private void AdvanceGlobalFrame()
    {
        if (IsForwardAdvancingState(currentState))
        {
            GlobalFrame++;
            return;
        }

        if (currentState == TimeState.Rewinding)
        {
            GlobalFrame--;
        }
    }

    private void UpdateRecorderVisibility()
    {
        for (int i = 0; i < statefulRecorders.Count; i++)
        {
            statefulRecorders[i].UpdateVisibility(GlobalFrame);
        }
    }

    private void ProcessCurrentState()
    {
        switch (currentState)
        {
            case TimeState.Rewinding:
                ProcessRewind();
                break;
            case TimeState.Replaying:
                ProcessReplay();
                break;
            case TimeState.Playing:
            case TimeState.FastForward:
                ProcessRecord();
                break;
            case TimeState.Paused:
                break;
        }
    }

    private void ProcessRewind()
    {
        for (int i = 0; i < statefulRecorders.Count; i++)
        {
            statefulRecorders[i].Rewind();
        }
    }

    private void ProcessReplay()
    {
        for (int i = 0; i < statefulRecorders.Count; i++)
        {
            statefulRecorders[i].Replay();
        }

        if (!AreAllRecordersDoneReplaying()) return;

        if (Time.timeScale > 1f)
        {
            SetState(TimeState.FastForward, Time.timeScale);
        }
        else
        {
            SetState(TimeState.Playing, 1f);
        }
    }

    private void ProcessRecord()
    {
        for (int i = 0; i < statefulRecorders.Count; i++)
        {
            statefulRecorders[i].Record();
        }
    }

    private void CheckGlobalPeriod()
    {
        if (periodVisualizer == null) return;
        if (!IsGlobalPeriodProgressState(currentState)) return;

        int totalPeriodLcm = periodVisualizer.totalPeriodLcm * 100;

        if (totalPeriodLcm <= 0) return;

        if (GlobalFrame >= PeriodStartFrame + totalPeriodLcm)
        {
            PeriodStartFrame += totalPeriodLcm;
            OnGlobalPeriodCompleted?.Invoke();
        }
    }

    private static bool IsForwardAdvancingState(TimeState state)
    {
        return state == TimeState.Playing || state == TimeState.FastForward || state == TimeState.Replaying;
    }

    private static bool IsGlobalPeriodProgressState(TimeState state)
    {
        return state == TimeState.Playing || state == TimeState.FastForward;
    }

    public void Play()
    {
        if (currentState == TimeState.Rewinding || (currentState == TimeState.Paused && HasPendingReplayRecorders()))
        {
            SetState(TimeState.Replaying, 1f);
        }
        else
        {
            SetState(TimeState.Playing, 1f);
        }
    }

    public void Pause()
    {
        // Keep engine loops running while paused; movement systems are paused by state checks.
        SetState(TimeState.Paused, PausedTimeScale);
    }

    public void FastForward(float multiplier)
    {
        if (HasPendingReplayRecorders())
        {
            SetState(TimeState.Replaying, multiplier);
        }
        else
        {
            SetState(TimeState.FastForward, multiplier);
        }
    }

    public void StartRewind()
    {
        SetState(TimeState.Rewinding, 1f);
    }

    private void SetState(TimeState newState, float newTimeScale)
    {
        CompactStatefulRecorders();
        CompactStatelessOrbiters();

        TimeState oldState = currentState;
        currentState = newState;
        Time.timeScale = newTimeScale;

        ApplyOrbiterState(newState);
        ApplyRecorderStateTransition(oldState, newState);
    }

    public void Register(BaseTimeRecorder recorder)
    {
        if (recorder == null) return;
        if (!statefulRecorders.Contains(recorder))
        {
            statefulRecorders.Add(recorder);
        }
    }

    public void Deregister(BaseTimeRecorder recorder)
    {
        if (recorder == null) return;
        statefulRecorders.Remove(recorder);
    }

    public void Register(OrbitRevolution orbiter)
    {
        if (orbiter == null) return;
        if (!statelessOrbiters.Contains(orbiter))
        {
            statelessOrbiters.Add(orbiter);
        }
    }

    public void Deregister(OrbitRevolution orbiter)
    {
        if (orbiter == null) return;
        statelessOrbiters.Remove(orbiter);
    }

    public bool IsPausedOrRewinding()
    {
        return currentState == TimeState.Paused || currentState == TimeState.Rewinding;
    }

    [Obsolete("Use IsPausedOrRewinding().")]
    public bool IspausedOrRewinding()
    {
        return IsPausedOrRewinding();
    }

    private bool HasPendingReplayRecorders()
    {
        return HasRecorderPendingReplay();
    }

    private bool AreAllRecordersDoneReplaying()
    {
        return !HasRecorderPendingReplay();
    }

    private bool HasRecorderPendingReplay()
    {
        for (int i = 0; i < statefulRecorders.Count; i++)
        {
            BaseTimeRecorder recorder = statefulRecorders[i];
            if (!recorder.isDoneReplaying()) return true;
        }
        return false;
    }

    private void ApplyOrbiterState(TimeState state)
    {
        float orbiterMultiplier = 1f;
        if (state == TimeState.Paused) orbiterMultiplier = 0f;
        else if (state == TimeState.Rewinding) orbiterMultiplier = -1f;

        for (int i = 0; i < statelessOrbiters.Count; i++)
        {
            statelessOrbiters[i].timeMultiplier = orbiterMultiplier;
        }
    }

    private void ApplyRecorderStateTransition(TimeState oldState, TimeState newState)
    {
        if (newState == TimeState.Playing || newState == TimeState.FastForward)
        {
            if (oldState != TimeState.Playing && oldState != TimeState.FastForward)
            {
                for (int i = 0; i < statefulRecorders.Count; i++)
                {
                    BaseTimeRecorder recorder = statefulRecorders[i];
                    recorder.ClearFutureHistory();
                    recorder.SetKinematic(false);
                    recorder.ApplyFinalState();
                }
            }
            return;
        }

        if (newState == TimeState.Paused)
        {
            for (int i = 0; i < statefulRecorders.Count; i++)
            {
                BaseTimeRecorder recorder = statefulRecorders[i];
                recorder.SetKinematic(true);
            }
            return;
        }

        bool isRewindingOrReplaying = (newState == TimeState.Rewinding || newState == TimeState.Replaying);
        if (!isRewindingOrReplaying) return;

        for (int i = 0; i < statefulRecorders.Count; i++)
        {
            statefulRecorders[i].SetKinematic(true);
        }
    }

    private void CompactStatefulRecorders()
    {
        int count = statefulRecorders.Count;
        int writeIndex = 0;
        for (int readIndex = 0; readIndex < count; readIndex++)
        {
            BaseTimeRecorder recorder = statefulRecorders[readIndex];
            if (recorder == null) continue;

            if (writeIndex != readIndex)
            {
                statefulRecorders[writeIndex] = recorder;
            }
            writeIndex++;
        }

        if (writeIndex < count)
        {
            statefulRecorders.RemoveRange(writeIndex, count - writeIndex);
        }
    }

    private void CompactStatelessOrbiters()
    {
        int count = statelessOrbiters.Count;
        int writeIndex = 0;
        for (int readIndex = 0; readIndex < count; readIndex++)
        {
            OrbitRevolution orbiter = statelessOrbiters[readIndex];
            if (orbiter == null) continue;

            if (writeIndex != readIndex)
            {
                statelessOrbiters[writeIndex] = orbiter;
            }
            writeIndex++;
        }

        if (writeIndex < count)
        {
            statelessOrbiters.RemoveRange(writeIndex, count - writeIndex);
        }
    }
}
