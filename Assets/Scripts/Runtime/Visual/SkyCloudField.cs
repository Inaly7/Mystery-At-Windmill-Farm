// 天空云场：所有云朵的父物体，提供"出生点"，并把球心（玩家位置）下发给 shader。
//
// 为什么 XZ 要跟随相机：云是"无限远"的东西。如果云场固定在世界原点，
// 玩家走到场景边缘时云会明显贴到侧面，一眼假。跟随相机 XZ、Y 保持不动，
// 各朵云按自己 azimuth 公转，互不影响。
//
// 为什么要把玩家位置下发给 shader：CloudCard.shader 需要以玩家为球心把卡片顶点
// 投到球面上做弧度。这里用全局属性 _SkyCenter，所有材质共用一份，零额外开销。
// 编辑态用 SceneView 相机，这样在场景里摆机位时看到的弧度是对的。
//
// 注意：根物体请保持 scale=1、不旋转——云朵的 distance/azimuth/height 都是
// 按"父物体位置 + 世界 Y 轴"算的。安装菜单建出来的根物体满足这个条件。
using UnityEngine;

namespace WindmillFarm.Visual
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SkyCloudField : MonoBehaviour
    {
        static readonly int SkyCenterId = Shader.PropertyToID("_SkyCenter");

        [Tooltip("跟随相机的 XZ，让云始终罩在玩家头顶。Y 保持不动")]
        public bool followCameraXZ = true;

        [Tooltip("要跟随的相机。留空 = Camera.main；编辑态用场景视图相机")]
        public Camera targetCamera;

        void Update()
        {
            var cam = ResolveCamera();

            // 弧度用的球心：先同步，编辑态也要对，否则在场景里摆机位时弧度是错的
            if (cam != null)
            {
                var c = cam.transform.position;
                Shader.SetGlobalVector(SkyCenterId, new Vector4(c.x, c.y, c.z, 0f));
            }

            // 编辑态不挪根节点：否则在场景里导航时云会跟着跑，没法摆位
            if (!followCameraXZ || cam == null || !Application.isPlaying) return;

            var p = transform.position;
            p.x = cam.transform.position.x;
            p.z = cam.transform.position.z;
            transform.position = p;
        }

        Camera ResolveCamera()
        {
            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                // 编辑态优先用场景视图相机：玩家还没跑起来，摆机位时弧度要跟着它
                var sv = UnityEditor.SceneView.lastActiveSceneView;
                if (sv != null && sv.camera != null) return sv.camera;
#endif
            }
            return targetCamera != null ? targetCamera : Camera.main;
        }

        void OnDrawGizmosSelected()
        {
            // 画出出生点参考：一个小十字
            var c = transform.position;
            const float s = 8f;
            Gizmos.color = new Color(1f, 0.9f, 0.5f, 0.8f);
            Gizmos.DrawLine(c - Vector3.right * s, c + Vector3.right * s);
            Gizmos.DrawLine(c - Vector3.forward * s, c + Vector3.forward * s);
            Gizmos.DrawLine(c - Vector3.up * s, c + Vector3.up * s);
        }
    }
}
