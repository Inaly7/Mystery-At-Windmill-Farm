using UnityEngine;

namespace EggRescue
{
    /// <summary>
    /// 绕 RectTransform 自身 pivot 匀速转动。主菜单两层云的 pivot 在画面右下角，慢转就是云在飘。
    /// </summary>
    public sealed class MenuCloudDrift : MonoBehaviour
    {
        [SerializeField, Tooltip("每秒转过的角度。正值为逆时针，负值为顺时针。")]
        float degreesPerSecond = 0.5f;

        void Update()
        {
            var step = degreesPerSecond * Time.unscaledDeltaTime;
            if (Mathf.Approximately(step, 0f)) return;
            transform.Rotate(0f, 0f, step);
        }
    }
}
