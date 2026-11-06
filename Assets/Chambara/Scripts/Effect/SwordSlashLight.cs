using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// 振っている間だけ刃の位置に Light を出して、斬撃の光を Player や床に当てる
    /// 振り終わったら fadeTime かけて消す
    /// Point Light と同じ GameObject に付ける（Player の子ならどこでもよい）
    /// </summary>
    [RequireComponent(typeof(Light))]
    [DefaultExecutionOrder(100)]   // Sword の LateUpdate で剣を動かした後に位置を合わせる
    public class SwordSlashLight : MonoBehaviour
    {
        [SerializeField] Sword sword;
        [SerializeField] float intensity = 4f;              // 振っている間の明るさ
        [SerializeField, Range(0f, 1f)] float along = 0.7f;   // 刃のどこに置くか（0:根元 1:刃先）
        [SerializeField, Min(0.01f)] float fadeTime = 0.2f;   // 振り終わってから消えるまで

        Light slashLight;
        float power;   // 0〜1

        private void Awake()
        {
            if (sword == null) sword = GetComponentInParent<Sword>();
            slashLight = GetComponent<Light>();
            slashLight.enabled = false;
        }

        private void LateUpdate()
        {
            if (sword.IsSwinging) power = 1f;
            else power = Mathf.MoveTowards(power, 0f, Time.deltaTime / fadeTime);

            slashLight.enabled = power > 0f;
            if (!slashLight.enabled) return;

            sword.GetBlade(out var root, out var tip);
            transform.position = Vector3.Lerp(root, tip, along);
            slashLight.intensity = intensity * power * power;   // 消えるときは早めに暗くする
        }
    }
}
