using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

[RequireComponent(typeof(PlayableDirector), typeof(Animator))]
public sealed class G2DisplayMode : MonoBehaviour
{
    [SerializeField] Camera displayCamera;
    [SerializeField] Light frontFill;
    [SerializeField] Light studioSpotlight;
    enum RobotCount
    {
        [InspectorName("1 Robot")] One,
        [InspectorName("3 Robots")] Three
    }

    [SerializeField] RobotCount robotCount;
    [SerializeField, Min(.5f)] float sideOffset = 2.85f;
    [SerializeField, Range(0, 40)] float inwardAngle = 23f;

    PlayableDirector center;
    readonly PlayableDirector[] sides = new PlayableDirector[2];
    readonly Light[] sideFills = new Light[2];
    readonly Light[] sideSpots = new Light[2];
    float singleFieldOfView;
    bool showThree;
    bool showControls = true;

    void Start()
    {
        center = GetComponent<PlayableDirector>();
        singleFieldOfView = displayCamera.fieldOfView;
        SetThreeRobots(robotCount == RobotCount.Three);
    }

    void Update()
    {
        bool threeRequested = robotCount == RobotCount.Three;
        if (threeRequested != showThree) SetThreeRobots(threeRequested);

        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            SetThreeRobots(false);
        if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            SetThreeRobots(true);
        if (keyboard.hKey.wasPressedThisFrame) showControls = !showControls;
        if (keyboard.rKey.wasPressedThisFrame) Replay();
    }

    void OnGUI()
    {
        if (!showControls || !center) return;
        GUILayout.BeginArea(new Rect(12, 12, 230, 34));
        GUILayout.BeginHorizontal();
        GUI.enabled = showThree;
        if (GUILayout.Button("1 robot")) SetThreeRobots(false);
        GUI.enabled = !showThree;
        if (GUILayout.Button("3 robots")) SetThreeRobots(true);
        GUI.enabled = true;
        if (GUILayout.Button("Replay")) Replay();
        if (GUILayout.Button("Hide")) showControls = false;
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    void Replay()
    {
        center.time = 0;
        center.Play();
        center.Evaluate();
    }

    void LateUpdate()
    {
        if (!showThree) return;
        foreach (var side in sides)
        {
            side.time = center.time;
            side.Evaluate();
        }
    }

    void SetThreeRobots(bool enabled)
    {
        showThree = enabled;
        robotCount = enabled ? RobotCount.Three : RobotCount.One;
        if (enabled && !sides[0])
        {
            CreateSide(0, -1);
            CreateSide(1, 1);
        }

        // A narrow preview needs more camera width than the wall's wide canvas.
        const float minimumHorizontalView = 86f;
        float verticalView = 2f * Mathf.Atan(Mathf.Tan(minimumHorizontalView * .5f * Mathf.Deg2Rad)
            / displayCamera.aspect) * Mathf.Rad2Deg;
        displayCamera.fieldOfView = enabled ? Mathf.Max(singleFieldOfView, verticalView) : singleFieldOfView;
        for (int i = 0; i < sides.Length; i++)
        {
            if (!sides[i]) continue;
            sides[i].gameObject.SetActive(enabled);
            sideFills[i].gameObject.SetActive(enabled);
            sideSpots[i].gameObject.SetActive(enabled);
            if (enabled)
            {
                sides[i].time = center.time;
                sides[i].Evaluate();
            }
        }
    }

    void CreateSide(int index, int sign)
    {
        var label = sign < 0 ? "Left" : "Right";
        var side = new GameObject("G2 " + label);
        side.SetActive(false);
        var horizontal = Vector3.ProjectOnPlane(displayCamera.transform.right, Vector3.up).normalized;
        var offset = horizontal * (sign * sideOffset);
        side.transform.SetPositionAndRotation(transform.position + offset,
            Quaternion.AngleAxis(sign * inwardAngle, Vector3.up) * transform.rotation);

        // Clone the complete animated hierarchy so the mesh retains its bone references.
        foreach (Transform child in transform)
        {
            var clone = Instantiate(child.gameObject, side.transform, false);
            clone.name = child.name;
        }
        var animator = side.AddComponent<Animator>();
        var director = side.AddComponent<PlayableDirector>();
        director.playOnAwake = false;
        director.timeUpdateMode = DirectorUpdateMode.Manual;
        director.extrapolationMode = DirectorWrapMode.Hold;
        director.playableAsset = center.playableAsset;
        foreach (var output in center.playableAsset.outputs)
            if (center.GetGenericBinding(output.sourceObject) == GetComponent<Animator>())
                director.SetGenericBinding(output.sourceObject, animator);
        sides[index] = director;

        // Aim both lights along this lane's mid-stride position.
        var laneOffset = offset - horizontal * (sign * .62f);
        sideFills[index] = CreateLaneLight(frontFill, laneOffset, "Studio " + label + " Fill", .85f);
        sideSpots[index] = CreateLaneLight(studioSpotlight, laneOffset, "Studio " + label + " Spotlight", .52f);
    }

    static Light CreateLaneLight(Light source, Vector3 offset, string lightName, float strength)
    {
        var light = Instantiate(source, source.transform.position + offset, source.transform.rotation);
        light.name = lightName;
        light.intensity *= strength;
        return light;
    }
}

