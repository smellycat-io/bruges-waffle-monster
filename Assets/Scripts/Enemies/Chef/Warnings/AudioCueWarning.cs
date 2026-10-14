using UnityEngine;

/// <summary>
/// The audio warning layer: volume/tempo ramp with proximity to an ACTUAL return, same
/// reliability characteristics as <see cref="SilhouetteWarning"/> (real returns only — no
/// fake-out audio ramp, per product decision; a subtle audio fake-out was flagged as an option
/// and declined for this pass). Reads <see cref="ChefController"/> only; independently
/// testable via the pure <see cref="ComputeProximity"/> calculation.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioCueWarning : MonoBehaviour
{
    [SerializeField] private ChefController chefController;
    [SerializeField, Min(0f), Tooltip("PLACEHOLDER")] private float minVolume;
    [SerializeField, Min(0f), Tooltip("PLACEHOLDER")] private float maxVolume = 1f;
    [SerializeField, Min(0.01f), Tooltip("PLACEHOLDER")] private float minPitch = 1f;
    [SerializeField, Min(0.01f), Tooltip("PLACEHOLDER")] private float maxPitch = 1.5f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (chefController == null)
        {
            chefController = FindAnyObjectByType<ChefController>();
        }
    }

    private void Update() => Tick(Time.deltaTime);

    /// <summary>Exposed publicly so tests can drive it with synthetic time.</summary>
    public void Tick(float deltaTime)
    {
        if (chefController == null)
        {
            return;
        }

        float proximity = ComputeProximity(
            chefController.IsRealReturnCountdownActive,
            chefController.RealReturnCountdownRemaining,
            chefController.RealReturnCountdownDuration);

        audioSource.volume = Mathf.Lerp(minVolume, maxVolume, proximity);
        audioSource.pitch = Mathf.Lerp(minPitch, maxPitch, proximity);

        if (proximity <= 0f && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        else if (proximity > 0f && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    /// <summary>
    /// 0..1 proximity to an actual return: 0 whenever no real countdown is active (idle, or a
    /// fake-out in progress — this is the "real returns only" guarantee), ramping to 1 exactly
    /// as the countdown reaches zero. Pure/static so it's unit-testable with no GameObject.
    /// </summary>
    public static float ComputeProximity(bool isRealReturnCountdownActive, float remaining, float duration)
    {
        if (!isRealReturnCountdownActive || duration <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(1f - remaining / duration);
    }
}
