using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectX
{
    /// <summary>
    /// Edit-mode helper: while enabled (menu ProjectX/Game View Follows Scene View), the Game view shows the
    /// in-game framing as if Rian stood on the play path at the Scene view camera's X. The pose comes from the
    /// real Cinemachine pipeline (with <see cref="PathCamera"/>'s rotation for that X) and is applied to the main
    /// camera just before it renders, so nothing in the scene is changed.
    /// </summary>
    [InitializeOnLoad]
    static class GameViewFollowsSceneView
    {
        const string MenuPath = "ProjectX/Game View Follows Scene View";
        const string PrefKey = "ProjectX.GameViewFollowsSceneView";
        const float WalkStep = 0.25f;   // metres per ground probe while walking the path
        const float StepUp = 1.2f;      // highest ledge Rian climbs onto in one step
        const float MaxDrop = 1.5f;     // deeper than this in one step is a gap she jumps over
        const float HeightSmoothTime = 0.35f;
        const float Settled = 0.001f;

        static PathCamera pathCamera;
        static float lastX = float.NaN;
        static float smoothY = float.NaN;
        static float yVelocity;
        static double lastRenderTime;

        static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, false);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static GameViewFollowsSceneView()
        {
            RenderPipelineManager.beginCameraRendering += Override;
            SceneView.duringSceneGui += RepaintGameViewOnMove;
        }

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            Enabled = !Enabled;
            smoothY = float.NaN;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        static bool TryGetSceneCamera(out Camera sceneCamera)
        {
            sceneCamera = null;
            if (!Enabled || EditorApplication.isPlayingOrWillChangePlaymode)
                return false;
            var view = SceneView.lastActiveSceneView;
            if (view == null || view.camera == null)
                return false;
            sceneCamera = view.camera;
            return true;
        }

        static void Override(ScriptableRenderContext context, Camera camera)
        {
            if (camera != Camera.main || !TryGetSceneCamera(out var sceneCamera))
                return;
            if (pathCamera == null)
                pathCamera = Object.FindAnyObjectByType<PathCamera>();
            if (pathCamera == null || !pathCamera.TryGetComponent(out CinemachineCamera vcam) || vcam.Follow == null)
                return;

            float x = sceneCamera.transform.position.x;
            Transform target = vcam.Follow;
            Vector3 stand = new Vector3(x, 0f, PlayPath.ZAt(x));
            stand.y = SmoothHeight(WalkedGroundY(target.position, x));

            // Run the vcam once with PathCamera's rotation for this X, read the result, then put it back.
            Transform t = vcam.transform;
            t.GetPositionAndRotation(out Vector3 savedPosition, out Quaternion savedRotation);
            Vector3 euler = savedRotation.eulerAngles;
            Vector2 rotation = pathCamera.RotationAt(x, 1f, euler.x, euler.y);
            t.rotation = Quaternion.Euler(rotation.x, rotation.y, 0f);
            vcam.InternalUpdateCameraState(Vector3.up, -1f);
            CameraState state = vcam.State;
            t.SetPositionAndRotation(savedPosition, savedRotation);

            // The composer has no damping at deltaTime < 0, so its result just shifts with the target.
            camera.transform.SetPositionAndRotation(
                state.GetFinalPosition() + (stand - target.position), state.GetFinalOrientation());
            camera.fieldOfView = state.Lens.FieldOfView;
        }

        // Walks the path from the target's position to x, following the ground the way Rian would: small steps
        // up and down are taken, and deeper drops (chasms, pools) are crossed at the current height.
        static float WalkedGroundY(Vector3 start, float x)
        {
            float y = start.y;
            int steps = Mathf.CeilToInt(Mathf.Abs(x - start.x) / WalkStep);
            for (int i = 1; i <= steps; i++)
            {
                float px = Mathf.Lerp(start.x, x, i / (float)steps);
                var origin = new Vector3(px, y + StepUp, PlayPath.ZAt(px));
                if (TryGroundBelow(origin, StepUp + MaxDrop, out float ground))
                    y = ground;
            }
            return y;
        }

        static bool TryGroundBelow(Vector3 origin, float distance, out float ground)
        {
            ground = 0f;
            float nearest = float.PositiveInfinity;
            var hits = Physics.RaycastAll(origin, Vector3.down, distance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider is CharacterController || hit.distance >= nearest)
                    continue;
                nearest = hit.distance;
                ground = hit.point.y;
            }
            return !float.IsPositiveInfinity(nearest);
        }

        // Eases the framing height toward the ground height, and keeps the Game view repainting until it settles.
        static float SmoothHeight(float goal)
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min((float)(now - lastRenderTime), 0.1f);
            lastRenderTime = now;
            if (float.IsNaN(smoothY))
            {
                smoothY = goal;
                yVelocity = 0f;
            }
            smoothY = Mathf.SmoothDamp(smoothY, goal, ref yVelocity, HeightSmoothTime, Mathf.Infinity, dt);
            if (Mathf.Abs(smoothY - goal) > Settled)
                EditorApplication.QueuePlayerLoopUpdate();
            return smoothY;
        }

        static void RepaintGameViewOnMove(SceneView view)
        {
            if (view != SceneView.lastActiveSceneView || !TryGetSceneCamera(out var sceneCamera))
                return;
            float x = sceneCamera.transform.position.x;
            if (x == lastX)
                return;
            lastX = x;
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}
