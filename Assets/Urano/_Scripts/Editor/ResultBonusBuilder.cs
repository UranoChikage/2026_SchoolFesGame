using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Pirates.EditorTools
{
    /// <summary>
    /// 開いているシーンの Canvas の下に「BONUS +数字 !!」の演出UIを作る。
    /// Sprite は Assets/_Sprits の Sprite_Result_* を自動で割り当てる。作った後は自由に位置・大きさを直せる。
    /// </summary>
    public static class ResultBonusBuilder
    {
        const string SpriteDir = "Assets/_Sprits/";

        [MenuItem("Urano/Result/Create Bonus UI")]
        static void Create()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Bonus UI", "シーンに Canvas がありません。", "OK");
                return;
            }

            var rowGo = new GameObject("BonusRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            Undo.RegisterCreatedObjectUndo(rowGo, "Create Bonus UI");
            rowGo.transform.SetParent(canvas.transform, false);
            var row = (RectTransform)rowGo.transform;
            row.anchoredPosition = Vector2.zero;
            var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            var fitter = rowGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // BONUS画像はレイアウトから外して、位置を自由に動かせるようにする（「+ 数字」だけが並びの対象）
            Image bonusLabel = AddImage(row, "Bonus", "Sprite_Result_Bonus.png", new Vector2(512f, 128f));
            bonusLabel.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            ((RectTransform)bonusLabel.transform).anchoredPosition = new Vector2(-420f, 0f);
            AddImage(row, "Plus", "Sprite_Result_12_Plus.png", new Vector2(128f, 128f));

            var numberGo = new GameObject("Number", typeof(RectTransform), typeof(SpriteNumber));
            numberGo.transform.SetParent(row, false);
            var number = numberGo.GetComponent<SpriteNumber>();
            var sn = new SerializedObject(number);
            var digits = sn.FindProperty("digits");
            digits.arraySize = 10;
            for (int i = 0; i < 10; i++)
                digits.GetArrayElementAtIndex(i).objectReferenceValue = LoadSprite($"Sprite_Result_{i}.png");
            sn.ApplyModifiedPropertiesWithoutUndo();

            Image excl = AddImage(row, "Exclamation", "Sprite_Result_11_Exclamation.png", new Vector2(256f, 128f));
            excl.preserveAspect = true;

            rowGo.name = "BonusRow";
            var seq = rowGo.AddComponent<ResultBonusSequence>();
            var so = new SerializedObject(seq);
            so.FindProperty("number").objectReferenceValue = number;
            so.FindProperty("exclamation").objectReferenceValue = excl;
            so.FindProperty("row").objectReferenceValue = row;
            so.ApplyModifiedPropertiesWithoutUndo();

            BuildResultRoot(canvas, seq);

            Selection.activeGameObject = rowGo;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rowGo.scene);
        }

        // 懸賞金・RANK・タイトルボタンと、全体を進行する ResultSequence を作る
        static void BuildResultRoot(Canvas canvas, ResultBonusSequence bonus)
        {
            var bountyGo = new GameObject("BountyRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            Undo.RegisterCreatedObjectUndo(bountyGo, "Create Bounty UI");
            bountyGo.transform.SetParent(canvas.transform, false);
            var bountyRt = (RectTransform)bountyGo.transform;
            bountyRt.anchoredPosition = new Vector2(0f, 250f);
            var bl = bountyGo.GetComponent<HorizontalLayoutGroup>();
            bl.childControlWidth = bl.childControlHeight = false;
            bl.childForceExpandWidth = bl.childForceExpandHeight = false;
            bl.childAlignment = TextAnchor.MiddleCenter;
            var bf = bountyGo.GetComponent<ContentSizeFitter>();
            bf.horizontalFit = bf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var numGo = new GameObject("BountyNumber", typeof(RectTransform), typeof(SpriteNumber));
            numGo.transform.SetParent(bountyRt, false);
            var num = numGo.GetComponent<SpriteNumber>();
            var sn = new SerializedObject(num);
            var digits = sn.FindProperty("digits");
            digits.arraySize = 10;
            for (int i = 0; i < 10; i++)
                digits.GetArrayElementAtIndex(i).objectReferenceValue = LoadSprite($"Sprite_Result_{i}.png");
            sn.ApplyModifiedPropertiesWithoutUndo();

            Image rank = AddImage(canvas.transform, "Rank", "Sprite_SRank.png", new Vector2(512f, 512f));
            ((RectTransform)rank.transform).anchoredPosition = new Vector2(0f, -250f);

            var btnGo = new GameObject("TitleButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(canvas.transform, false);
            var btnRt = (RectTransform)btnGo.transform;
            btnRt.sizeDelta = new Vector2(480f, 120f);
            btnRt.anchoredPosition = new Vector2(0f, -450f);

            var root = new GameObject("ResultSequence", typeof(RectTransform), typeof(ResultSequence));
            root.transform.SetParent(canvas.transform, false);
            var seq = new SerializedObject(root.GetComponent<ResultSequence>());
            seq.FindProperty("bountyNumber").objectReferenceValue = num;
            seq.FindProperty("bountyRoot").objectReferenceValue = bountyRt;
            seq.FindProperty("bonus").objectReferenceValue = bonus;
            seq.FindProperty("rankImage").objectReferenceValue = rank;
            seq.FindProperty("rankB").objectReferenceValue = LoadSprite("Sprite_BRank.png");
            seq.FindProperty("rankA").objectReferenceValue = LoadSprite("Sprite_ARank.png");
            seq.FindProperty("rankS").objectReferenceValue = LoadSprite("Sprite_SRank.png");
            seq.FindProperty("titleButton").objectReferenceValue = btnGo.GetComponent<Button>();
            seq.ApplyModifiedPropertiesWithoutUndo();
        }

        static Image AddImage(Transform parent, string name, string file, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = LoadSprite(file);
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        static Sprite LoadSprite(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + file);
    }
}
