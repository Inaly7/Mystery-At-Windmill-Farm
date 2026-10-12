using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EggRescue
{
    /// <summary>
    /// 让 Animals 下按名字编号的角色，沿着右下角蛋露出的那一段弧往前走。
    /// 身体跟着蛋的朝向转，头顶指向蛋表面的外法线。1 号先入画，走完后再从队尾出现。
    /// </summary>
    public sealed class MenuEggParade : MonoBehaviour
    {
        [SerializeField] RectTransform egg;
        [SerializeField] float centerX = -2.5f;
        [SerializeField] float centerY = -6f;
        [SerializeField] float radiusX = 703.5f;
        [SerializeField] float radiusY = 400f;
        [SerializeField] float thetaStart = 196f;
        [SerializeField] float thetaEnd = 78f;
        [SerializeField] float speed = 82.5f;
        [SerializeField] float spacing = 230f;
        [SerializeField, Tooltip("母鸡比别的角色大，和前一只、后一只都多留出这段空档。")]
        float henExtraSpacing = 52f;
        [SerializeField, Tooltip("开场时，队尾那只要已经走到弧线上的距离。编辑器里的摆放和这个一致。")]
        float startOnPath = 150f;
        [SerializeField, Range(0f, 0.3f)] float feetInset = 0.082f;

        struct Walker
        {
            public RectTransform Rect;
            public Image Image;
            public int Order;
            public float Lead;
        }

        readonly List<Walker> _walkers = new List<Walker>();
        Vector3[] _points;
        float[] _along;
        float _arcLength;
        float _loop;
        float _distance;

        void Start()
        {
            if (!Bind()) return;
            BuildPath();
            for (var i = 0; i < _walkers.Count; i++)
            {
                if (_walkers[i].Image != null) _walkers[i].Image.raycastTarget = false;
            }
        }

        void LateUpdate()
        {
            if (_walkers.Count == 0 || _arcLength <= 1f) return;
            _distance += speed * Time.unscaledDeltaTime;
            var loop = _loop;
            var order = new List<int>();
            for (var i = 0; i < _walkers.Count; i++)
            {
                var walker = _walkers[i];
                var along = Mathf.Repeat(_distance - walker.Lead, loop);
                var visible = along <= _arcLength;
                if (walker.Image != null) walker.Image.enabled = visible;
                if (!visible) continue;
                Place(walker, along / _arcLength);
                order.Add(i);
            }
            order.Sort((a, b) =>
            {
                var pa = Mathf.Repeat(_distance - _walkers[a].Lead, loop);
                var pb = Mathf.Repeat(_distance - _walkers[b].Lead, loop);
                return pa.CompareTo(pb);
            });
            for (var i = 0; i < order.Count; i++)
                _walkers[order[i]].Rect.SetSiblingIndex(i);
        }

        bool Bind()
        {
            if (egg == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas == null) return false;
                var images = canvas.GetComponentsInChildren<Image>(true);
                for (var i = 0; i < images.Length; i++)
                {
                    if (images[i] != null && images[i].name == "bg_egg")
                    {
                        egg = images[i].rectTransform;
                        break;
                    }
                }
            }
            if (egg == null) return false;

            _walkers.Clear();
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i) as RectTransform;
                if (child == null) continue;
                var order = _walkers.Count;
                int parsed;
                if (int.TryParse(child.name, out parsed)) order = parsed - 1;
                if (order < 0) order = 0;
                _walkers.Add(new Walker
                {
                    Rect = child,
                    Image = child.GetComponent<Image>(),
                    Order = order
                });
            }
            _walkers.Sort((a, b) => a.Order.CompareTo(b.Order));
            var hen = 0;
            for (var i = 0; i < _walkers.Count; i++)
            {
                if (_walkers[i].Order == 0) hen = i;
            }
            var lead = 0f;
            for (var i = 0; i < _walkers.Count; i++)
            {
                var walker = _walkers[i];
                walker.Order = i;
                walker.Lead = lead;
                var next = i + 1 < _walkers.Count ? i + 1 : 0;
                lead += spacing + (i == hen || next == hen ? henExtraSpacing : 0f);
                _walkers[i] = walker;
            }
            _loop = Mathf.Max(lead, spacing);
            if (_walkers.Count > 0)
                _distance = _walkers[_walkers.Count - 1].Lead + startOnPath;
            return _walkers.Count > 0;
        }

        void BuildPath()
        {
            const int samples = 48;
            _points = new Vector3[samples + 1];
            _along = new float[samples + 1];
            var length = 0f;
            for (var i = 0; i <= samples; i++)
            {
                var t = i / (float)samples;
                _points[i] = Scaled(Mathf.Lerp(thetaStart, thetaEnd, t));
                if (i > 0) length += Vector3.Distance(_points[i - 1], _points[i]);
                _along[i] = length;
            }
            _arcLength = length;
        }

        void Place(Walker walker, float u)
        {
            var local = Evaluate(u);
            var surface = egg.TransformPoint(Unscale(local));
            var ahead = egg.TransformPoint(Unscale(Evaluate(Mathf.Clamp01(u + 0.02f))));
            var behind = egg.TransformPoint(Unscale(Evaluate(Mathf.Clamp01(u - 0.02f))));
            var tangent = ahead - behind;
            if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.right;
            var normal = new Vector3(-tangent.y, tangent.x, 0f).normalized;
            normal.z = 0f;
            var center = egg.TransformPoint(new Vector3(centerX, centerY, 0f));
            if (Vector3.Dot(normal, surface - center) < 0f) normal = -normal;
            if (normal.sqrMagnitude < 0.0001f) normal = Vector3.up;
            else normal.Normalize();
            var inset = walker.Rect.rect.height * walker.Rect.lossyScale.y * feetInset;
            walker.Rect.position = surface - normal * inset;
            var angle = Mathf.Atan2(-normal.x, normal.y) * Mathf.Rad2Deg;
            walker.Rect.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        Vector3 Evaluate(float u)
        {
            var target = Mathf.Clamp01(u) * _arcLength;
            for (var i = 1; i < _along.Length; i++)
            {
                if (target > _along[i] && i < _along.Length - 1) continue;
                var span = _along[i] - _along[i - 1];
                var blend = span > 0.001f ? (target - _along[i - 1]) / span : 0f;
                return Vector3.Lerp(_points[i - 1], _points[i], blend);
            }
            return _points[_points.Length - 1];
        }

        Vector3 Scaled(float degrees)
        {
            var rad = degrees * Mathf.Deg2Rad;
            var local = new Vector3(
                centerX + radiusX * Mathf.Cos(rad),
                centerY + radiusY * Mathf.Sin(rad),
                0f);
            local.x *= egg.localScale.x;
            local.y *= egg.localScale.y;
            return local;
        }

        Vector3 Unscale(Vector3 scaled)
        {
            return new Vector3(
                egg.localScale.x != 0f ? scaled.x / egg.localScale.x : scaled.x,
                egg.localScale.y != 0f ? scaled.y / egg.localScale.y : scaled.y,
                0f);
        }
    }
}
