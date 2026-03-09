using System;
using UnityEngine;

public interface IInputService
{
    event Action<Transform> focusEvent;
    event Action<Icon> hoverEvent;
    event Action<Vector2> dragOffsetEvent;
    event Action<float> zoomOffsetEvent;
}
