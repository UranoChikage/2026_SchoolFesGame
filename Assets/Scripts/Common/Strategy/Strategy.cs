using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// ストラテジーパターンの基底クラス
    /// </summary>
    public abstract class Strategy<T> : MonoBehaviour, IStrategy<T>
    {
        public abstract T Key { get; }

        protected virtual void Awake() { }
        protected virtual void Start() { }
        protected virtual void Update() { }
        public abstract void Execute(GameObject owner);
    }
}
