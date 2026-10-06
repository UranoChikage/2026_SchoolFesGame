using System;
using System.Collections.Generic;

namespace Chambara
{
    //チャンバラ用
    public class StateMachine<T>
    {
        public Action<T> OnStateStarted;
        public Action<T> OnStateStopped;

        public StateMachine() { }

        State<T> currentState;

        Dictionary<State<T>, T> states = new Dictionary<State<T>, T>();

        public void Transition(T stateType)
        {
            // 状態遷移の処理

            if (currentState != null)
            {
                currentState.Exit();
                OnStateStopped?.Invoke(stateType);
            }

            // 新しい状態に遷移
            //TODO

        }
    }
}
