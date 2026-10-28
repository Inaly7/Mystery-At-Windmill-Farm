// 单朵云：位置、尺寸、公转、地平线淡出全部自己做主，父级只提供"出生点"。
//
// 设计取舍：不再分 High/Mid/Low 层。分层的好处是批量产内容，但逐朵手调时
// （"这朵大一点、那朵贴地飘"）按朵给参数更直观——想批量就在 Hierarchy 里
// 多选一起改。
//
// 位置语义（都相对父物体位置；父物体请保持 scale=1、不旋转）：
//   distance  距出生点的水平距离（米）
//   azimuth   方位角（度，绕 Y 轴；0 = 出生点 +Z 方向，顺时针增大）
//   height    世界高度（米）= 出生点 Y + 此值
// 每帧按这三个值重算位置，所以公转只是累加 azimuth，不会累积漂移。
//
// 朝向：卡片 +Z 始终朝向出生点（玩家在球心看过来是正面）；roll 是绕视线的
// 额外倾角，避免所有云一个朝向很假。
//
// 淡出参数用 MaterialPropertyBlock 逐渲染器下发：四张贴图共用四个材质，
// 但每朵云可以有不同的阈值和最低透明度（代价是这些渲染器不走 SRP Batcher，
// 二十来张卡片无所谓）。
//
// 注意：位置请在 Inspector 改字段，不要直接拖物体——下次改任何字段时
// 位置会被字段值重算覆盖。
using UnityEngine;

namespace WindmillFarm.Visual
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public sealed class CloudCard : MonoBehaviour
    {
        static readonly int FadeStartId = Shader.PropertyToID("_HorizonFadeStart");
        static readonly int FadeEndId = Shader.PropertyToID("_HorizonFadeEnd");
        static readonly int MinOpacityId = Shader.PropertyToID("_HorizonMinOpacity");
        static readonly int AlphaScaleId = Shader.PropertyToID("_AlphaScale");

        [Header("出生位置（相对父物体）")]
        [Tooltip("距出生点的水平距离（米）")]
        public float distance = 320f;

        [Tooltip("方位角（度）。0 = 出生点 +Z 方向，顺时针增大")]
        [Range(-360f, 360f)]
        public float azimuth = 0f;

        [Tooltip("世界高度（米）= 出生点 Y + 此值")]
        public float height = 120f;

        [Header("尺寸与朝向")]
        [Tooltip("云朵宽度（米）。高度按贴图宽高比自动算")]
        public float size = 140f;

        [Tooltip("绕视线方向的额外倾角（度），避免所有云一个朝向")]
        [Range(-45f, 45f)]
        public float roll = 0f;

        [Header("公转")]
        [Tooltip("角速度（度/秒）。可正可负，0 = 不动")]
        public float angularSpeed = 0.8f;

        [Tooltip("编辑状态下也旋转。默认关：否则场景会被持续改脏，一直弹保存")]
        public bool rotateInEditMode = false;

        [Header("不透明度")]
        [Tooltip("整体不透明度，逐朵独立")]
        [Range(0f, 1f)]
        public float opacity = 1f;

        [Header("地平线淡出")]
        [Tooltip("仰角低于此值（度）开始变透明")]
        [Range(-5f, 60f)]
        public float fadeStartAngle = 8f;

        [Tooltip("仰角到此值（度）时降到最低透明度")]
        [Range(0f, 90f)]
        public float fadeEndAngle = 26f;

        [Tooltip("地平线附近的最低不透明度。0 = 完全消失")]
        [Range(0f, 1f)]
        public float horizonOpacity = 0.3f;

        MaterialPropertyBlock block;
        float lastRealtime;

        void OnEnable()
        {
            Apply();
        }

        void OnValidate()
        {
            Apply();
        }

        void Update()
        {
            if (Application.isPlaying)
            {
                // 父级（天空云场）会随相机移动，所以每帧都要按当前参数重算位置
                azimuth = Mathf.Repeat(azimuth + angularSpeed * Time.deltaTime, 360f);
                Apply();
            }
            else if (rotateInEditMode)
            {
                var now = Time.realtimeSinceStartup;
                var dt = now - lastRealtime;
                lastRealtime = now;
                azimuth = Mathf.Repeat(azimuth + angularSpeed * dt, 360f);
                Apply();
            }
        }

        /// <summary>
        /// 按当前参数重算位置 / 朝向 / 缩放，并把淡出参数推给渲染器。
        /// 编辑态改字段、运行期公转、父物体移动，都走这一个入口。
        /// </summary>
        public void Apply()
        {
            var parent = transform.parent;
            var centre = parent != null ? parent.position : Vector3.zero;

            var az = azimuth * Mathf.Deg2Rad;
            var pos = centre + new Vector3(Mathf.Sin(az) * distance, height, Mathf.Cos(az) * distance);
            transform.position = pos;

            var toCentre = centre - pos;
            if (toCentre.sqrMagnitude > 1e-4f)
            {
                transform.rotation = Quaternion.LookRotation(toCentre.normalized, Vector3.up)
                                   * Quaternion.Euler(0f, 0f, roll);
            }

            ApplyScale();
            ApplyProperties();
        }

        void ApplyScale()
        {
            var tex = CardTexture();
            var aspect = tex != null && tex.height > 0 ? (float)tex.width / tex.height : 2f;
            transform.localScale = new Vector3(size, size / aspect, 1f);
        }

        Texture2D CardTexture()
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr == null) return null;
            var mat = mr.sharedMaterial;
            return mat != null ? mat.mainTexture as Texture2D : null;
        }

        void ApplyProperties()
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            block.SetFloat(FadeStartId, fadeStartAngle);
            block.SetFloat(FadeEndId, fadeEndAngle);
            block.SetFloat(MinOpacityId, horizonOpacity);
            block.SetFloat(AlphaScaleId, opacity);
            mr.SetPropertyBlock(block);
        }

        void OnDrawGizmosSelected()
        {
            // 选中时画出卡片范围，场景里一眼看到在调哪一朵
            Gizmos.color = new Color(0.55f, 0.85f, 1f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 1f, 0f));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
