#if UNITY_SWITCH
using System.Collections.Generic;
using UnityEngine;
using nn.hid;

namespace Common
{
    /// <summary>
    /// Npad の接続状態を毎フレーム見て、SwitchInput と同じ index で NnVibrate を SwitchVibration に登録する。
    /// 接続・切断・持ち方の変更（携帯モード⇔Joy-Con分離など）に追従する。
    /// </summary>
    sealed class NnVibrateInstaller : MonoBehaviour
    {
        //SwitchInput の index と同じ並び順にすること（入力バックエンドと揃える）
        static readonly NpadId[] Order =
        {
            NpadId.Handheld,
            NpadId.No1, NpadId.No2, NpadId.No3, NpadId.No4,
            NpadId.No5, NpadId.No6, NpadId.No7, NpadId.No8,
        };

        struct Slot
        {
            public NpadId Id;
            public NpadStyle Style;
            public NnVibrate Vib;
        }
        readonly List<Slot> slots = new List<Slot>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var go = new GameObject("NnVibrateInstaller") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<NnVibrateInstaller>();
        }

        // Npad.Initialize より前に触らないよう、BeforeSceneLoad では何もせず Update で見る
        void Update()
        {
            int index = 0;
            foreach (var id in Order)
            {
                NpadStyle style = Npad.GetStyleSet(id);
                if (style == NpadStyle.None) continue;

                if (index == slots.Count)
                    slots.Add(new Slot { Style = NpadStyle.None, Vib = new NnVibrate() });

                var s = slots[index];
                if (s.Id != id || s.Style != style)
                {
                    // 別のコントローラー／別の持ち方に変わった：止めてからハンドルを取り直す
                    s.Vib.Stop();
                    s.Id = id;
                    s.Style = style;
                    s.Vib.GetVibrationDevice((int)id, (int)style);
                    slots[index] = s;
                    SwitchVibration.Register(index, s.Vib);
                }
                index++;
            }

            // 減った分は解除
            for (int i = slots.Count - 1; i >= index; i--)
            {
                slots[i].Vib.Stop();
                SwitchVibration.Register(i, null);
                slots.RemoveAt(i);
            }
        }
    }
}
#endif