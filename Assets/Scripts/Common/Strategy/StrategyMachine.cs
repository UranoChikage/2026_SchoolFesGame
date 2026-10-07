using System.Collections.Generic;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// ストラテジーをKeyで管理するクラス
    /// </summary>
    public class StrategyMachine<T>
    {
        private IStrategy<T>[] strategies;
        public StrategyMachine(IStrategy<T>[] strategies)
        {
            this.strategies = strategies;
        }
        public void ExecuteStrategy(T key, GameObject owner)
        {
            IStrategy<T> strategy = GetStrategy(key);
            strategy?.Execute(owner);
        }
        private IStrategy<T> GetStrategy(T key)
        {
            foreach (var strategy in strategies)
            {
                if (EqualityComparer<T>.Default.Equals(strategy.Key, key))
                {
                    return strategy;
                }
            }
            return null;
        }
    }
}
