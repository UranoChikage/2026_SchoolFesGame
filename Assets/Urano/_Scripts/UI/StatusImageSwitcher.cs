using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 自船の被弾とGAMEOVERで、用意したImage(のGameObject)のSetActiveを切り替える。
    /// 被弾：ダメージを受けるたびに表示し、一定時間後に消す（連続で受けたら延びる）。バリア中は出ない。
    /// GAMEOVER：自船が沈んで終了した時に表示。時間切れ(生き残り)では出さない。
    /// 自船は BoatController から自動で取得する。
    /// </summary>
    public class StatusImageSwitcher : MonoBehaviour
    {
        [Header("切り替える対象（Image自体のGameObject）")]
        [SerializeField] GameObject damageImage;
        [SerializeField] GameObject gameOverImage;


        ShipHealth health;
        GameFlow flow;
        bool gameOver;

        void Awake()
        {
            BoatController boat = FindFirstObjectByType<BoatController>();
            if (boat != null) health = boat.GetComponent<ShipHealth>();
            Set(damageImage, false);
            Set(gameOverImage, false);
        }

        void OnEnable()
        {
            if (health != null) health.Damaged += OnDamaged;
        }

        // GameFlow.Current は Awake で決まるので、購読は Start で行う
        void Start()
        {
            flow = GameFlow.Current;
            if (flow != null)
            {
                flow.GameStarted += ResetImages;
                flow.GameEnded += OnGameEnded;
            }
        }

        void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        void OnDestroy()
        {
            if (flow == null) return;
            flow.GameStarted -= ResetImages;
            flow.GameEnded -= OnGameEnded;
        }


        void OnDamaged(DamageInfo info)
        {
            if (gameOver) return;
            Set(damageImage, true);
        }

        void OnGameEnded(bool survived)
        {
            Set(damageImage, false);
            gameOver = !survived;
            Set(gameOverImage, gameOver);
        }

        void ResetImages()
        {
            gameOver = false;
            Set(damageImage, false);
            Set(gameOverImage, false);
        }

        static void Set(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(!false);
                go.SetActive(active);
            }
        }
    }
}
