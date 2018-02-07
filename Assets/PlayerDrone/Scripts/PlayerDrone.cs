using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ExtensionMethods;

public class PlayerDrone : MonoBehaviour {
    #region Variables

    public int swarmControlMode = 1;
    public float swarmSpread = 1;

    public GameObject arrow; //the arrow pointer that points in the direction the swarm is being controlled
    public LayerMask aimRayLayerMask; //everything that should be ignored when shooting out the ray to move the carrot
    public ParticleSystem jetParticles; //particle emitter of jet
    public GameObject cannon, barrel, laser, jet, jetBall; //parts of the drone needed in this script
    public LauncheeScript launcheeScript; //script from the prefab of the launchee ball
    public float mouseSensitivity; //how quickly should the camera move when the player moves the mouse
    public bool glidey; //switches between an easier to control mode and a more physics based control
    public float glideySpeed = 7; //how fast the drone should move if glidey
    public float glideyTurnSpeed = 70; //how fast should the drone turn when glidey
    public float maxWalkSpeed = 20f; //whats the fastest the player should be able to go when not glidey
    public float kick = 50; //how hard should we kick the player in the direction they press when they are at a standstill
    public float sprintSpeed = 2.5f; //how much faster should the player go when sprinting
    public float slowSpeed = 0.3f; //how much slower should they go when crawling
    public float acceleration = 30f; //how quickly should the player reach max speed
    public float accelerationHoriz = 30f; //how quickly should the player reach max speed horizontally
    public float deceleration = 1; //how quickly shuold the player be slowed down when not pressing a movement key
    public float maxHoverHeight = 30; //the highest we can hover in low hover mode
    public float hoverForce = 65f; //multiplier for force output by jet. Mostly affects low hover mode
    public float maxVerticalSpeed = 24; //the fastest the drone can move up or down
    public float ballMass = 10; //mass of balls shot out
    public float maxPush = 3000; //max force that balls can be shot out
    public float minPush = 100; //min force that balls can be shot out

    [HideInInspector] public bool isControlled; //if true, player input controls the PlayerDrone
    [HideInInspector] public float vertInput, horizInput, turnInput; //movement input
    [HideInInspector] public float cannonAngle; //used by the camera script to keep the cannon aligned with the camera

    private float _hoverHeight = 1; //how high the drone is currently hovering
    private float _push = 300; //starting force of balls shot from drone
    private bool _lowHover; //should the drone hover above the ground or at an absolute height
    public float pGain = 2600; //the proportional gain used by the PID controller for attaining the desired hover hieght
    public float iGain = 40; //the integral gain
    public float dGain = 260; //differential gain
    private float _integrator = 19.6f; //error accumulator
    private float _lastError; //previous error
    private float _curPos = 2; // actual Pos
    private float _force = 785; // current force
    private Rigidbody _rb; //this rigidbody (for convenience)
    private PlayerDroneCamera _camera; //Camera script for the player drone
    private float _sprint = 1f; //Current sprint multiplier
    private Vector3 _fwdVec, _rightVec;
    private Transform _carrot;
    private SwarmDrone _leader;
    [HideInInspector] public List<GameObject> swarm;
    [HideInInspector] public Vector3 swarmVec, swarmCenterPos, swarmCenterDir;

    #endregion

    private void Awake() {
        _rb = GetComponent<Rigidbody>();
        float frac = (_push - minPush) / (maxPush - minPush);
        barrel.GetComponent<MeshRenderer>().material.SetColor("_EmisColor", Color.HSVToRGB(1f, frac + .2f, 1));
        _carrot = GameObject.FindGameObjectWithTag("Carrot").transform;
        _camera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<PlayerDroneCamera>();
    }

    private void Update() {
        swarmSpread += Input.GetAxis("Spread") * Time.deltaTime;
        SwarmControlMode();
        if(isControlled) PlayerControl();
    }

    private void FixedUpdate() {
        SwarmControlModeFixed();
        JetControl();
        Walk();
    }

    private void LateUpdate() { UpdateJetBall(); }


    #region SwarmControl

    /// <summary>
    /// Sets up the different control modes. <para />
    /// 0 is null; no control, drones just drift around. <para />
    /// 1 is carrot; carrot can be moved around scene, and swarm follows. <para />
    /// 2 is vector based control at swarm COM. <para />
    /// 3 is vector based control at center of implied circle w/ optional visualization of the circle. <para />
    /// 4 is leader that rest of the swarm flock to -- leader holds the carrot. <para />
    /// 5 is leader where carrot drags behind leader at distance based on size of swarm
    ///   when leader turns, carrot will slowly move to keep being behind the leader. <para />
    /// 6 is the above, but the carrot won't stay behind the leader, so it's like the leader is dragging the carrot on a chain.
    /// </summary>
    private void SwarmControlMode() {
        bool runOnce = false;

        for(int number = 0; number <= 9; number++) {
            if(!Input.GetKeyDown(number.ToString()) || swarmControlMode == number) continue;
            swarmControlMode = number;
            runOnce = true;
        }
        if(!runOnce) return;

        switch(swarmControlMode) {
            case 1:
                ActivateDrone();
                Destroy(GetComponent<LineRenderer>());
                DeelectLeader();
                break;
            case 2:
                DeactivateDrone();
                arrow.SetActive(true);
                Destroy(GetComponent<LineRenderer>());
                DeelectLeader();
                break;
            case 3:
                DeactivateDrone();
                arrow.SetActive(true);
                DeelectLeader();
                break;
            case 4:
                DeactivateDrone();
                arrow.SetActive(false);
                Destroy(GetComponent<LineRenderer>());
                DeelectLeader();
                StartCoroutine(ElectLeader());
                break;
            case 5:
                DeactivateDrone();
                arrow.SetActive(false);
                Destroy(GetComponent<LineRenderer>());
                DeelectLeader();
                StartCoroutine(ElectLeader());
                break;
            case 6:
                DeactivateDrone();
                arrow.SetActive(false);
                Destroy(GetComponent<LineRenderer>());
                DeelectLeader();
                StartCoroutine(ElectLeader());
                break;
            default:
                ActivateDrone();
                Destroy(GetComponent<LineRenderer>());
                DeelectLeader();
                break;
        }
    }

    /// <summary> Operates the different control modes </summary>
    private void SwarmControlModeFixed() {
        float swarmRadius = Mathf.Sqrt(swarmSpread * swarm.Count / Mathf.PI);
        Vector3 carrotTarget;
        switch(swarmControlMode) {
            case 1:
                MoveCarrot(Input.GetButton("Mouse0"));
                break;
            case 2:
                SetSwarmCentersByCOM();
                ControlSwarmParallel();
                break;
            case 3:
                SetSwarmCentersByShape(drawShape: true);
                ControlSwarmParallel();
                break;
            case 4:
                SetSwarmCentersByShape(drawShape: false);
                ControlSwarmLeader();
                _carrot.transform.position = _leader.transform.position;
                break;
            case 5:
                SetSwarmCentersByShape(drawShape: false);
                ControlSwarmLeader();
                carrotTarget = _leader.transform.position - _leader.transform.forward * swarmRadius;
                _carrot.transform.position = Extensions.SharpInDamp(_carrot.transform.position, carrotTarget, 0.3f);
                break;
            case 6:
                SetSwarmCentersByShape(drawShape: false);
                ControlSwarmLeader();
                carrotTarget = _leader.transform.position;
                if(Vector3.Distance(_carrot.transform.position, _leader.transform.position) > swarmRadius)
                    _carrot.transform.position = Extensions.SharpInDamp(_carrot.transform.position, carrotTarget, 0.3f);
                break;
        }
    }

    /// <summary> Take direct control of the swarm bot that is designated as the leader. </summary>
    private void ControlSwarmLeader() {
        //TODO consider using vector based control rather than directly controlling the motors of the leader-bot
        //  SetMoveByVector() would have to be re-enabled for the leader in FixedUpdate() in SwarmDrone.cs, 
        //  and botMoveVector would be set here instead of in FlockingControl() there.
        AutoControl(rotationMode: 2); //Camera rotation follows the leader
        _leader.fwdControl = Input.GetAxis("Vertical");
        float trnSpdCtrl = Mathf.Lerp(.6f, .4f, _leader.GetComponent<Rigidbody>().velocity.magnitude / 2.7f);
        _leader.turnControl = Input.GetAxis("Horizontal") * trnSpdCtrl;
    }

    /// <summary> Choose a leader and make it glow red </summary>
    public IEnumerator ElectLeader() {
        int rand = Random.Range(0, swarm.Count - 1);
        _leader = swarm[rand].GetComponent<SwarmDrone>();
        _leader.isLeader = true;
        if(_leader.GetComponent<Light>())
            yield return new WaitForEndOfFrame(); //Prevents errors when same leader is elected after being de-elected
        Light lgt = _leader.gameObject.AddComponent<Light>();
        lgt.color = Color.red;
        lgt.range = 1;
        lgt.intensity = 2;
        yield return null;
    }

    /// <summary> Un-set the current leader if there is one </summary>
    public void DeelectLeader() {
        if(_leader == null) return;
        _leader.isLeader = false;
        Destroy(_leader.gameObject.GetComponent<Light>());
    }

    /// <summary> Control the entire swarm in parallel by directly setting their goalVector </summary>
    private void ControlSwarmParallel() {
        AutoControl(rotationMode: 1); //Give the user control of camera rotation, but follow the swarmCenterPos

        arrow.transform.position = swarmCenterPos; //Keep an arrow centered in the swarm
        arrow.transform.forward = _camera.transform.up; //that shows the user where they are directing the swarm to go
        arrow.transform.localScale = new Vector3(1, 1, Input.GetAxis("Vertical"));

        Vector3 fwdControl = transform.forward * Input.GetAxis("Vertical");
        swarmVec = Extensions.SharpInDamp(swarmVec, swarmVec * Mathf.Abs(Input.GetAxis("Vertical")) + fwdControl, .5f);
        swarmVec = Vector3.ClampMagnitude(swarmVec, 1);
    }

    /// <summary> Stop controlling the player drone, making it essentially just a camera </summary>
    private void DeactivateDrone() {
        isControlled = false;
        laser.SetActive(false);
        foreach(var mesh in GetComponentsInChildren<MeshRenderer>()) { mesh.enabled = false; }
        foreach(var coll in GetComponentsInChildren<Collider>()) { coll.enabled = false; }
    }

    /// <summary> Start controlling the player drone, making it visible and giving the user direct control of it. </summary>
    private void ActivateDrone() {
        isControlled = true;
        laser.SetActive(true);
        arrow.SetActive(false);
        foreach(var mesh in GetComponentsInChildren<MeshRenderer>()) { mesh.enabled = true; }
        foreach(var coll in GetComponentsInChildren<Collider>()) { coll.enabled = true; }
    }

    /// <summary> Set swarmCenterPos and swarmCenterDir based on the averages of the bots within the swarm </summary>
    private void SetSwarmCentersByCOM() {
        swarmCenterPos = Vector3.zero;
        swarmCenterDir = Vector3.zero;
        foreach(GameObject bot in swarm) {
            swarmCenterPos += bot.transform.position;
            swarmCenterDir += bot.transform.forward;
        }
        swarmCenterPos /= swarm.Count;
        swarmCenterDir /= swarm.Count;
        swarmCenterDir.Normalize();
    }

    /// <summary>
    /// Draws a tight fitting convex polygon around the swarm in the x-z plane, and then sets swarmCenterPos to the centroid of that shape.
    /// swarmCenterDir is set as in SetSwarmCentersByCOM()
    /// </summary>
    /// <param name="drawShape">determines whether or not to draw the bounding convex shape of the swarm</param>
    private void SetSwarmCentersByShape(bool drawShape) {
        swarmCenterDir = Vector3.zero;
        List<Vector3> vertices = new List<Vector3>(); //The bot positions, used by the Jarvis March Algorithm
        foreach(GameObject bot in swarm) {
            vertices.Add(bot.transform.position);
            swarmCenterDir += bot.transform.forward;
        }
        swarmCenterDir /= swarm.Count;
        swarmCenterDir.Normalize();

        //Special cases that Jarvis March can't handle
        switch(vertices.Count) {
            case 0: return;
            case 1:
                swarmCenterPos = swarm[0].transform.position;
                return;
            case 2:
                swarmCenterPos = (swarm[0].transform.position + swarm[1].transform.position) / 2;
                return;
        }

        List<Vector3> hull = JarvisMarchAlgorithm.GetConvexHull(vertices);

        LineRenderer lr = null;
        if(drawShape) {
            lr = GetComponent<LineRenderer>() ? GetComponent<LineRenderer>() : gameObject.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Particles/Additive"));
            lr.widthMultiplier = 0.2f;
            lr.positionCount = hull.Count + 1;
        }

        float sX = 0, sY = 0, sZ = 0, sDist = 0;
        for(int i = 0; i < hull.Count; i++) {
            if(drawShape) lr.SetPosition(i, hull[i]); //set the points in the line renderer that will draw the shape...

            //This is basically just the formula for calculating the centroid. I don't know how it works, but it does.
            Vector3 prev = i == 0 ? hull.Last() : hull[i - 1];
            Vector3 curr = hull[i];
            float dist = Vector3.Distance(prev, curr);
            sX += (prev.x + curr.x) / 2 * dist;
            sY += (prev.y + curr.y) / 2 * dist;
            sZ += (prev.z + curr.z) / 2 * dist;
            sDist += dist;
        }
        if(drawShape) lr.SetPosition(hull.Count, hull[0]); //...and finally draw a line from the last point to the first to close the shape

        swarmCenterPos = new Vector3(sX / sDist, sY / sDist, sZ / sDist); //final step in centroid formula
    }

    /// <summary>
    /// Calculate the smallest radius needed to enclose the entire swarm, where the circle is centered at the specified center
    /// </summary>
    /// <param name="center">The center point of the enclosing circle</param>
    /// <returns></returns>
    private float GetSwarmRadius(Vector3 center) {
        float radius = 0;
        foreach(GameObject bot in swarm) {
            float distance = Vector3.Distance(bot.transform.position, center);
            if(distance > radius) radius = distance;
        }
        return radius;
    }

    /// <summary> Move the carrot to wherever the laser is pointing </summary>
    /// <param name="moveCarrotKey">Set to true to move carrot</param>
    private void MoveCarrot(bool moveCarrotKey) {
        if(!moveCarrotKey) return;

        RaycastHit hit;
        if(!Physics.Raycast(laser.transform.position, laser.transform.forward, out hit, 100f, aimRayLayerMask)) return;

        _carrot.transform.position = Extensions.SharpInDamp(_carrot.transform.position, hit.point, 1);
    }

    #endregion

    #region DroneAuto

    /// <summary> Make the player drone automatically follow and look at the swarm </summary>
    /// <param name="rotationMode">0 = rotate the camera to match the average direction of the swarm. <para />
    ///                            1 = let the player rotate the camera. <para />
    ///                            2 = rotate the camera to match the leader bot.</param>
    private void AutoControl(int rotationMode = 0) {
        //Position
        float swarmRadius = GetSwarmRadius(swarmCenterPos);
        Vector3 camPos = swarmCenterPos + Vector3.up * (-swarmCenterPos.y + transform.position.y);
        transform.position = Extensions.SharpInDamp(transform.position, camPos, 3f);

        //Rotation
        Vector3 camRot;
        Vector3 forward;
        float angle;
        switch(rotationMode) {
            case 1:
                _camera.smoothX = Input.GetAxis("Horizontal") * 2f;
                break;
            case 2:
                camRot = Vector3.ProjectOnPlane(_leader.transform.forward, Vector3.up);
                forward = Vector3.ProjectOnPlane(_camera.transform.up, Vector3.up);
                angle = Vector3.SignedAngle(forward, camRot, Vector3.up);
                _camera.smoothX = Extensions.SharpInDampAngle(0, angle, 2f);
                break;
            default:
                camRot = Vector3.ProjectOnPlane(swarmCenterDir, Vector3.up);
                forward = Vector3.ProjectOnPlane(_camera.transform.up, Vector3.up);
                angle = Vector3.SignedAngle(forward, camRot, Vector3.up);
                if(Input.GetButton("Vertical") && !Input.GetButton("Horizontal"))
                    angle = Mathf.Abs(angle) > 20 ? angle : 0;
                _camera.smoothX = Extensions.SharpInDampAngle(0, angle, .3f);
                break;
        }

        //Height
        float height = swarmRadius / Mathf.Tan(_camera.gameObject.GetComponent<Camera>().fieldOfView / 2 * Mathf.Deg2Rad) + 2.5f;
        _hoverHeight = Extensions.SharpInDamp(_hoverHeight, height, .3f);
    }

    #endregion

    #region DroneControlled

    /// <summary> All the buttons the player can press and the functions they control </summary>
    private void PlayerControl() {
        if(Input.GetKeyDown("y")) {
            glidey = !glidey;
        }
        vertInput = Input.GetAxis("Vertical");
        horizInput = Input.GetAxis("Horizontal");

        Sprint(Input.GetButton("Shift"), Input.GetButton("Ctrl"));
        ShootBall(Input.GetKeyDown("f"));
        UpdateShootStrength(Input.GetAxis("Mouse ScrollWheel"));
        HoverControl(Input.GetKeyDown("u"), "E", "Q");
    }

    /// <summary> Controls PlayerDrone movement based on vertInput, turnInput, and horizInput </summary>
    private void Walk() {
        if(glidey) {
            _rb.AddRelativeForce(horizInput * glideySpeed * _sprint, 0f, vertInput * glideySpeed * _sprint,
                ForceMode.Acceleration);
            _rb.AddRelativeTorque(0f, turnInput * glideyTurnSpeed * mouseSensitivity, 0f, ForceMode.Acceleration);
        } else {
            float velFwd = Vector3.Dot(_rb.velocity, transform.forward); //Current fwd speed. Used to see if Kick is necessary.
            float velRight = Vector3.Dot(_rb.velocity, transform.right); //Similar to ^

            //Speed player up in directions being pressed
            _fwdVec = transform.forward * acceleration * vertInput * Time.fixedDeltaTime * _sprint;
            _rightVec = transform.right * accelerationHoriz * horizInput * Time.fixedDeltaTime * _sprint;
            if(Mathf.Abs(velFwd) < maxWalkSpeed * _sprint)
                _rb.velocity += _fwdVec;
            if(Mathf.Abs(velRight) < maxWalkSpeed * _sprint)
                _rb.velocity += _rightVec;

            //Slow player down in directions not being pressed
            if(Mathf.Abs(horizInput) < 0.1f)
                _rb.velocity -= transform.right * velRight * deceleration / 5;
            if(Mathf.Abs(vertInput) < 0.1f)
                _rb.velocity -= transform.forward * velFwd * deceleration / 5;

            //::Kick - If pressing walk from standstill, gives a kick so walking is more responsive.
            if(vertInput > 0 && velFwd < maxWalkSpeed / 3) {
                _rb.AddRelativeForce(0, 0, kick * 30 * _sprint);
            } else if(vertInput < 0 && velFwd > -maxWalkSpeed / 3) {
                _rb.AddRelativeForce(0, 0, -kick * 30 * _sprint);
            }

            if(horizInput > 0 && velRight < maxWalkSpeed / 3) {
                _rb.AddRelativeForce(kick * 30 * _sprint, 0, 0);
            } else if(horizInput < 0 && velRight > -maxWalkSpeed / 3) {
                _rb.AddRelativeForce(-kick * 30 * _sprint, 0, 0);
            }
        }
    }

    /// <summary> Set the sprint multiplier to speed player up or slow player down. </summary>
    /// <param name="sprintPressed">Button that should speed player up</param>
    /// <param name="slowPressed">Button that should slow player down</param>
    private void Sprint(bool sprintPressed, bool slowPressed) {
        if(sprintPressed) {
            _sprint = sprintSpeed;
        } else if(slowPressed) {
            _sprint = slowSpeed;
        } else {
            _sprint = 1;
        }
    }

    /// <summary> Controls how and how high the player drone should hover </summary>
    /// <param name="changeHoverMode">Button to switch hover modes</param>
    /// <param name="hoverUpAxisName">Input axis name for axis the raises drone</param>
    /// <param name="hoverDownAxisName">Input axis name for axis the lowers drone</param>
    private void HoverControl(bool changeHoverMode, string hoverUpAxisName, string hoverDownAxisName) {
        if(changeHoverMode) {
            if(_lowHover) {
                _hoverHeight = transform.position.y; //make transition between hover modes smooth
            } else {
                RaycastHit hit;
                if(Physics.Raycast(transform.position, -transform.up, out hit, maxHoverHeight))
                    _hoverHeight = hit.distance; //make transition between hover modes smooth
            }

            _lowHover = !_lowHover;
        }

        //Stretch/shrink jet particle stream if we are rising or lowering
        if(Input.GetButton(hoverUpAxisName)) {
            var ps = jetParticles.velocityOverLifetime;
            ps.z = 6 * _sprint;
        } else if(Input.GetButton(hoverDownAxisName)) {
            var ps = jetParticles.velocityOverLifetime;
            ps.z = -6 * _sprint;
        } else if(Input.GetButtonUp(hoverUpAxisName) || Input.GetButtonUp(hoverDownAxisName)) {
            var ps = jetParticles.velocityOverLifetime;
            ps.z = 0;
        }

        //Make hover height change more rapidly when higher up, and more precisely when close to the ground
        _hoverHeight += (Input.GetAxis(hoverUpAxisName) - Input.GetAxis(hoverDownAxisName)) *
                        Mathf.Pow(Mathf.Max(Mathf.Min(_hoverHeight, 20), 1), 0.6f) / 40 * _sprint;
        if(_lowHover) _hoverHeight = Mathf.Clamp(_hoverHeight, 0.3f, maxHoverHeight);
    }

    /// <summary> Shoots a ball out of the drone's cannon </summary>
    /// <param name="shootKey">The key to press to shoot a ball</param>
    private void ShootBall(bool shootKey) {
        if(!shootKey) return;
        LauncheeScript shot = Instantiate(launcheeScript, cannon.transform.position, gameObject.transform.rotation);
        shot.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        shot.launcherScript = GetComponent<PlayerDrone>();
        shot.barrelT = barrel.transform;
        shot.push = _push;
        shot.size = 0.115f;
        shot.GetComponent<Rigidbody>().mass = ballMass;
    }

    /// <summary> Updates the strength with which balls are shot from the drone </summary>
    /// <param name="shootStrengthAxis">The GetAxis(...) value for changing the shooting strength</param>
    private void UpdateShootStrength(float shootStrengthAxis) {
        if(!(Mathf.Abs(shootStrengthAxis) > 0.01f)) return;
        _push += shootStrengthAxis * 400;
        _push = Mathf.Clamp(_push, minPush, maxPush);

        float frac = (_push - minPush) / (maxPush - minPush);
        barrel.GetComponent<MeshRenderer>().material.SetColor("_EmisColor", Color.HSVToRGB(1f, frac + .2f, 1));
    }

    #endregion

    #region DroneGeneral

    /// <summary> 
    /// Controls the direction of the jet and jet ball, keeping the drone balanced. <para />
    /// Also controls the jet force to reach the desired hover height.
    /// </summary>
    private void JetControl() {
        Quaternion jbr = Quaternion.FromToRotation(-transform.up, Vector3.down);
        float downDot = Vector3.Dot(transform.up, Vector3.down);
        float rightDot = Vector3.Dot(transform.right, Vector3.down);
        if(downDot > -0.3f && downDot < 0.3f) {
            jbr = Quaternion.Euler(-transform.up) *
                  Quaternion.AngleAxis(-50 * rightDot / Mathf.Abs(rightDot), transform.forward);
        } else if(downDot > 0.3f) {
            jbr = Quaternion.Euler(Vector3.right) * Quaternion.AngleAxis(30, transform.right);
        }

        jet.transform.rotation = jbr;
        jetBall.transform.rotation = Quaternion.Slerp(jetBall.transform.rotation, jbr,
            Quaternion.Angle(jetBall.transform.rotation, jbr) * Time.fixedDeltaTime / 10);

        Vector3 jetDir = -jet.transform.up;
        Vector3 posVec = transform.position - jet.transform.position;
        _rb.AddTorque(-Vector3.Cross(jetDir, posVec) * hoverForce * 3);
        if(!(Vector3.Dot(jet.transform.up, Vector3.down) < .5)) return; //Break if the jet is pointing up
        if(_lowHover) {
            Ray ray = new Ray(transform.position, -transform.up);
            RaycastHit hit;
            if(Physics.Raycast(ray, out hit, _hoverHeight)) {
                float proportionalHeight = (_hoverHeight - hit.distance) / _hoverHeight;
                Vector3 appliedHoverForce = jet.transform.up * proportionalHeight * hoverForce;
                _rb.AddForce(appliedHoverForce, ForceMode.Acceleration);
            } else {
                _rb.AddForce(hoverForce * 20 * Vector3.down);
            }
        } else {
            _curPos = transform.position.y;
            float error = _hoverHeight - _curPos; //generate the error signal
            _integrator += error * Time.fixedDeltaTime; //integrate error
            float diff = (error - _lastError) / Time.fixedDeltaTime; //differentiate error
            _lastError = error;
            //calculate the force summing the 3 errors with respective gains:
            _force = error * pGain + _integrator * iGain + diff * dGain;
            if(Mathf.Abs(Vector3.Dot(_rb.velocity, transform.up)) < maxVerticalSpeed) {
                _rb.AddForce(_force * jet.transform.up); // apply the force to accelerate the rigidbody
            }
        }
    }

    /// <summary> Updates the jet ball to point in the right direction </summary>
    private void UpdateJetBall() {
        if(!isControlled) return;
        jetBall.transform.Rotate(transform.right * vertInput * 6, Space.World);
        jetBall.transform.Rotate(transform.forward * -horizInput * 6, Space.World);
    }

    #endregion
}