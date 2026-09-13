using UnityEngine;

/// <summary>
/// Marker component. Add it to any collider a designer wants the waffle monster to
/// auto-climb — building walls, ledges, drainpipes. The presence of this component (on the
/// contacted collider or a parent) is the <em>only</em> thing that puts
/// <see cref="PlayerController"/> into the climb state; plain colliders are never climbable.
/// It can sit on a solid collider or a trigger.
/// </summary>
[DisallowMultipleComponent]
public class ClimbableSurface : MonoBehaviour
{
}
