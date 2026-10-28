// 一条气流线：把"一条随机曲线"这件事拆成 控制点 → Catmull-Rom → 等弧长重采样，
// 最后喂给 LineRenderer。
//
// 为什么必须做等弧长重采样：LineRenderer 在 Stretch 模式下，uv.x 是按【点索引】
// 均匀分配的，不是按弧长。而 Catmull-Rom 采样出来的点疏密不均（弯道处更密），
// 直接用会让 shader 里的流动亮带在弯道被拉长、直道被压缩，看起来一抖一抖的。
// 重采样成等距点之后，uv.x 才等价于"沿线的进度"，绘制动画和流动才均匀。
//
// 为什么曲线在水平面里弯：参考图是俯视角看到的风线。气流在空中，从上方看是弧线、
// 从正面看是一条近似直线 —— 这正好是吉卜力那种"一笔带过"的观感。再加竖向的轻微
// 起伏（riseAmount）避免完全扁平。
//
// 卷环（loop）：参考图右上角那一笔末端打了个小圈。用圆弧直接生成控制点，
// 圆半径按线长比例走，太大就不像风、太小会自交穿帮。
using System.Collections.Generic;
using UnityEngine;

namespace WindmillFarm.Visual
{
    /// <summary>
    /// 播放一条气流所需的全部形状参数。由 WindGustField 随机化后传进来，
    /// 让 Ribbon 只负责"按参数画线"，不掺和"什么时候画、画在哪"。
    /// </summary>
    public struct WindGustParams
    {
        public float Length;          // 曲线总弧长（米）
        public float Width;           // 最大宽度（米）
        public float BendAmount;      // 横向弯曲幅度，相对长度
        public float BendWaves;       // 横向弯几个弯
        public float RiseAmount;      // 竖向起伏幅度（米）
        public float LoopAngle;       // 末端卷环角度（度），0 = 不卷
        public float LoopSide;        // 卷环朝左还是朝右，±1
        public float LoopRadiusScale; // 卷环半径，相对长度
        public float CoreSoftness;    // 笔触芯的软硬
        public float WobbleFreq;      // 笔触粗细抖动的频率
        public float Opacity;         // 基础不透明度
        public float LifeTime;        // 整条线的存活时间（秒）
        public float FlowSpeed;       // 流动亮带速度
        public float FlowTiling;      // 沿线有几个流动周期
        public float Phase;           // 相位偏移，防止所有线同步呼吸
        public float YawDeg;          // 主方向（水平面内，度）
    }

    [ExecuteAlways]
    [RequireComponent(typeof(LineRenderer))]
    [DisallowMultipleComponent]
    public sealed class WindGustRibbon : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        static readonly int ProgressId = Shader.PropertyToID("_DrawProgress");
        static readonly int DrawSoftnessId = Shader.PropertyToID("_DrawSoftness");
        static readonly int PhaseId = Shader.PropertyToID("_Phase");
        static readonly int FlowTilingId = Shader.PropertyToID("_FlowTiling");
        static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
        static readonly int CoreSoftnessId = Shader.PropertyToID("_CoreSoftness");
        static readonly int WobbleFreqId = Shader.PropertyToID("_WobbleFreq");
        static readonly int WobbleAmpId = Shader.PropertyToID("_WobbleAmp");
        static readonly int HeadFadeId = Shader.PropertyToID("_HeadFade");
        static readonly int TailFadeId = Shader.PropertyToID("_TailFade");
        static readonly int FlowContrastId = Shader.PropertyToID("_FlowContrast");

        [Header("采样精度")]
        [Tooltip("最终喂给 LineRenderer 的点数。越大越顺滑，也越吃顶点")]
        [Range(8, 160)]
        public int segments = 56;

        [Tooltip("Catmull-Rom 每段插值多少份再拿去等弧长重采样")]
        [Range(4, 24)]
        public int subdivPerSpan = 12;

        [Header("编辑器预览")]
        [Tooltip("不运行时用这个种子生成一条样例曲线，方便调参数")]
        public int previewSeed = 20261028;

        [Tooltip("不运行时也显示线条（关掉则只在运行时可见）")]
        public bool previewInEditMode = true;

        LineRenderer line;
        MaterialPropertyBlock block;

        // 当前这条线的运行态
        WindGustParams active;
        System.Random activeRng;
        float age;
        bool playing;
        int lastSignature;

        void Awake()
        {
            Cache();
        }

        void OnEnable()
        {
            Cache();
            if (!Application.isPlaying && previewInEditMode)
            {
                PreviewOnce();
            }
        }

        void OnValidate()
        {
            if (Application.isPlaying) return;
            if (!previewInEditMode) return;

            // 只在参数真的变了才重建，否则 ExecuteAlways 下每次重编译都会写一次
            // LineRenderer，把场景持续标脏、一直弹保存。
            int sig = segments * 397 ^ subdivPerSpan * 31 ^ previewSeed;
            if (sig == lastSignature) return;
            lastSignature = sig;
            PreviewOnce();
        }

        void Cache()
        {
            if (line == null) line = GetComponent<LineRenderer>();
            if (block == null) block = new MaterialPropertyBlock();
            if (line == null) return;

            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        /// <summary>
        /// 开始播放一条新气流。origin 是本地坐标（一般传 Vector3.zero，
        /// 由 WindGustField 决定物体摆哪）。
        /// </summary>
        public void Play(in WindGustParams p, int seed)
        {
            if (line == null) Cache();
            if (line == null) return;

            active = p;
            activeRng = new System.Random(seed);
            age = 0f;
            playing = true;

            gameObject.SetActive(true);
            BuildCurve(activeRng, p);
            ApplyMaterial(p);

            // 起手就是画到 0，第一帧不可见，避免"啪"地一下整条出现
            SetProgress(0f);
            SetOpacity(0f);
        }

        /// <summary>停止并隐藏，回池用。</summary>
        public void Stop()
        {
            playing = false;

            // 池初始化不完整时（比如 Field.OnDisable 早于 Play）也不要炸
            if (line != null)
            {
                block.SetFloat(OpacityId, 0f);
                line.SetPropertyBlock(block);
            }

            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!playing) return;

            age += Time.deltaTime;
            float t = active.LifeTime > 0.01f ? age / active.LifeTime : 1f;

            // 前 45% 时间把线"画"出来，后 55% 边画边淡出 —— 两者重叠会让收尾更自然，
            // 不会出现"画完了再原地停一会儿才消失"的呆板感。
            const float drawPortion = 0.45f;
            float drawT = Mathf.Clamp01(t / drawPortion);
            float progress = Mathf.Lerp(0f, 1.12f, EaseOut(drawT));

            float fadeT = Mathf.Clamp01((t - drawPortion) / (1f - drawPortion));
            float opacity = active.Opacity * (1f - fadeT * fadeT);

            // 两个值一起下发：SetPropertyBlock 会触发渲染器属性重建，
            // 每帧调两次是白白的开销。
            block.SetFloat(ProgressId, progress);
            block.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            line.SetPropertyBlock(block);

            if (t >= 1f) Stop();
        }

        static float EaseOut(float x)
        {
            float inv = 1f - x;
            return 1f - inv * inv * inv;
        }

        // ───────────────────────── 曲线生成 ─────────────────────────

        void BuildCurve(System.Random rng, in WindGustParams p)
        {
            var cps = new List<Vector3>(10);

            float yaw = p.YawDeg * Mathf.Deg2Rad;
            var fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            var side = new Vector3(fwd.z, 0f, -fwd.x); // 水平面内垂直于主方向

            int baseCount = 3 + rng.Next(0, 3); // 3~5 个基础控制点
            float bendPhase = (float)rng.NextDouble() * Mathf.PI * 2f;
            float risePhase = (float)rng.NextDouble() * Mathf.PI * 2f;

            for (int i = 0; i < baseCount; i++)
            {
                float u = i / (float)(baseCount - 1);
                float along = u * p.Length;

                // 两端收在主轴线上（乘 sin(πu)），否则起笔和落笔会飘离主线
                float taper = Mathf.Sin(u * Mathf.PI);
                float lateral = Mathf.Sin(u * Mathf.PI * p.BendWaves + bendPhase) * p.BendAmount * p.Length * taper;
                float up = Mathf.Sin(u * Mathf.PI * Mathf.Max(1f, p.BendWaves) + risePhase) * p.RiseAmount * taper;

                cps.Add(fwd * along + side * lateral + Vector3.up * up);
            }

            if (p.LoopAngle > 1f)
            {
                AppendLoop(cps, p, Mathf.Sign(p.LoopSide == 0f ? 1f : p.LoopSide));
            }

            var pts = ResampleEven(cps, Mathf.Max(8, segments), Mathf.Max(4, subdivPerSpan));

            line.positionCount = pts.Count;
            line.SetPositions(pts.ToArray());
            line.widthCurve = BuildWidthCurve(rng, p);
            line.widthMultiplier = p.Width;
        }

        /// <summary>
        /// 在曲线末端接一段圆弧做卷环。圆心在末端的侧向，圆弧从末端点出发绕回去，
        /// 这样衔接处切线连续、不会出现折角。
        /// </summary>
        static void AppendLoop(List<Vector3> cps, in WindGustParams p, float sideSign)
        {
            Vector3 end = cps[cps.Count - 1];
            Vector3 prev = cps[Mathf.Max(0, cps.Count - 2)];
            Vector3 tangent = end - prev;
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 1e-6f) tangent = Vector3.forward;
            tangent.Normalize();

            // 水平面内垂直切线
            var normal = new Vector3(tangent.z, 0f, -tangent.x) * sideSign;

            float radius = Mathf.Max(0.02f, p.Length * Mathf.Clamp(p.LoopRadiusScale, 0.02f, 0.2f));
            Vector3 center = end + normal * radius;
            Vector3 inward = -normal; // 从圆心指向起点的方向

            float totalRad = p.LoopAngle * Mathf.Deg2Rad;
            int steps = Mathf.Clamp(Mathf.RoundToInt(p.LoopAngle / 22f), 4, 18);

            for (int i = 1; i <= steps; i++)
            {
                float th = totalRad * i / steps;
                cps.Add(center
                        + inward * (radius * Mathf.Cos(th))
                        + tangent * (radius * Mathf.Sin(th)));
            }
        }

        static AnimationCurve BuildWidthCurve(System.Random rng, in WindGustParams p)
        {
            // 起笔细一点、中段最粗、收笔成尖 —— 就是毛笔的提按
            float startW = 0.14f + (float)rng.NextDouble() * 0.22f;
            float peakT = 0.28f + (float)rng.NextDouble() * 0.42f;
            float endW = 0.02f + (float)rng.NextDouble() * 0.10f;

            var keys = new[]
            {
                new Keyframe(0f, startW, 0f, 0f),
                new Keyframe(peakT, 1f, 0f, 0f),
                new Keyframe(1f, endW, 0f, 0f)
            };

            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].inTangent = 0f;
                keys[i].outTangent = 0f;
            }

            return new AnimationCurve(keys);
        }

        /// <summary>
        /// Catmull-Rom 密集采样后，按弧长等距取点。
        /// 端点用重复首尾的方式构造 p0/p3，避免曲线在两端甩出去。
        /// </summary>
        static List<Vector3> ResampleEven(List<Vector3> cps, int count, int subdiv)
        {
            var dense = new List<Vector3>(cps.Count * subdiv + 2);

            for (int i = 0; i < cps.Count - 1; i++)
            {
                Vector3 p0 = cps[Mathf.Max(i - 1, 0)];
                Vector3 p1 = cps[i];
                Vector3 p2 = cps[i + 1];
                Vector3 p3 = cps[Mathf.Min(i + 2, cps.Count - 1)];

                for (int s = 0; s < subdiv; s++)
                {
                    dense.Add(CatmullRom(p0, p1, p2, p3, s / (float)subdiv));
                }
            }
            dense.Add(cps[cps.Count - 1]);

            // 累计弧长
            var cum = new float[dense.Count];
            for (int i = 1; i < dense.Count; i++)
            {
                cum[i] = cum[i - 1] + Vector3.Distance(dense[i - 1], dense[i]);
            }
            float total = cum[dense.Count - 1];

            var result = new List<Vector3>(count);
            if (total < 1e-4f)
            {
                for (int i = 0; i < count; i++) result.Add(dense[0]);
                return result;
            }

            result.Add(dense[0]);
            int cursor = 1;
            for (int i = 1; i < count - 1; i++)
            {
                float target = total * i / (count - 1);
                while (cursor < dense.Count - 1 && cum[cursor] < target) cursor++;

                float segLen = cum[cursor] - cum[cursor - 1];
                float f = segLen > 1e-6f ? (target - cum[cursor - 1]) / segLen : 0f;
                result.Add(Vector3.Lerp(dense[cursor - 1], dense[cursor], f));
            }
            result.Add(dense[dense.Count - 1]);

            return result;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1)
                         + (-p0 + p2) * t
                         + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                         + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // ───────────────────────── 材质参数 ─────────────────────────

        void ApplyMaterial(in WindGustParams p)
        {
            block.SetFloat(OpacityId, 0f);
            block.SetFloat(ProgressId, 0f);
            block.SetFloat(PhaseId, p.Phase);
            block.SetFloat(FlowTilingId, p.FlowTiling);
            block.SetFloat(FlowSpeedId, p.FlowSpeed);
            block.SetFloat(CoreSoftnessId, p.CoreSoftness);
            block.SetFloat(WobbleFreqId, p.WobbleFreq);
            block.SetFloat(FlowContrastId, 0.35f);
            block.SetFloat(WobbleAmpId, 0.35f);
            block.SetFloat(HeadFadeId, 0.05f);
            block.SetFloat(TailFadeId, 0.14f);
            block.SetFloat(DrawSoftnessId, 0.05f);
            line.SetPropertyBlock(block);
        }

        void SetProgress(float v)
        {
            block.SetFloat(ProgressId, v);
            line.SetPropertyBlock(block);
        }

        void SetOpacity(float v)
        {
            block.SetFloat(OpacityId, Mathf.Clamp01(v));
            line.SetPropertyBlock(block);
        }

        // 编辑器里看一条样例线，参数调完立刻有反馈
        void PreviewOnce()
        {
            Cache();
            if (line == null) return;

            var rng = new System.Random(previewSeed);
            var p = new WindGustParams
            {
                Length = 7f,
                Width = 0.22f,
                BendAmount = 0.24f,
                BendWaves = 1.2f,
                RiseAmount = 0.4f,
                LoopAngle = 250f,
                LoopSide = 1f,
                LoopRadiusScale = 0.06f,
                CoreSoftness = 2.2f,
                WobbleFreq = 4.5f,
                Opacity = 0.6f,
                LifeTime = 3f,
                FlowSpeed = 0.3f,
                FlowTiling = 2f,
                Phase = 0f,
                YawDeg = 0f
            };

            BuildCurve(rng, p);
            ApplyMaterial(p);
            SetProgress(1.2f);
            SetOpacity(p.Opacity);
        }
    }
}
