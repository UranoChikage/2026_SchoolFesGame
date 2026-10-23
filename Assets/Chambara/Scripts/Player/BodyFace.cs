using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// 状態に合わせて顔のUVをずらし、表情テクスチャ内の表情を切り替える
    /// キャラクターのモデル側に付ける（表情テクスチャを持たないキャラには付けない）
    /// </summary>
    public class BodyFace : MonoBehaviour
    {
        public enum Face
        {
            Normal,//ノーマル
            Troubled,//焦り
            Angry,//怒り顔
            Happy,//喜び
            Sad,//悲しみ
            Serious //真剣
        }

        [SerializeField, Tooltip("顔のある頭。空なら子から headName で探す")] Renderer head;
        [SerializeField] string headName = "Head";

        [Header("表情テクスチャ内で通常顔からずらすUV（Face の順）")]
        [SerializeField] Vector2[] faceOffsets =
        {
            new Vector2(0f,     0f),       // Normal   左上
            new Vector2(0.299f, 0f),       // Troubled 右上
            new Vector2(0f,     -0.297f),  // Angry    中左
            new Vector2(0.302f, -0.296f),  // Happy    中右
            new Vector2(0f,     -0.604f),  // Sad      左下
            new Vector2(0.302f, -0.607f),  // Serious  右下
        };

        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

        PlayerBehaviour player;
        MaterialPropertyBlock block;
        Face face = (Face)(-1);

        public Face CurrentFace => face;

        private void Awake()
        {
            player = GetComponentInParent<PlayerBehaviour>();
            block = new MaterialPropertyBlock();
            if (head == null)
            {
                foreach (var t in GetComponentsInChildren<Transform>())
                {
                    if (t.name == headName && t.TryGetComponent(out head)) break;
                }
            }
        }

        private void LateUpdate()
        {
            if (player != null) SetFace(SelectFace());
        }

        public void SetFace(Face f)
        {
            if (f == face || head == null) return;
            face = f;
            Vector2 o = (int)f < faceOffsets.Length ? faceOffsets[(int)f] : Vector2.zero;
            head.GetPropertyBlock(block);
            block.SetVector(BaseMapST, new Vector4(1f, 1f, o.x, o.y));
            head.SetPropertyBlock(block);
        }

        Face SelectFace()
        {
            if (player.Lost) return Face.Sad;
            if (player.Won) return Face.Happy;
            return (player.Data != null ? player.Data.state : PlayerState.Idle) switch
            {
                PlayerState.Stun   => Face.Troubled,
                PlayerState.Attack => Face.Angry,
                PlayerState.Guard  => Face.Serious,
                _                  => Face.Normal,
            };
        }
    }
}
