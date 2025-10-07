using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using TMPro;

public class TimeManager : MonoBehaviour
{
    [Header("Debug")]
    public TextMeshProUGUI globalFrameText;

    public static TimeManager Instance { get; private set; }

    public static event Action OnGlobalPeriodCompleted;

    private List<BaseTimeRecorder> statefulRecorders = new List<BaseTimeRecorder>();
    private List<Orbiter> statelessOrbiters = new List<Orbiter>();
    private enum TimeState { Playing, Paused, FastForward, Rewinding, Replaying }
    private TimeState currentState;

    public int GlobalFrame { get; private set; } = 0;
    public int PeriodStartFrame { get; private set; } = 0;

    private Orbiter mainOrbiter;
    private float previousMainOrbiterAngle;
    private bool mainOrbiterInitialized = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
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
            globalFrameText.text = "Global Frame: " + GlobalFrame.ToString();
        }
    }

    private void FixedUpdate()
    {
        if (currentState == TimeState.Playing || currentState == TimeState.FastForward || currentState == TimeState.Replaying)
        {
            GlobalFrame++;
        }
        else if (currentState == TimeState.Rewinding)
        {
            GlobalFrame--;
        }

        foreach (var recorder in statefulRecorders)
        {
            if (recorder != null) recorder.UpdateVisibility(GlobalFrame);
        }

        if (statefulRecorders == null) return;

        switch (currentState)
        {
            case TimeState.Rewinding:
                foreach (var recorder in statefulRecorders) recorder.Rewind();
                break;

            case TimeState.Replaying:
                foreach (var recorder in statefulRecorders) recorder.Replay();
                if (statefulRecorders.All(r => r.isDoneReplaying()))
                {
                    if (Time.timeScale > 1f)
                    {
                        SetState(TimeState.FastForward, Time.timeScale);
                    }
                    else
                    {
                        SetState(TimeState.Playing, 1f);
                    }
                }
                break;

            case TimeState.Playing:
            case TimeState.FastForward:
                foreach (var recorder in statefulRecorders) recorder.Record();
                break;

            case TimeState.Paused:
                break;
        }

        CheckGlobalPeriod();
    }

    private void CheckGlobalPeriod()
    {
        if (statelessOrbiters.Count == 0) return;

        if (!mainOrbiterInitialized)
        {
            mainOrbiter = statelessOrbiters.OrderBy(o => o.orbitSpeed).FirstOrDefault();
            if (mainOrbiter != null)
            {
                previousMainOrbiterAngle = mainOrbiter.currentAngle;
                mainOrbiterInitialized = true;
            }
            return;
        }

        if (mainOrbiter != null && (currentState == TimeState.Playing || currentState == TimeState.FastForward))
        {
            if (previousMainOrbiterAngle > mainOrbiter.currentAngle && Mathf.Abs(previousMainOrbiterAngle - mainOrbiter.currentAngle) > 180)
            {
                PeriodStartFrame = GlobalFrame;
                OnGlobalPeriodCompleted?.Invoke();
            }
            previousMainOrbiterAngle = mainOrbiter.currentAngle;
        }
    }

    public void Play()
    {
        if (currentState == TimeState.Rewinding || (currentState == TimeState.Paused && statefulRecorders.Any(r => !r.isDoneReplaying())))
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
        SetState(TimeState.Paused, 0f);
    }

    public void FastForward(float multiplier)
    {
        if (statefulRecorders.Any(r => !r.isDoneReplaying()))
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
        TimeState oldState = currentState;
        currentState = newState;
        Time.timeScale = newTimeScale;

        float orbiterMultiplier = 1f;
        if (newState == TimeState.Paused) orbiterMultiplier = 0f;
        else if (newState == TimeState.Rewinding) orbiterMultiplier = -1f;

        foreach (var orbiter in statelessOrbiters)
        {
            orbiter.timeMultiplier = orbiterMultiplier;
        }

        if (newState == TimeState.Playing || newState == TimeState.FastForward)
        {
            if (oldState != TimeState.Playing && oldState != TimeState.FastForward)
            {
                foreach (var recorder in statefulRecorders)
                {
                    if (recorder == null) continue;
                    recorder.ClearFutureHistory();
                    recorder.SetKinematic(false);
                    recorder.ApplyFinalState();
                }
            }
        }
        else
        {
            bool isRewindingOrReplaying = (newState == TimeState.Rewinding || newState == TimeState.Replaying);
            if (isRewindingOrReplaying)
            {
                foreach (var recorder in statefulRecorders)
                {
                    recorder.SetKinematic(true);
                }
            }
        }
    }

    public void Register(BaseTimeRecorder recorder)
    {
        if (!statefulRecorders.Contains(recorder))
        {
            statefulRecorders.Add(recorder);
        }
    }

    public void Deregister(BaseTimeRecorder recorder)
    {
        if (statefulRecorders.Contains(recorder))
        {
            statefulRecorders.Remove(recorder);
        }
    }

    public void Register(Orbiter orbiter)
    {
        if (!statelessOrbiters.Contains(orbiter))
        {
            statelessOrbiters.Add(orbiter);
            mainOrbiterInitialized = false;
        }
    }

    public void Deregister(Orbiter orbiter)
    {
        if (statelessOrbiters.Contains(orbiter))
        {
            statelessOrbiters.Remove(orbiter);
            mainOrbiterInitialized = false;
        }
    }

    public bool IspausedOrRewinding()
    {
        return currentState == TimeState.Paused || currentState == TimeState.Rewinding;
    }
}