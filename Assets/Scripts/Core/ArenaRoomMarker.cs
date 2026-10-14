using UnityEngine;

/// <summary>
/// Marks the Arena's root GameObject (the fixed-camera boss/kitchen pocket room) so
/// <see cref="ArenaState"/> can find and activate/deactivate it without a hardcoded object
/// name. Exactly one should exist per level scene, starting inactive — the Arena is a
/// same-scene room toggled on entry/exit rather than a separately loaded scene, since no
/// scene-loading infrastructure exists in this project yet (see GameDesign.md).
/// </summary>
[DisallowMultipleComponent]
public class ArenaRoomMarker : MonoBehaviour
{
}
