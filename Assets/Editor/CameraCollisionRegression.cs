using System;
using System.Reflection;
using EggRescue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runs in an isolated empty scene without loading saves or starting gameplay.
public static class CameraCollisionRegression
{
    [MenuItem("Tools/Egg Rescue/Validate Camera Collision")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run camera regression outside Play Mode.");
        var previousScene = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var instance = typeof(PlayerController).GetProperty("Instance");
        var previousPlayer = PlayerController.Instance;
        try
        {
            var origin = new Vector3(10000f, 10000f, 10000f);
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.transform.position = origin;
            var controller = player.AddComponent<PlayerController>();
            instance.GetSetMethod(true).Invoke(null, new object[] { controller });
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.transform.SetParent(player.transform, false);
            child.transform.localPosition = Vector3.back * 2f;
            var cameraObject = new GameObject("Regression Camera", typeof(Camera));
            var camera = cameraObject.AddComponent<ThirdPersonCamera>();
            Physics.SyncTransforms();

            Check(!Overlap(origin), "Player root must not block overlap checks");
            Check(!Overlap(child.transform.position), "Player children must not block overlap checks");
            Check(Vector3.Distance(Solve(camera, origin), origin + Vector3.back * 2.5f) < 0.01f,
                "Player colliders must not retract the camera");

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.localScale = new Vector3(10f, 10f, 0.2f);
            wall.transform.position = origin + Vector3.forward * 0.3f;
            Physics.SyncTransforms();
            Check(Vector3.Distance(Solve(camera, origin), origin + Vector3.back * 2.5f) < 0.01f,
                "An initial overlap in front must not collapse the arm behind the player");
            Call(camera, "ResetArm", origin, origin + Vector3.back * 0.01f);
            var escaped = (Vector3)Call(camera, "ResolvePosition", origin, origin + Vector3.back * 2.5f, 1f / 60f);
            Check(!Overlap(escaped) && Vector3.Distance(origin, escaped) < 0.35f,
                "A short arm grazing a wall must escape just enough, without staying inside or jumping to full length");

            wall.transform.position = origin + Vector3.back * 1.5f;
            Physics.SyncTransforms();
            var blocked = Solve(camera, origin);
            Check(blocked.z > wall.transform.position.z + 0.4f && blocked.z < origin.z - 0.5f,
                "A wall behind must retract the camera without crossing it");
            Check(!Overlap(blocked), "Retracted camera must be clear of world geometry");

            wall.transform.localScale = new Vector3(10f, 0.5f, 0.2f);
            wall.transform.position = origin + new Vector3(0f, -0.2f, -0.55f);
            Physics.SyncTransforms();
            var close = Solve(camera, origin);
            Check(Vector3.Distance(close, origin) < 0.9f && Mathf.Abs(close.y - origin.y) < 0.001f && !Overlap(close),
                "A nearby low obstacle must shorten the arm without lifting the camera overhead");

            camera.transform.position = close;
            Call(camera, "ResetArm", origin, close);
            for (var frame = 0; frame < 120; frame++)
            {
                var wanted = origin + Quaternion.Euler(0f, frame * 3f, 0f) * Vector3.back * 2.5f;
                var before = camera.transform.position;
                var next = (Vector3)Call(camera, "ResolvePosition", origin, wanted, 1f / 60f);
                Check(Vector3.Distance(origin, next) - Vector3.Distance(origin, before) <= 2f / 60f + 0.005f,
                    "Rotating clear of an obstacle must not suddenly extend the arm");
                Check(Vector3.Cross(wanted - origin, next - origin).magnitude < 0.005f,
                    "Orbit collision correction must stay on the requested arm, without sideways detours");
                Check(!Overlap(next), "Camera transition must not enter the obstacle");
                camera.transform.position = next;
            }
            wall.SetActive(false);
            Physics.SyncTransforms();
            for (var frame = 0; frame < 180; frame++)
            {
                var before = camera.transform.position;
                var next = (Vector3)Call(camera, "ResolvePosition", origin, origin + Vector3.back * 2.5f, 1f / 60f);
                Check(Vector3.Distance(before, next) <= 2f / 60f + 0.005f,
                    "Leaving the obstacle must restore the arm gradually");
                camera.transform.position = next;
            }
            Check(Vector3.Distance(camera.transform.position, origin + Vector3.back * 2.5f) < 0.01f,
                "The camera must return to the normal arm after the obstacle clears");

            // The player falls beyond the edge while the camera is still above the roof.
            // Its downward movement sweep must not strand it on the upper surface.
            wall.SetActive(true);
            wall.transform.localScale = new Vector3(10f, 0.5f, 3f);
            wall.transform.position = origin + Vector3.back * 2f;
            Physics.SyncTransforms();
            foreach (var deltaTime in new[] { 1f / 30f, 1f / 60f, 1f / 120f })
            {
                var pivot = origin + Vector3.up * 3f;
                camera.transform.position = pivot + Vector3.back * 2.5f;
                Call(camera, "ResetArm", pivot, camera.transform.position);
                for (var frame = 0; frame < Mathf.RoundToInt(1f / deltaTime); frame++)
                {
                    pivot += Vector3.down * (6f * deltaTime);
                    var next = (Vector3)Call(camera, "ResolvePosition", pivot, pivot + Vector3.back * 2.5f, deltaTime);
                    Check(!Overlap(next), "Falling past a roof must keep the camera outside geometry");
                    camera.transform.position = next;
                }
                Check(camera.transform.position.y < origin.y - 2f,
                    "The camera must follow below the roof instead of remaining stuck on top");
                Check(Vector3.Distance(camera.transform.position, pivot) <= 2.51f,
                    "A fall must not stretch the camera arm beyond its normal length");
            }
            wall.SetActive(false);
            Physics.SyncTransforms();

            // Repeated brief gaps in a collider must not pump the camera in and out.
            wall.transform.localScale = new Vector3(10f, 10f, 0.2f);
            wall.transform.position = origin + Vector3.back * 1.5f;
            foreach (var deltaTime in new[] { 1f / 30f, 1f / 60f, 1f / 120f })
            {
                wall.SetActive(true);
                Physics.SyncTransforms();
                var constrained = Solve(camera, origin);
                camera.transform.position = constrained;
                Call(camera, "ResetArm", origin, constrained);
                for (var frame = 0; frame < 60; frame++)
                {
                    wall.SetActive(frame % 2 == 0);
                    Physics.SyncTransforms();
                    var next = (Vector3)Call(camera, "ResolvePosition", origin, origin + Vector3.back * 2.5f, deltaTime);
                    Check(Vector3.Distance(next, constrained) < 0.005f,
                        "Brief collider gaps must not repeatedly zoom the camera out and in");
                    camera.transform.position = next;
                }
                wall.SetActive(false);
                Physics.SyncTransforms();
                var previousDistance = Vector3.Distance(origin, camera.transform.position);
                for (var frame = 0; frame < Mathf.RoundToInt(3f / deltaTime); frame++)
                {
                    var next = (Vector3)Call(camera, "ResolvePosition", origin, origin + Vector3.back * 2.5f, deltaTime);
                    var distance = Vector3.Distance(origin, next);
                    Check(distance >= previousDistance - 0.002f && distance - previousDistance <= 2f * deltaTime + 0.005f,
                        "Recovery must be monotonic and speed-limited at every supported frame rate");
                    previousDistance = distance;
                    camera.transform.position = next;
                }
                Check(Vector3.Distance(camera.transform.position, origin + Vector3.back * 2.5f) < 0.01f,
                    "Stable clearance must eventually restore the full camera distance");
            }

            var cameraTarget = new GameObject("Camera target").transform;
            cameraTarget.position = origin;
            camera.SetTarget(cameraTarget);
            camera.SetView(0f, 8f);
            var orbit = Quaternion.Euler(8f, 0f, 0f);
            wall.SetActive(true);
            // Move a solid wall through the old camera position towards the player.
            for (var frame = 0; frame < 60; frame++)
            {
                wall.transform.position = origin + Vector3.back * Mathf.Lerp(2.5f, 0.55f, frame / 59f);
                Physics.SyncTransforms();
                Call(camera, "LateUpdate");
                Check(!Overlap(camera.transform.position), "A moving collider must not squeeze into the camera");
                Check(Quaternion.Angle(camera.transform.rotation, orbit) < 0.01f,
                    "Collider pressure must not change player-controlled pitch, yaw or roll");
            }
            wall.SetActive(false);
            Physics.SyncTransforms();
            camera.SetView(110f, -20f);
            Call(camera, "LateUpdate");
            Check(Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(-20f, 110f, 0f)) < 0.01f,
                "Camera input must remain responsive after a collapsed arm recovers");

            var renderer = player.GetComponent<Renderer>();
            var originalMaterial = renderer.sharedMaterial;
            camera.transform.position = origin;
            Call(camera, "UpdatePlayerVisibility");
            Check(!renderer.forceRenderingOff && renderer.sharedMaterial.shader.name == "EggRescue/Camera Proximity Fade",
                "A close character must stay visible with a translucent material");
            camera.transform.position = origin + Vector3.back * 5f;
            Call(camera, "UpdatePlayerVisibility");
            Check(renderer.sharedMaterial == originalMaterial, "Camera clearance must restore the original material");
            camera.transform.position = origin;
            Call(camera, "UpdatePlayerVisibility");
            Call(camera, "OnDisable");
            Check(renderer.sharedMaterial == originalMaterial, "Disabling the camera must restore the original material");
            Debug.Log("Camera collision regression: collision, roof falls, stable orientation, edge hysteresis, smooth recovery and visibility checks passed.");
        }
        finally
        {
            instance.GetSetMethod(true).Invoke(null, new object[] { previousPlayer });
            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(previousScene);
        }
    }

    static Vector3 Solve(ThirdPersonCamera camera, Vector3 pivot)
    {
        return (Vector3)Call(camera, "AvoidGeometry", pivot, pivot + Vector3.back * 2.5f);
    }

    static bool Overlap(Vector3 position)
    {
        return (bool)typeof(ThirdPersonCamera).GetMethod("WorldOverlap",
            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { position, 0.3f });
    }

    static object Call(ThirdPersonCamera camera, string name, params object[] args)
    {
        return typeof(ThirdPersonCamera).GetMethod(name,
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(camera, args);
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Camera collision regression: " + message);
    }
}
