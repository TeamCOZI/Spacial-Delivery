using System;
using UnityEngine;

public interface IFocusService
{
    Transform CurrentFocus { get; }
    void SetFocus(Transform focusedTransform);
    void RegisterFocusListener(Action<Transform> listener, bool invokeImmediately = true);
    void DeregisterFocusListener(Action<Transform> listener);
}
