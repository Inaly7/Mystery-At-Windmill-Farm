using UnityEngine;

namespace EggRescue
{
    /// <summary>
    /// 走路时在脚后扬起土块和灰色卡通烟，靠缩小消失，不靠变透明。
    /// 卡通烟尘图换成 Assets/Res/vfx/mats/m_walkSmoke 的 Base Map 即可。
    /// </summary>
    public sealed class PlayerWalkDust : MonoBehaviour
    {
        const float MinSpeed = 0.35f;
        const float StepDistance = 0.36f;
        const int PuffCount = 3;
        const int SmokeCount = 2;

        static readonly Color DustA = new Color(0.62f, 0.52f, 0.4f, 1f);
        static readonly Color DustB = new Color(0.8f, 0.74f, 0.62f, 1f);
        static readonly Color SmokeA = new Color(0.55f, 0.55f, 0.58f, 1f);
        static readonly Color SmokeB = new Color(0.78f, 0.78f, 0.8f, 1f);

        CharacterController _cc;
        ParticleSystem _dust;
        ParticleSystem _smoke;
        Material _dustMaterial;
        Vector3 _lastPos;
        float _step;
        float _side = 1f;
        bool _hasLast;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (_cc == null) return;
            _dustMaterial = CreateDustMaterial();
            if (_dustMaterial != null) _dust = CreateDust(transform, _dustMaterial);
            var smokeMaterial = LoadSmokeMaterial();
            if (smokeMaterial != null) _smoke = CreateSmoke(transform, smokeMaterial);
        }

        void OnDestroy()
        {
            if (_dustMaterial != null) Destroy(_dustMaterial);
        }

        void LateUpdate()
        {
            if (_cc == null || (_dust == null && _smoke == null)) return;
            var now = transform.position;
            if (!_hasLast)
            {
                _lastPos = now;
                _hasLast = true;
                return;
            }

            var delta = now - _lastPos;
            _lastPos = now;
            delta.y = 0f;
            var dist = delta.magnitude;
            if (dist > 1.5f)
            {
                _step = 0f;
                return;
            }

            var speed = dist / Mathf.Max(Time.deltaTime, 0.0001f);
            if (!_cc.isGrounded || speed < MinSpeed) return;
            _step += dist;
            if (_step < StepDistance) return;
            _step = 0f;
            EmitStep();
        }

        void EmitStep()
        {
            _side = -_side;
            var back = -transform.forward;
            back.y = 0f;
            if (back.sqrMagnitude < 0.0001f) back = Vector3.back;
            else back.Normalize();
            var right = transform.right;
            var foot = _cc.center.y - _cc.height * 0.5f;
            var origin = transform.position + Vector3.up * (foot + 0.03f) + back * 0.14f + right * (_side * 0.06f);
            EmitDust(origin, back, right);
            EmitSmoke(origin + Vector3.up * 0.05f + back * 0.04f, back, right);
        }

        void EmitDust(Vector3 origin, Vector3 back, Vector3 right)
        {
            if (_dust == null) return;
            if (!_dust.isPlaying) _dust.Play();
            for (var i = 0; i < PuffCount; i++)
            {
                var p = new ParticleSystem.EmitParams();
                p.position = origin + new Vector3(
                    Random.Range(-0.04f, 0.04f),
                    Random.Range(0f, 0.02f),
                    Random.Range(-0.04f, 0.04f));
                p.velocity = back * Random.Range(0.04f, 0.18f)
                    + Vector3.up * Random.Range(0.12f, 0.32f)
                    + right * Random.Range(-0.08f, 0.08f);
                p.startLifetime = Random.Range(0.26f, 0.42f);
                p.startSize = Random.Range(0.055f, 0.12f);
                var color = Color.Lerp(DustA, DustB, Random.value);
                color.a = 1f;
                p.startColor = color;
                _dust.Emit(p, 1);
            }
        }

        void EmitSmoke(Vector3 origin, Vector3 back, Vector3 right)
        {
            if (_smoke == null) return;
            if (!_smoke.isPlaying) _smoke.Play();
            for (var i = 0; i < SmokeCount; i++)
            {
                var p = new ParticleSystem.EmitParams();
                p.position = origin + new Vector3(
                    Random.Range(-0.03f, 0.03f),
                    Random.Range(0f, 0.04f),
                    Random.Range(-0.03f, 0.03f));
                p.velocity = back * Random.Range(0.02f, 0.1f)
                    + Vector3.up * Random.Range(0.22f, 0.48f)
                    + right * Random.Range(-0.06f, 0.06f);
                p.startLifetime = Random.Range(0.48f, 0.72f);
                p.startSize = Random.Range(0.11f, 0.2f);
                p.rotation = Random.Range(0f, Mathf.PI * 2f);
                var color = Color.Lerp(SmokeA, SmokeB, Random.value);
                color.a = 1f;
                p.startColor = color;
                _smoke.Emit(p, 1);
            }
        }

        static ParticleSystem CreateDust(Transform parent, Material material)
        {
            return CreateSystem(parent, "WalkDust", material, 0.35f, 0.25f, new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.4f, 1f),
                new Keyframe(1f, 0f)));
        }

        static ParticleSystem CreateSmoke(Transform parent, Material material)
        {
            return CreateSystem(parent, "WalkSmoke", material, -0.05f, 0.35f, new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.18f, 1.08f),
                new Keyframe(0.45f, 1f),
                new Keyframe(1f, 0f)));
        }

        static ParticleSystem CreateSystem(Transform parent, string name, Material material, float gravity, float maxSize, AnimationCurve size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();

            // AddComponent 出来的粒子系统默认已经在播；
            // 而 main.duration 只允许在"停止且已清空"的状态下改，否则报
            // "Setting the duration while system is still playing is not supported"。
            // 所以必须先 Stop(clear)，再做任何配置。
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = 0.4f;
            main.startSpeed = 0.2f;
            main.startSize = 0.08f;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = false;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, size);

            var shape = ps.shape;
            shape.enabled = false;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = maxSize;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        static Material CreateDustMaterial()
        {
            var source = LoadDustMaterial();
            if (source == null) return null;
            var material = new Material(source);
            material.SetFloat("_Blend", 0f);
            material.SetInt("_SrcBlend", 5);
            material.SetInt("_DstBlend", 10);
            material.SetInt("_SrcBlendAlpha", 1);
            material.SetInt("_DstBlendAlpha", 10);
            material.SetInt("_ZWrite", 0);
            return material;
        }

        static Material LoadDustMaterial()
        {
            var catalog = Resources.Load<VfxCatalog>("VfxCatalog");
            if (catalog != null && catalog.DustMaterial != null) return catalog.DustMaterial;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Res/vfx/mats/m_particle.mat");
#else
            return null;
#endif
        }

        static Material LoadSmokeMaterial()
        {
            var catalog = Resources.Load<VfxCatalog>("VfxCatalog");
            if (catalog != null && catalog.SmokeMaterial != null) return catalog.SmokeMaterial;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Res/vfx/mats/m_walkSmoke.mat");
#else
            return null;
#endif
        }
    }
}
