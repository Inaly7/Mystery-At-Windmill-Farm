// 定点气流发射器：挂在一个空物体上，每隔一段随机时间刮"一阵"风。
//
// 怎么做到"每次都不像重复"，靠四层随机叠起来：
//   1. 间隔随机       —— 不会形成能被记住的节奏（这点最省事也最有效）
//   2. 每次条数随机   —— 有时一笔，有时三四笔，形成"阵风"的疏密感
//   3. 每条的形状随机 —— 长度 / 宽度 / 弯几个弯 / 卷环角度与方向 全部独立掷骰
//   4. 出场错峰       —— 同一阵里的几条错开 0.1~0.35 秒，像风连续掠过去，
//                        而不是"啪"一下同时冒出来（同时出现最容易被识破是复用的）
//
// 唯一被约束的是【主风向】：所有线都围绕 baseDirection ± directionJitter 摆。
// 完全不约束方向会显得像一堆无头苍蝇，有主方向才像同一股气流。
//
// 池：只在运行时创建，编辑模式不动场景（[ExecuteAlways] 下建物体会把场景标脏）。
using System.Collections.Generic;
using UnityEngine;

namespace WindmillFarm.Visual
{
    [DisallowMultipleComponent]
    public sealed class WindGustField : MonoBehaviour
    {
        struct Pending
        {
            public float FireAt;
            public Vector3 LocalPos;
            public WindGustParams Params;
            public int Seed;
        }

        [Header("播放节奏（秒）")]
        [Tooltip("两次起风的间隔下限")]
        public float intervalMin = 3.5f;

        [Tooltip("两次起风的间隔上限。和下限差得越大越不容易被听出规律")]
        public float intervalMax = 8f;

        [Tooltip("第一次起风前的等待，避免场景一加载就喷")]
        public float startDelay = 0.8f;

        [Header("每次几笔")]
        public int countMin = 1;
        public int countMax = 3;

        [Tooltip("同一阵里几条线的错峰范围（秒）")]
        public float staggerMin = 0.1f;
        public float staggerMax = 0.35f;

        [Header("空间散布（本地坐标）")]
        [Tooltip("起点在这个半径内随机偏移（米）")]
        public float radius = 1.5f;

        [Tooltip("在水平散布之外，再上下抖这个高度（米）")]
        public float heightSpread = 0.6f;

        [Header("主风向")]
        [Tooltip("整阵风的平均方向（本地空间，只用水平分量）")]
        public Vector3 baseDirection = Vector3.right;

        [Tooltip("每条线相对主风向的随机偏角（度）")]
        [Range(0f, 180f)]
        public float directionJitter = 55f;

        [Header("线条尺寸（米）")]
        public float lengthMin = 5f;
        public float lengthMax = 9f;
        public float widthMin = 0.16f;
        public float widthMax = 0.30f;

        [Header("造型随机范围")]
        [Range(0f, 0.5f)] public float bendAmountMin = 0.12f;
        [Range(0f, 0.5f)] public float bendAmountMax = 0.32f;
        [Range(0f, 2f)] public float riseAmountMax = 0.5f;

        [Tooltip("末端打卷的概率。参考图那种风线通常只有一部分会卷")]
        [Range(0f, 1f)]
        public float loopChance = 0.55f;

        [Header("质感")]
        [Range(0f, 1f)] public float opacityMin = 0.45f;
        [Range(0f, 1f)] public float opacityMax = 0.75f;
        public float lifeTimeMin = 2.2f;
        public float lifeTimeMax = 3.8f;

        [Tooltip("线条颜色，吉卜力风格用偏暖的米白")]
        public Color tint = new Color(1f, 0.97f, 0.92f, 1f);

        [Header("性能")]
        [Tooltip("同时存在的线条上限。超了就不补，宁可少画不可掉帧")]
        public int maxConcurrent = 10;

        [Tooltip("气流材质。留空会在运行时按 Shader 名兜底创建")]
        public Material gustMaterial;

        readonly List<WindGustRibbon> pool = new List<WindGustRibbon>();
        readonly List<Pending> pending = new List<Pending>();

        float clock;
        float nextAt;
        int cursor;
        bool ready;

        void OnEnable()
        {
            if (!Application.isPlaying) return;

            EnsurePool();
            clock = 0f;

            // 初始延迟之外再抖半个间隔：场景里通常不止一个风点，
            // 如果都从 startDelay 开始计时，所有位置会整齐地同时起风，一眼假。
            nextAt = Mathf.Max(0f, startDelay) + Random.Range(0f, Mathf.Max(0.1f, intervalMax * 0.5f));
            ready = true;
        }

        void OnDisable()
        {
            ready = false;
            pending.Clear();
            if (!Application.isPlaying) return;

            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null) pool[i].Stop();
            }
        }

        void EnsurePool()
        {
            if (pool.Count > 0) return;

            var mat = ResolveMaterial();
            int n = Mathf.Max(1, maxConcurrent);

            // 先把 Prefab 里预置的样例线收编进池。那条线是给编辑模式预览用的，
            // 运行时如果不收编，它会一直挂着不回收，视觉上等于永远多一笔。
            var existing = GetComponentsInChildren<WindGustRibbon>(true);
            for (int i = 0; i < existing.Length; i++)
            {
                var r = existing[i];
                if (r == null || r.transform.parent != transform) continue;

                r.previewInEditMode = false;
                r.gameObject.SetActive(false);
                pool.Add(r);
            }

            for (int i = pool.Count; i < n; i++)
            {
                var go = new GameObject($"Gust_{i:00}");
                go.transform.SetParent(transform, false);
                go.SetActive(false);

                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = mat;
                lr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
                lr.widthMultiplier = 0.2f;
                lr.positionCount = 0;

                var ribbon = go.AddComponent<WindGustRibbon>();
                ribbon.previewInEditMode = false;
                pool.Add(ribbon);
            }
        }

        Material ResolveMaterial()
        {
            if (gustMaterial != null) return gustMaterial;

            var shader = Shader.Find("WindmillFarm/VFX/WindGust");
            if (shader == null)
            {
                Debug.LogWarning("[WindGustField] 找不到 Shader 'WindmillFarm/VFX/WindGust'，气流将不可见。");
                return null;
            }

            gustMaterial = new Material(shader) { name = "m_windGust(runtime)" };
            return gustMaterial;
        }

        void Update()
        {
            if (!ready || !Application.isPlaying) return;

            clock += Time.deltaTime;

            // 错峰出场
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (clock < pending[i].FireAt) continue;
                Fire(pending[i]);
                pending.RemoveAt(i);
            }

            if (clock >= nextAt)
            {
                Burst();
                nextAt = clock + Random.Range(intervalMin, intervalMax);
            }
        }

        void Burst()
        {
            int count = Random.Range(countMin, countMax + 1);

            for (int i = 0; i < count; i++)
            {
                var seed = Random.Range(int.MinValue, int.MaxValue);
                var rng = new System.Random(seed);

                float stagger = i == 0 ? 0f : Random.Range(staggerMin, staggerMax) * i;

                pending.Add(new Pending
                {
                    FireAt = clock + stagger,
                    LocalPos = RandomPoint(),
                    Params = BuildParams(rng, RandomYaw()),
                    Seed = seed
                });
            }
        }

        Vector3 RandomPoint()
        {
            // 球内均匀取点，再单独抖高度 —— 比直接用球坐标更不容易出现"中间空、边上挤"
            Vector3 v = Random.insideUnitSphere * radius;
            v.y = Random.Range(-heightSpread, heightSpread);
            return v;
        }

        float RandomYaw()
        {
            Vector3 d = baseDirection;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) d = Vector3.right;
            d.Normalize();

            // Ribbon 里 fwd = (sin yaw, 0, cos yaw)，所以这里用 atan2(x, z)
            float baseYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            return baseYaw + Random.Range(-directionJitter, directionJitter);
        }

        WindGustParams BuildParams(System.Random rng, float yaw)
        {
            bool loop = rng.NextDouble() < loopChance;

            return new WindGustParams
            {
                Length = Random.Range(lengthMin, lengthMax),
                Width = Random.Range(widthMin, widthMax),
                BendAmount = Random.Range(bendAmountMin, bendAmountMax),
                BendWaves = 0.9f + (float)rng.NextDouble() * 1.4f,
                RiseAmount = (float)rng.NextDouble() * riseAmountMax,
                LoopAngle = loop ? 190f + (float)rng.NextDouble() * 140f : 0f,
                LoopSide = rng.NextDouble() < 0.5 ? -1f : 1f,
                LoopRadiusScale = 0.045f + (float)rng.NextDouble() * 0.035f,
                CoreSoftness = 1.8f + (float)rng.NextDouble() * 1.0f,
                WobbleFreq = 3.2f + (float)rng.NextDouble() * 3.2f,
                Opacity = Random.Range(opacityMin, opacityMax),
                LifeTime = Random.Range(lifeTimeMin, lifeTimeMax),
                FlowSpeed = 0.22f + (float)rng.NextDouble() * 0.22f,
                FlowTiling = 1.4f + (float)rng.NextDouble() * 1.4f,
                Phase = (float)rng.NextDouble() * 10f,
                YawDeg = yaw
            };
        }

        void Fire(in Pending p)
        {
            var ribbon = TakeFree();
            if (ribbon == null) return; // 池满了就跳过这一笔，不扩容

            ribbon.transform.localPosition = p.LocalPos;
            ribbon.transform.localRotation = Quaternion.identity;
            ribbon.Play(p.Params, p.Seed);
        }

        WindGustRibbon TakeFree()
        {
            if (pool.Count == 0) return null;

            for (int i = 0; i < pool.Count; i++)
            {
                int idx = (cursor + i) % pool.Count;
                var r = pool[idx];
                if (r == null || r.gameObject.activeSelf) continue;

                cursor = (idx + 1) % pool.Count;
                return r;
            }
            return null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.75f, 0.92f, 1f, 0.85f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(Vector3.zero, radius);

            Vector3 d = baseDirection;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) d = Vector3.right;
            Gizmos.DrawLine(Vector3.zero, d.normalized * Mathf.Max(1f, radius * 2f));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
