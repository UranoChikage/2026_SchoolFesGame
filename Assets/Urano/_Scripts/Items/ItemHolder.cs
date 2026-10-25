using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 砲手1人ぶんの所持アイテム。CannonStation が自動で付ける（設定不要）。
    /// 1人1つまで。所持中は新しく取得できない。使うと所持が空になり、効果が終わると通常の大砲に戻る。
    /// </summary>
    public class ItemHolder : MonoBehaviour
    {
        const int HitBuffer = 64;
        const float HitHeight = 20f; // 判定の上下の広がり(m)。砲弾の爆発と同じく縦長に取る
        const float BeamDamage = 99999f; // 一撃で沈める

        static readonly Collider[] hits = new Collider[HitBuffer];
        static readonly RaycastHit[] rayHits = new RaycastHit[16];

        CannonShooter shooter;
        ItemAsset held;
        ItemAsset beam;
        float beamEnd;
        bool cameraEffectOn;

        // ビームの見た目（ItemAsset のPrefabがあればそれ、無ければ仮の見た目）
        ItemAsset visualFor;
        GameObject beamRoot;
        LineRenderer beamLine;
        bool beamIsFallback;
        // Prefabが LineRenderer ではなくメッシュのモデルのとき：最も長い軸をビームの長さ方向に合わせて伸縮させる
        bool beamIsModel;
        int modelAxis;
        float modelLength;
        float modelThickness;
        Vector3 modelStart; // 長さ方向の端（根元）の位置。Prefabのルート基準・倍率1のとき
        GameObject areaRoot; // 判定範囲を示す水面の円
        float areaScaleY;

        // ビームの伸び縮み。砲口からの距離(m)で持つ。伸びきるまで判定なし、使用時間が終わると根元から縮む
        float headDist;
        float tailDist;
        bool beamExtended;
        Vector3 lastAim;
        bool hasLastAim;

        // 演出
        bool muzzleVfxPlayed;
        GameObject hitVfxInstance; // 終点に追従させている当たり演出（1つだけ）
        VfxAsset hitVfxCurrent;

        /// <summary>誰かがアイテムを取得した（獲得通知UIが使う）</summary>
        /// <summary>第2引数は取得した砲手の色</summary>
        public static event System.Action<ItemAsset, Color> Acquired;

        public ItemAsset Held => held;
        /// <summary>ビーム使用中。この間この砲手は通常の大砲を撃てない</summary>
        public bool IsBeamActive => beam != null && Time.time < beamEnd;

        public static ItemHolder For(CannonShooter shooter)
        {
            var h = shooter.GetComponent<ItemHolder>();
            if (h == null) h = shooter.gameObject.AddComponent<ItemHolder>();
            h.shooter = shooter;
            return h;
        }

        public bool TryAcquire(ItemAsset item)
        {
            if (item == null || held != null) return false;
            held = item;
            Debug.Log($"[Item] {name} が {item.DisplayName} を取得", this);
            var station = GetComponent<CannonStation>();
            Acquired?.Invoke(item, station != null ? station.PlayerColor : Color.white);
            return true;
        }

        /// <summary>所持アイテムを使う。使えたらtrue（発射ボタンの代わりに呼ぶ）</summary>
        public bool TryUse()
        {
            if (held == null || IsBeamActive) return false;

            ItemAsset item = held;
            held = null;
            Debug.Log($"[Item] {name} が {item.DisplayName} を使用", this);

            if (item.Kind == ItemKind.Barrier)
            {
                ShipBarrier barrier = ShipBarrier.ForPlayer();
                if (barrier != null) barrier.Activate(item);
            }
            else
            {
                beam = item;
                beamEnd = Time.time + item.Duration;
                headDist = 0f;
                tailDist = 0f;
                beamExtended = false;
                muzzleVfxPlayed = false;
                StartCameraEffect();
            }
            return true;
        }

        /// <summary>毎フレーム、カーソルが指す位置を渡す。ビームが伸びきった後、その位置の船を沈める</summary>
        public void Tick(Vector3 aimPoint, bool hasAim)
        {
            if (cameraEffectOn && !IsBeamActive) StopCameraEffect();
            if (beam == null) return;

            if (hasAim)
            {
                lastAim = aimPoint;
                hasLastAim = true;
            }
            Vector3 from = shooter.MuzzlePosition;
            Vector3 to = lastAim - from;
            float length = to.magnitude;
            if (!hasLastAim || length < 0.01f)
            {
                HideVisuals();
                return;
            }
            Vector3 dir = to / length;
            Transform owner = shooter.Owner;

            // 遮蔽（岩礁など）があれば、そこでビームを止める
            bool blocked = FindBlocker(from, dir, length, owner, out RaycastHit blockHit, out HitSurface blockSurface);
            if (blocked) length = blockHit.distance;

            bool firing = IsBeamActive;
            if (firing && !beamExtended)
            {
                headDist += beam.BeamExtendSpeed * Time.deltaTime;
                if (headDist >= length) beamExtended = true;
            }
            float head = beamExtended ? length : Mathf.Min(headDist, length);

            if (!firing)
            {
                // 使用時間が終わったら、根元から着弾点へ縮める。縮む間は判定なし
                tailDist += beam.BeamRetractSpeed * Time.deltaTime;
                if (tailDist >= head)
                {
                    beam = null;
                    HideVisuals();
                    return;
                }
            }

            if (firing && !muzzleVfxPlayed)
            {
                muzzleVfxPlayed = true;
                if (beam.BeamMuzzleVfx != null) beam.BeamMuzzleVfx.Play(from, Quaternion.LookRotation(dir), Vector3.one);
            }

            bool hitting = firing && beamExtended;
            Vector3 end = blocked ? blockHit.point : lastAim;
            ShowBeam(from + dir * tailDist, from + dir * head);
            ShowArea(lastAim, hitting && !blocked);

            if (!hitting || !hasAim)
            {
                ReleaseHitVfx();
                return;
            }

            // 終点の当たり演出：遮蔽に当たっていればその位置、着弾点が水面なら海。それ以外は出さない
            if (blocked)
            {
                FollowHitVfx(blockSurface, blockHit.point, blockHit.normal);
            }
            else
            {
                BoatBuoyancy water = PlayerTarget.Current != null ? PlayerTarget.Current.Water : null;
                if (water != null && Mathf.Abs(aimPoint.y - water.waterY) < 0.5f) FollowHitVfx(HitSurface.Sea, aimPoint, Vector3.up);
                else ReleaseHitVfx();
            }

            // 通り道：砲口から終点までの線上にいる船も全て攻撃判定
            int count = Physics.OverlapCapsuleNonAlloc(from, end, beam.BeamWidth, hits, shooter.HitMask, QueryTriggerInteraction.Ignore);
            Damage(count, owner, aimPoint);

            // 遮蔽で止まっていなければ着弾点の判定。水面の高さのずれで外れないよう、縦長のカプセルで判定する
            if (!blocked)
            {
                Vector3 up = Vector3.up * HitHeight;
                count = Physics.OverlapCapsuleNonAlloc(aimPoint + up, aimPoint - up, beam.BeamRadius, hits, shooter.HitMask, QueryTriggerInteraction.Ignore);
                Damage(count, owner, aimPoint);
            }
        }

        // 砲口から着弾点までの線上で、いちばん手前の「ビームを遮るもの」を探す
        bool FindBlocker(Vector3 from, Vector3 dir, float length, Transform owner, out RaycastHit result, out HitSurface surface)
        {
            result = default;
            surface = null;
            int count = Physics.RaycastNonAlloc(from, dir, rayHits, length, shooter.HitMask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = rayHits[i];
                if (h.distance >= nearest) continue;
                if (owner != null && h.collider.transform.IsChildOf(owner)) continue;
                var s = h.collider.GetComponentInParent<HitSurface>();
                if (s == null || !s.BlocksBeam) continue;
                nearest = h.distance;
                result = h;
                surface = s;
            }
            return surface != null;
        }

        void Damage(int count, Transform owner, Vector3 point)
        {
            for (int i = 0; i < count; i++)
            {
                if (owner != null && hits[i].transform.IsChildOf(owner)) continue;
                // ビームに触れた宝箱は獲得（アイテムはビームでは取れない）
                var chest = hits[i].GetComponentInParent<TreasurePickup>();
                if (chest != null) chest.TryCollect();
                var health = hits[i].GetComponentInParent<ShipHealth>();
                if (health != null && !health.IsDead) health.TakeDamage(BeamDamage, point);
            }
        }

        // 終点の当たり演出を1つだけ出して、終点に追従させる。素材(HitSurface→SurfaceAsset)の演出を優先し、無ければアイテム既定の演出。
        // 素材が変わって演出が別物になったら入れ替える
        void FollowHitVfx(HitSurface surface, Vector3 point, Vector3 normal)
        {
            SurfaceAsset sa = surface != null ? surface.Surface : null;
            bool hasSurfaceVfx = sa != null && sa.BeamHitVfx != null;
            VfxAsset vfx = hasSurfaceVfx ? sa.BeamHitVfx : beam.BeamHitVfx;
            if (vfx == null)
            {
                ReleaseHitVfx();
                return;
            }

            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal);
            if (hitVfxInstance == null || hitVfxCurrent != vfx)
            {
                ReleaseHitVfx();
                hitVfxInstance = vfx.Hold(point, rotation, Vector3.one * (hasSurfaceVfx ? sa.VfxScale : 1f));
                hitVfxCurrent = vfx;
                return;
            }
            hitVfxInstance.transform.SetPositionAndRotation(point, rotation);
        }

        void ReleaseHitVfx()
        {
            if (hitVfxInstance != null) PoolManager.Release(hitVfxInstance);
            hitVfxInstance = null;
            hitVfxCurrent = null;
        }

        void StartCameraEffect()
        {
            BoatFollowCamera cam = BoatFollowCamera.Current;
            if (cam == null || cameraEffectOn) return;
            cam.BeginEffect(beam.CameraZoomOut, beam.CameraShake);
            cameraEffectOn = true;
        }

        void StopCameraEffect()
        {
            cameraEffectOn = false;
            if (BoatFollowCamera.Current != null) BoatFollowCamera.Current.EndEffect();
        }

        void OnDisable()
        {
            HideVisuals();
            if (cameraEffectOn) StopCameraEffect();
        }

        void OnDestroy() => DestroyVisuals();

        void HideVisuals()
        {
            ReleaseHitVfx();
            if (beamRoot != null) beamRoot.SetActive(false);
            if (areaRoot != null) areaRoot.SetActive(false);
        }

        void DestroyVisuals()
        {
            if (beamRoot != null) Destroy(beamRoot);
            if (areaRoot != null) Destroy(areaRoot);
            beamRoot = null;
            beamLine = null;
            areaRoot = null;
        }

        // 見た目はアイテムが変わったら作り直す
        void EnsureVisuals()
        {
            if (visualFor == beam && beamRoot != null) return;
            DestroyVisuals();
            visualFor = beam;

            // ビーム本体：Prefab（LineRenderer入り、またはメッシュのモデル）。無い／どちらでもないなら仮の見た目
            beamIsModel = false;
            if (beam.BeamPrefab != null)
            {
                beamRoot = Instantiate(beam.BeamPrefab, Vector3.zero, Quaternion.identity);
                beamLine = beamRoot.GetComponentInChildren<LineRenderer>(true);
                if (beamLine == null)
                {
                    beamIsModel = TryMeasureModel(beamRoot);
                    if (!beamIsModel)
                    {
                        Debug.LogWarning($"[Item] {beam.name} の Beam Prefab に LineRenderer もメッシュ（MeshRenderer）もありません。仮の見た目を使います", beam);
                        Destroy(beamRoot);
                        beamRoot = null;
                    }
                }
            }
            beamIsFallback = beamRoot == null;
            if (beamIsFallback)
            {
                beamRoot = new GameObject("BeamVisual");
                beamLine = beamRoot.AddComponent<LineRenderer>();
                beamLine.numCapVertices = 4;
                beamLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beamLine.receiveShadows = false;
                beamLine.material = new Material(Shader.Find("Sprites/Default")) { renderQueue = 3100 }; // 海(3000, 不透明のように上書き)より後に描く
            }
            if (beamLine != null)
            {
                beamLine.positionCount = 2;
                beamLine.useWorldSpace = true;
            }

            // 判定範囲の円：Prefab（直径1mの薄い円盤）。無ければ仮の見た目
            if (beam.BeamAreaPrefab != null)
            {
                areaRoot = Instantiate(beam.BeamAreaPrefab);
                areaScaleY = beam.BeamAreaPrefab.transform.localScale.y;
            }
            else
            {
                areaRoot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                areaRoot.name = "BeamAreaVisual";
                Destroy(areaRoot.GetComponent<Collider>());
                Color ac = beam.Color;
                ac.a = 0.25f;
                areaRoot.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default")) { renderQueue = 3100, color = ac };
                areaScaleY = 0.02f;
            }
            areaRoot.SetActive(false);
        }

        void ShowBeam(Vector3 tail, Vector3 head)
        {
            EnsureVisuals();

            float width = beam.BeamWidth * 2f; // 通り道の判定と同じ太さ
            if (beamIsModel)
            {
                ShowBeamModel(tail, head, width);
                return;
            }
            if (beamIsFallback)
            {
                Color c = beam.Color;
                c.a = 0.5f;
                beamLine.startColor = c;
                beamLine.endColor = c;
                beamLine.startWidth = width;
                beamLine.endWidth = width;
            }
            else
            {
                beamLine.widthMultiplier = width; // Prefab側の太さカーブに倍率として掛かる
            }
            beamLine.SetPosition(0, tail);
            beamLine.SetPosition(1, head);
            beamRoot.SetActive(true);
        }

        // モデルの大きさを測る。メッシュ（MeshRenderer／SkinnedMeshRenderer）が無ければfalse。最も長い軸を長さ方向とみなす
        bool TryMeasureModel(GameObject root)
        {
            root.transform.localScale = Vector3.one;
            bool found = false;
            Bounds bounds = default;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (!found) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                found = true;
            }
            if (!found) return false;

            Vector3 size = bounds.size;
            modelAxis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
            modelLength = Mathf.Max(0.0001f, size[modelAxis]);
            float other1 = size[(modelAxis + 1) % 3];
            float other2 = size[(modelAxis + 2) % 3];
            modelThickness = Mathf.Max(0.0001f, Mathf.Max(other1, other2));
            modelStart = bounds.center;
            modelStart[modelAxis] = bounds.min[modelAxis];
            return true;
        }

        // モデルを tail〜head に合わせて伸縮させる。長さ方向は length/モデルの長さ、太さは width/モデルの太さ
        void ShowBeamModel(Vector3 tail, Vector3 head, float width)
        {
            Vector3 d = head - tail;
            float length = d.magnitude;
            if (length < 0.01f)
            {
                beamRoot.SetActive(false);
                return;
            }
            Vector3 dir = d / length;

            Vector3 axis = modelAxis == 0 ? Vector3.right : (modelAxis == 1 ? Vector3.up : Vector3.forward);
            Quaternion rotation = Quaternion.FromToRotation(axis, dir);
            Vector3 scale = Vector3.one * (width / modelThickness);
            scale[modelAxis] = length / modelLength;

            Transform t = beamRoot.transform;
            t.localScale = scale;
            t.rotation = rotation;
            t.position = tail - rotation * Vector3.Scale(modelStart, scale); // モデルの根元の端を tail に合わせる
            beamRoot.SetActive(true);
        }

        // 着弾点の判定範囲の円。判定が出ている間だけ見せる
        void ShowArea(Vector3 point, bool show)
        {
            if (areaRoot == null) return;
            areaRoot.SetActive(show);
            if (!show) return;

            float diameter = beam.BeamRadius * 2f;
            areaRoot.transform.SetPositionAndRotation(point + Vector3.up * 0.05f, Quaternion.identity);
            areaRoot.transform.localScale = new Vector3(diameter, areaScaleY, diameter);
        }
    }
}
