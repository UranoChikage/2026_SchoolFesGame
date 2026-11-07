using UnityEngine;
[RequireComponent(typeof(ParticleSystem))]
public class SizeoverLifetimeChanger : MonoBehaviour
{
    [SerializeField] AnimationCurve curve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.5f, 1f),
        new Keyframe(1f, 0.5f)
    );
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    [ContextMenu("Change SizeoverLifetime")]
    public void Change()
    {
        var ps = GetComponent<ParticleSystem>();

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;

        sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }
}