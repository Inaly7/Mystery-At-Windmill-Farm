using UnityEngine;
using UnityEngine.UI;

namespace EggRescue
{
    /// <summary>
    /// 绕自身 pivot 转动。主菜单两栋房子的扇叶 pivot 在叶片中心，也就是风车轴心。
    /// </summary>
    public sealed class MenuWindmillSpin : MonoBehaviour
    {
        [SerializeField, Tooltip("每秒转过的角度。负值为顺时针。")]
        float degreesPerSecond = -60f;

        void Awake()
        {
            var graphic = GetComponent<Graphic>();
            if (graphic != null) graphic.raycastTarget = false;
        }

        void Update()
        {
            var step = degreesPerSecond * Time.unscaledDeltaTime;
            if (Mathf.Approximately(step, 0f)) return;
            transform.Rotate(0f, 0f, step);
        }
    }
}
