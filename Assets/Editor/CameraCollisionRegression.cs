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

            wall.transform.position = origin + Vector3.back * 1.5f;
            Physics.SyncTransforms();
            var blocked = Solve(camera, origin);
            Check(blocked.z > wall.transform.position.z + 0.4f && blocked.z < origin.z - 0.5f,
                "A wall behind must retract the camera without crossing it");
            Check(!Overlap(blocked), "Retracted camera must be clear of world geometry");

            wall.transform.localScale = new Vector3(10f, 0.5f, 0.2f);
            wall.transform.position = origin + new Vector3(0f, -0.2f, -0.55f);
            Physics.SyncTransforms();
            var elevated = Solve(camera, origin);
            Check(Vector3.Distance(elevated, origin) >= 0.9f && elevated.y > origin.y && !Overlap(elevated),
                "A nearby low obstacle must use a clear elevated view instead of entering the body");

            camera.transform.position = elevated;
            typeof(ThirdPersonCamera).GetField("_previousPivot", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(camera, origin);
            typeof(ThirdPersonCamera).GetField("_avoiding", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(camera, true);
            for (var frame = 0; frame < 120; frame++)
            {
                var wanted = origin + Quaternion.Euler(0f, frame * 3f, 0f) * Vector3.back * 2.5f;
                var before = camera.transform.position;
                var next = (Vector3)Call(camera, "ResolvePosition", origin, wanted, 1f / 60f);
                // A free 3-degree orbit moves about 0.131 m at the full arm length.
                Check(Vector3.Distance(before, next) <= 0.14f,
                    "Rotating above an obstacle must not jump between camera positions");
                Check(!Overlap(next), "Camera transition must not enter the obstacle");
                camera.transform.position = next;
            }
            wall.SetActive(false);
            Physics.SyncTransforms();
            typeof(ThirdPersonCamera).GetField("_avoiding", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(camera, true);
            for (var frame = 0; frame < 180; frame++)
            {
                var before = camera.transform.position;
                var next = (Vector3)Call(camera, "ResolvePosition", origin, origin + Vector3.back * 2.5f, 1f / 60f);
                Check(Vector3.Distance(before, next) <= 4f / 60f + 0.005f,
                    "Leaving the obstacle must restore the arm gradually");
                camera.transform.position = next;
            }
            Check(Vector3.Distance(camera.transform.position, origin + Vector3.back * 2.5f) < 0.01f,
                "The camera must return to the normal arm after the obstacle clears");

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
            Debug.Log("Camera collision regression: collision, rotation continuity, recovery and visibility checks passed.");
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
